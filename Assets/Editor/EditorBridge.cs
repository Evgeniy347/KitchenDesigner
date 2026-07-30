using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Мост к ЖИВОМУ редактору: тесты и сборки запускаются в уже открытом
/// Unity вместо холодного batch-старта.
///
/// Зачем. Каждый вызов `Unity.exe -batchMode` платит фиксированные 60–90 секунд:
/// лицензия, Asset Pipeline Refresh (12 с), три domain reload. На прогоне одного
/// тестового класса это 2 минуты вместо 20 секунд, и именно из этого набегают
/// многоминутные циклы «правка → проверка». Мост убирает всю фиксированную часть:
/// платить приходится только за компиляцию изменённых скриптов и сами тесты.
///
/// Слушатель поднимается в ЛЮБОМ открытом редакторе (`[InitializeOnLoad]`), а не
/// только в запущенном скриптами: тогда исчезает и вторая беда — «закрой Unity,
/// иначе batch не стартует» (проект держит эксклюзивный лок).
///
/// Протокол намеренно примитивный — строка на запрос, строка на ответ, чтобы
/// клиентом мог быть однострочник на PowerShell:
///
///   PING                                  → OK &lt;project&gt;|&lt;unity&gt;|&lt;busy&gt;
///   RUN &lt;EditMode|PlayMode&gt;|&lt;filter&gt;|&lt;xml&gt; → OK &lt;jobId&gt;
///   STATUS &lt;jobId&gt;                        → OK running | OK done|total|passed|failed
///   METHOD &lt;Class.Method&gt;                 → OK &lt;jobId&gt;
///   QUIT                                  → OK
///
/// Состояние задач лежит в <see cref="SessionState"/> — оно переживает domain
/// reload, а он случается посреди прогона всегда, когда изменились скрипты.</summary>
[InitializeOnLoad]
public static class EditorBridge
{
    public const int Port = 9338;

    private const string JobPrefix = "EditorBridge.job.";
    private const string JobCounterKey = "EditorBridge.jobCounter";

    private static TcpListener? _listener;
    private static Thread? _thread;
    private static volatile bool _stopping;

    private static readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

    /// <summary>Зеркало состояния задач для ответов БЕЗ главного потока:
    /// jobId → "state|counts|error". Пишется из главного потока (SetJob),
    /// читается из потока сокета.</summary>
    private static readonly ConcurrentDictionary<string, string> _jobs =
        new ConcurrentDictionary<string, string>();

    /// <summary>Заготовленный ответ на PING и время последнего тика главного
    /// потока. Оба обновляются в <see cref="Pump"/>.</summary>
    private static volatile string _pingInfo = "starting|?|?";
    private static long _lastPumpTicks = DateTime.UtcNow.Ticks;

    private static readonly string _projectName = Path.GetFileName(Directory.GetCurrentDirectory());
    private static readonly string _unityVersion = Application.unityVersion;

    static EditorBridge()
    {
        EditorApplication.update += Pump;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;

        RestoreJobMirror();
        LogLifecycle();

        // Колбэки регистрируются заново на каждый domain reload: прогон его
        // переживает, а подписка — нет.
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new RunCallbacks());

        Start();
    }

    /// <summary>Точка входа для `-executeMethod`: держит batch-редактор живым.
    /// Возврата из метода достаточно — без `-quit` редактор продолжает крутить
    /// свой цикл и обслуживать сокет.</summary>
    public static void Daemon()
    {
        Debug.Log($"[EditorBridge] daemon ready on port {Port}");
    }

    /// <summary>Вернуть в зеркало состояние текущей задачи после domain reload.
    ///
    /// Зеркало живёт в статике и умирает вместе с доменом, а domain reload
    /// случается ПОСРЕДИ прогона всегда: на перекомпиляции и на входе в play
    /// mode. Без восстановления клиент после reload получал бы «unknown job» на
    /// задачу, которая на самом деле идёт.</summary>
    private static void RestoreJobMirror()
    {
        string jobId = SessionState.GetString("EditorBridge.currentJob", "");
        if (string.IsNullOrEmpty(jobId)) return;

        string state = SessionState.GetString(JobPrefix + jobId + ".state", "");
        if (string.IsNullOrEmpty(state)) return;

        _jobs[jobId] = $"{state}|{SessionState.GetString(JobPrefix + jobId + ".counts", "")}"
                     + $"|{SessionState.GetString(JobPrefix + jobId + ".error", "")}";
    }

    /// <summary>Метки в лог обо всём, что рвёт связь с клиентом: перезагрузка
    /// домена, вход и выход из play mode. Без них падение демона на PlayMode
    /// выглядит как «мост просто замолчал» и не диагностируется.</summary>
    private static void LogLifecycle()
    {
        Debug.Log("[EditorBridge] domain loaded");
        AssemblyReloadEvents.beforeAssemblyReload += () => Debug.Log("[EditorBridge] beforeAssemblyReload");
        EditorApplication.quitting += () => Debug.Log("[EditorBridge] editor quitting");
        EditorApplication.playModeStateChanged += s => Debug.Log($"[EditorBridge] playMode: {s}");
    }

    // ── Сокет ───────────────────────────────────────────────────────────

    /// <summary>Поднять слушателя. Привязка к порту повторяется В СВОЁМ ПОТОКЕ,
    /// пока не удастся.
    ///
    /// Порт часто занят в первую секунду: предыдущий слушатель отпустил его
    /// только что (domain reload закрывает сокет), и он ещё в TIME_WAIT.
    /// Раньше повтор жил в <see cref="Pump"/>, то есть в
    /// `EditorApplication.update`, — и это ломалось ровно там, где было нужнее
    /// всего: на входе в PLAY MODE. Домен перезагружается, привязка не удаётся,
    /// а до Pump в batch-редакторе очередь не доходит — мост молчит весь
    /// play-сеанс. Полный набор PlayMode из-за этого выглядел как зависание,
    /// хотя тесты честно отрабатывали до конца (в логе есть и
    /// ExitingPlayMode, и EnteredEditMode).</summary>
    private static void Start()
    {
        if (_thread != null && _thread.IsAlive) return;
        _stopping = false;
        _thread = new Thread(BindAndAccept) { IsBackground = true, Name = "EditorBridge" };
        _thread.Start();
    }

    private static void BindAndAccept()
    {
        while (!_stopping)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, Port);
                listener.Start();
                _listener = listener;
                break;
            }
            catch (SocketException)
            {
                // Порт ещё не отпущен (или рядом поднимается второй редактор —
                // от этого защищает замок в шлюзе). Ждём и пробуем снова.
                Thread.Sleep(250);
            }
        }
        if (_stopping) return;
        Accept();
    }

    private static void Stop()
    {
        _stopping = true;
        try { _listener?.Stop(); } catch (Exception) { /* редактор закрывается */ }
        _listener = null;
        _thread = null;
    }

    private static void Accept()
    {
        while (!_stopping)
        {
            TcpClient client;
            try { client = _listener!.AcceptTcpClient(); }
            catch (Exception) { return; } // Stop() или reload — выходим тихо

            // Соединение — на свой поток.
            //
            // Раньше Handle звался прямо здесь, и приём стоял, пока команда не
            // ответит. А команда может ждать главный поток минутами (REFRESH с
            // перекомпиляцией, сборка плеера). Тогда `PING` не получал даже
            // шанса быть принятым — клиент видел мёртвый мост и убивал живой
            // редактор в разгар работы. Неблокирующий ответ на PING бесполезен,
            // пока его соединение не берут в руки.
            var worker = new Thread(() =>
            {
                try { Handle(client); }
                catch (Exception e) { Debug.LogWarning($"[EditorBridge] {e.Message}"); }
                finally { client.Close(); }
            }) { IsBackground = true, Name = "EditorBridge.client" };
            worker.Start();
        }
    }

    private static void Handle(TcpClient client)
    {
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        string? line = reader.ReadLine();
        if (string.IsNullOrEmpty(line)) return;

        string reply;
        try
        {
            // PING и STATUS отвечают ПРЯМО ЗДЕСЬ, по зеркалу состояния.
            //
            // Раньше они, как и всё остальное, вставали в очередь главного
            // потока — и пока тот занят (вход в play mode, сборка, длинный
            // тест), клиент не получал ответа вообще. Скрипт-шлюз считает
            // молчание сокета зависанием и через -SilenceMinutes УБИВАЕТ живой
            // редактор посреди работы. То есть самая частая команда «как там
            // дела» ломала ровно то, о чём спрашивала.
            reply = Fast(line!) ?? OnMainThread(() => Execute(line!));
        }
        catch (Exception e) { reply = "ERR " + e.Message.Replace('\n', ' '); }

        writer.WriteLine(reply);
    }

    /// <summary>Команды, которым главный поток не нужен: ответ собирается из
    /// зеркала. Возвращает null, если команда не из этих — тогда она пойдёт
    /// обычным путём.</summary>
    private static string? Fast(string line)
    {
        int sp = line.IndexOf(' ');
        string cmd = (sp < 0 ? line : line.Substring(0, sp)).Trim().ToUpperInvariant();
        string arg = sp < 0 ? "" : line.Substring(sp + 1).Trim();

        switch (cmd)
        {
            case "PING":
                // age — сколько секунд назад главный поток тикал в последний
                // раз. Это и есть разница между «занят» и «завис»: клиенту
                // больше не нужно гадать по молчанию сокета.
                double age = (DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastPumpTicks))).TotalSeconds;
                return $"OK {_pingInfo}|age={age.ToString("F1", CultureInfo.InvariantCulture)}";

            case "STATUS":
                return _jobs.TryGetValue(arg, out var s) ? "OK " + s : "ERR unknown job";

            default:
                return null;
        }
    }

    /// <summary>Выполнить работу в главном потоке и дождаться ответа: почти всё
    /// API редактора вне его падает.</summary>
    private static string OnMainThread(Func<string> work)
    {
        string result = "ERR timeout";
        using var done = new ManualResetEventSlim(false);

        lock (_mainThreadQueue)
        {
            _mainThreadQueue.Enqueue(() =>
            {
                try { result = work(); }
                catch (Exception e) { result = "ERR " + e.Message.Replace('\n', ' '); }
                finally { done.Set(); }
            });
        }

        // Долгие операции (сборка плеера) блокируют главный поток целиком,
        // поэтому ждём щедро: клиент всё равно опрашивает STATUS.
        done.Wait(TimeSpan.FromMinutes(30));
        return result;
    }

    private static void Pump()
    {
        // Зеркало для PING: собрать здесь, в главном потоке, чтобы поток сокета
        // мог отвечать не трогая API редактора.
        Interlocked.Exchange(ref _lastPumpTicks, DateTime.UtcNow.Ticks);
        _pingInfo = $"{_projectName}|{_unityVersion}|"
                  + (EditorApplication.isCompiling ? "compiling"
                     : EditorApplication.isPlaying ? "playing" : "idle");

        // Привязку к порту Pump больше не двигает — она живёт в своём потоке
        // (см. BindAndAccept). Здесь остаётся только очередь работы для
        // главного потока.
        while (true)
        {
            Action action;
            lock (_mainThreadQueue)
            {
                if (_mainThreadQueue.Count == 0) return;
                action = _mainThreadQueue.Dequeue();
            }
            action();
        }
    }

    // ── Команды ─────────────────────────────────────────────────────────

    private static string Execute(string line)
    {
        int sp = line.IndexOf(' ');
        string cmd = (sp < 0 ? line : line.Substring(0, sp)).Trim().ToUpperInvariant();
        string arg = sp < 0 ? "" : line.Substring(sp + 1).Trim();

        switch (cmd)
        {
            case "PING":
                return $"OK {Path.GetFileName(Directory.GetCurrentDirectory())}|"
                     + $"{Application.unityVersion}|{(EditorApplication.isCompiling ? "compiling" : "idle")}";

            case "RUN":
                return StartRun(arg);

            case "STATUS":
                return Status(arg);

            case "METHOD":
                return StartMethod(arg);

            case "REFRESH":
                AssetDatabase.Refresh();
                return "OK";

            case "QUIT":
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
                return "OK";

            default:
                return $"ERR unknown command '{cmd}'";
        }
    }

    /// <summary>RUN EditMode|Filter|C:\path\results.xml — фильтр и путь
    /// необязательны.</summary>
    private static string StartRun(string arg)
    {
        // Разделитель — ТАБУЛЯЦИЯ, а не '|'.
        //
        // Фильтр уходит в NUnit как регулярка по имени, а в регулярке '|' —
        // обычный символ: исключение вида `^(?!.*(A|B)).*$` резалось протоколом
        // пополам, до NUnit доезжало `^(?!.*(A`, и тот падал на КАЖДОМ тесте
        // с «Not enough )'s». Прогон при этом не заканчивался никогда — десять
        // минут до сторожа. Табуляции в именах классов и в путях не бывает.
        var parts = arg.Split('\t');
        if (parts.Length == 1) parts = arg.Split('|');   // старые вызовы без фильтра
        string platform = parts.Length > 0 ? parts[0].Trim() : "EditMode";
        string filter = parts.Length > 1 ? parts[1].Trim() : "";
        string resultPath = parts.Length > 2 ? parts[2].Trim() : "";

        var mode = platform.Equals("PlayMode", StringComparison.OrdinalIgnoreCase)
            ? TestMode.PlayMode
            : TestMode.EditMode;

        var testFilter = new Filter { testMode = mode };
        if (!string.IsNullOrEmpty(filter))
            testFilter.groupNames = new[] { filter };

        ResetGlobalAssets();

        string jobId = NextJobId();
        SetJob(jobId, "running", "");
        SessionState.SetString(JobPrefix + jobId + ".results", resultPath);
        SessionState.SetString("EditorBridge.currentJob", jobId);

        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(testFilter));
        return "OK " + jobId;
    }

    /// <summary>Вернуть глобальные ассеты к тому, что лежит на диске.
    ///
    /// Холодный batch давал изоляцию бесплатно: каждый прогон — новый процесс,
    /// `KitchenSettings` читается из Resources заново. В живом редакторе объект
    /// переживает прогон, и правки настроек из одного набора утекают в
    /// следующий: снапшот-тесты сериализуют настройки целиком и краснеют на
    /// `autoSave`/`edgeOutline`/`gridStep`, оставленных соседями.
    ///
    /// `UnloadAsset` делает ссылку «сломанной», а геттер `KitchenSettings.Instance`
    /// проверяет её на null и перечитывает ассет с диска — то же состояние, что
    /// у свежего процесса.</summary>
    private static void ResetGlobalAssets()
    {
        var settings = Resources.Load<KitchenDesigner.Core.KitchenSettings>("KitchenSettings");
        if (settings != null) Resources.UnloadAsset(settings);
    }

    /// <summary>METHOD BuildProject.Build — статический метод редактора
    /// (сборка плеера). Идёт синхронно в главном потоке.</summary>
    private static string StartMethod(string arg)
    {
        string jobId = NextJobId();
        SetJob(jobId, "running", "");

        int dot = arg.LastIndexOf('.');
        if (dot <= 0) return "ERR expected Class.Method";

        string typeName = arg.Substring(0, dot);
        string methodName = arg.Substring(dot + 1);

        var type = FindType(typeName);
        if (type == null) return $"ERR type '{typeName}' not found";

        var method = type.GetMethod(methodName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (method == null) return $"ERR method '{methodName}' not found";

        // Сборки завершаются вызовом EditorApplication.Exit — он рассчитан на
        // batch с `-quit`. В живом редакторе это убило бы демон, поэтому просим
        // BuildProject вернуть код вместо выхода.
        BuildProject.KeepEditorAlive = true;
        BuildProject.LastExitCode = 0;
        try
        {
            method.Invoke(null, null);
            int code = BuildProject.LastExitCode;
            if (code != 0) SetJobError(jobId, $"exit code {code}");
            SetJob(jobId, code == 0 ? "done" : "failed", $"0|0|{code}");
        }
        catch (Exception e)
        {
            SetJobError(jobId, (e.InnerException ?? e).Message.Replace('\n', ' '));
            SetJob(jobId, "failed", "0|0|1");
        }
        finally
        {
            BuildProject.KeepEditorAlive = false;
        }
        return "OK " + jobId;
    }

    private static Type? FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    private static string Status(string jobId)
    {
        string state = SessionState.GetString(JobPrefix + jobId + ".state", "");
        if (string.IsNullOrEmpty(state)) return "ERR unknown job";

        string counts = SessionState.GetString(JobPrefix + jobId + ".counts", "");
        string error = SessionState.GetString(JobPrefix + jobId + ".error", "");
        return $"OK {state}|{counts}|{error}";
    }

    // ── Состояние задач ─────────────────────────────────────────────────

    private static string NextJobId()
    {
        int n = SessionState.GetInt(JobCounterKey, 0) + 1;
        SessionState.SetInt(JobCounterKey, n);
        return n.ToString(CultureInfo.InvariantCulture);
    }

    private static void SetJob(string jobId, string state, string counts)
    {
        SessionState.SetString(JobPrefix + jobId + ".state", state);
        SessionState.SetString(JobPrefix + jobId + ".counts", counts);
        // SessionState переживает domain reload, зеркало — нет; зато зеркало
        // читается без главного потока. Нужны оба, поэтому пишем в оба.
        _jobs[jobId] = $"{state}|{counts}|{SessionState.GetString(JobPrefix + jobId + ".error", "")}";
    }

    /// <summary>Причина падения. Пишется ДО <see cref="SetJob"/>: клиент,
    /// увидевший «failed», должен сразу видеть и текст, а не пустую строку.</summary>
    private static void SetJobError(string jobId, string error)
    {
        SessionState.SetString(JobPrefix + jobId + ".error", error);
    }

    // ── Колбэки тестового раннера ───────────────────────────────────────

    private sealed class RunCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            string jobId = SessionState.GetString("EditorBridge.currentJob", "");
            if (string.IsNullOrEmpty(jobId)) return;

            int passed = result.PassCount;
            int failed = result.FailCount;
            int skipped = result.SkipCount;
            int total = passed + failed + skipped + result.InconclusiveCount;

            string path = SessionState.GetString(JobPrefix + jobId + ".results", "");
            if (!string.IsNullOrEmpty(path))
            {
                try { WriteXml(path, result, total, passed, failed, skipped); }
                catch (Exception e) { Debug.LogWarning($"[EditorBridge] отчёт не записан: {e.Message}"); }
            }

            SetJob(jobId, failed > 0 ? "failed" : "done", $"{total}|{passed}|{failed}");
            SessionState.SetString("EditorBridge.currentJob", "");
        }

        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
    }

    /// <summary>Отчёт в том же формате, что пишет `-runTests`: корневой
    /// `test-run` со счётчиками и `test-case` на каждый НЕзелёный тест —
    /// разбор на стороне скриптов остаётся прежним.</summary>
    private static void WriteXml(string path, ITestResultAdaptor result,
        int total, int passed, int failed, int skipped)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir!);

        var settings = new XmlWriterSettings { Indent = true };
        using var w = XmlWriter.Create(path, settings);

        w.WriteStartElement("test-run");
        w.WriteAttributeString("total", total.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("passed", passed.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("failed", failed.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("skipped", skipped.ToString(CultureInfo.InvariantCulture));
        w.WriteAttributeString("duration",
            result.Duration.ToString("F3", CultureInfo.InvariantCulture));
        WriteProblems(w, result);
        WriteSlowest(w, result);
        w.WriteEndElement();
    }

    /// <summary>Пять самых долгих классов прогона.
    ///
    /// Набор дешевеет не там, где кажется: почти всё время съедают два-три
    /// класса (кодирование GIF, профилирование), а остальные семьдесят тестов
    /// идут фоном. Без этого списка «набор стал медленным» обсуждается на
    /// ощупь, и оптимизируют не тот класс.</summary>
    private static void WriteSlowest(XmlWriter w, ITestResultAdaptor root)
    {
        var byClass = new Dictionary<string, double>();
        CollectDurations(root, byClass);

        var top = new List<KeyValuePair<string, double>>(byClass);
        top.Sort((a, b) => b.Value.CompareTo(a.Value));

        w.WriteStartElement("slowest");
        for (int i = 0; i < Math.Min(5, top.Count); i++)
        {
            w.WriteStartElement("suite");
            w.WriteAttributeString("name", top[i].Key);
            w.WriteAttributeString("seconds", top[i].Value.ToString("F1", CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void CollectDurations(ITestResultAdaptor node, Dictionary<string, double> byClass)
    {
        if (node.HasChildren)
        {
            foreach (var child in node.Children) CollectDurations(child, byClass);
            return;
        }

        // FullName листа — "Class.Method" (или "Ns.Class.Method(args)"); класс
        // берём как всё до последней точки перед именем метода.
        string full = node.FullName ?? "";
        int paren = full.IndexOf('(');
        if (paren > 0) full = full.Substring(0, paren);
        int dot = full.LastIndexOf('.');
        string cls = dot > 0 ? full.Substring(0, dot) : full;

        byClass.TryGetValue(cls, out double sum);
        byClass[cls] = sum + node.Duration;
    }

    private static void WriteProblems(XmlWriter w, ITestResultAdaptor node)
    {
        // Именно HasChildren, а не `Children != null`: у листа коллекция пустая,
        // но НЕ null — по проверке на null рекурсия до самих тестов не доходила,
        // и отчёт получался с одними счётчиками, без единого упавшего теста.
        if (node.HasChildren)
        {
            foreach (var child in node.Children) WriteProblems(w, child);
            return;
        }

        // Лист дерева = один тест. Зелёные в отчёт не пишем: он нужен для
        // разбора падений, а не для протокола на 2000 строк.
        if (node.TestStatus == TestStatus.Passed) return;

        w.WriteStartElement("test-case");
        w.WriteAttributeString("fullname", node.FullName);
        w.WriteAttributeString("result", node.TestStatus.ToString());
        if (!string.IsNullOrEmpty(node.Message))
        {
            w.WriteStartElement("failure");
            w.WriteStartElement("message");
            w.WriteCData(node.Message);
            w.WriteEndElement();
            if (!string.IsNullOrEmpty(node.StackTrace))
            {
                w.WriteStartElement("stack-trace");
                w.WriteCData(node.StackTrace);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }
}
