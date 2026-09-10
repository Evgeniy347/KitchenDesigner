using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Хозяин уходит со сцены — его вырез уходит с ним, кем бы и каким
/// путём он ни был удалён.
///
/// Дефект, ради которого написан этот набор: удалили мойку — дыра в столешнице
/// осталась. Причина была в одной строке <c>DeleteCommand.Execute</c>: она
/// снимала врезку у ДВУХ типов, выписанных поимённо (<c>WindowElement</c> и
/// <c>DoorElement</c>), и ничего не знала про мойку с варочной. Ровно тот
/// класс, о котором предупреждает `agents/TEST-DESIGN.md` → «Сторож,
/// отбирающий по типу, слеп к соседям по объекту»: список типов молча
/// устаревает, а объект, удалённый через <c>SetActive(false)</c>, не зовёт ни
/// <c>OnDestroy</c>, ни <c>PrepareForDestruction</c>, так что убрать себя из
/// хозяина ему больше некому.
///
/// Поэтому список типов здесь не выписан: он берётся из
/// <c>EveryElementType.Makers</c>, то есть заводится настоящей фабрикой, и
/// новый тип попадает под проверку сам. Гостю, режущему хозяина, положено
/// объявить <c>ICutsItsHost</c> — по нему и только по нему сторож зовёт
/// «отпусти вырез» и «верни вырез».
///
/// Механизмов врезки в проекте ДВА, и они честно разные: мебель держит вырез
/// на стороне ГОСТЯ (<c>PartMount</c> регистрирует себя в
/// <c>KitchenElement.AttachedCutouts</c>), стена — на стороне ХОЗЯИНА
/// (<c>Wall.AttachedWindows</c>/<c>AttachedDoors</c>). Общего предка им не
/// придумано; общее у них одно правило и одна точка проверки —
/// <c>SceneMembership.Leave</c>/<c>Return</c>, через которую ходят
/// <c>DeleteCommand</c>, <c>CreateCommand</c> и отмена размещения.
///
/// Перепись дыр (<c>OpenHoles</c>) знает про оба механизма. Третий механизм
/// она бы не увидела — и поэтому сторож проверяется сторожем: тип, который
/// ОБЪЯВИЛ <c>ICutsItsHost</c>, обязан оказаться видимым переписи. Заведи
/// третий реестр вырезов — и этот набор покраснеет строкой «перепись его не
/// увидела» раньше, чем дыра осиротеет у пользователя.</summary>
public class CutoutOwnerGuardTests
{
    private static readonly Vector3 CountertopCentre = new Vector3(2f, 0f, 0f);
    private static readonly Vector3 OverTheCountertop = new Vector3(2f, 0.02f, 0f);
    private static readonly Vector3 InTheWall = new Vector3(0f, 1.2f, 0f);

    private ProjectLoadStateGuard? _globals;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _globals = ProjectLoadStateGuard.Capture();
        CommandStack.Clear();
        EveryElementType.ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        EveryElementType.ClearScene();
        _globals?.Restore();
        LogAssert.ignoreFailingMessages = false;
    }

    /// <summary>Сколько вырезов сейчас открыто во всей сцене, по ОБОИМ
    /// механизмам сразу. Читает те же поля, что читает боевой код при
    /// перестройке меша, — второе описание того же контура сошлось бы само с
    /// собой и не проверило бы ничего.</summary>
    private static int OpenHoles()
    {
        int holes = 0;
        foreach (var el in PartRegistry.GetAll())
        {
            if (el == null) continue;
            foreach (var cutout in el.AttachedCutouts)
                if (cutout is UnityEngine.Object o ? o != null : cutout != null) holes++;

            var wall = el.GetComponent<Wall>();
            if (wall == null) continue;
            holes += wall.AttachedWindows.Count + wall.AttachedDoors.Count;
        }
        return holes;
    }

    private static (GameObject wall, GameObject top) BuildHosts()
    {
        var wall = ElementFactory.CreateWall(new Vector3Int(3000, 2700, 100), "Стена", Vector3.zero);
        var top = ElementFactory.CreatePart(new Vector3Int(1200, 40, 600), "Столешница", CountertopCentre);
        return (wall, top);
    }

    /// <summary>Просим элемент сесть на место — НЕ спрашивая, объявил ли он
    /// <c>ICutsItsHost</c>. Ровно в этом была слепота прежней пробы: она
    /// начиналась с <c>if (el is not ICutsItsHost) return 0</c>, и новый тип,
    /// который режет хозяина, но забыл объявить интерфейс, считался «не
    /// режущим» — то есть самая вероятная ошибка оставляла сторожа зелёным.
    /// Теперь проба ходит общим путём: объявленному гостю — его же
    /// <c>RestoreHostCutout</c>, всем остальным — их собственный жест посадки
    /// (<c>SnapTo…</c>) плюс общий тик сцены, а судим по ХОЗЯИНУ: у кого
    /// выросла перепись дыр.</summary>
    private static void MakeItSettle(KitchenElement el)
    {
        if (el is ICutsItsHost guest)
        {
            guest.RestoreHostCutout();
            return;
        }

        foreach (var m in el.GetType().GetMethods(
                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (m.GetParameters().Length != 0 || m.ReturnType != typeof(void)) continue;
            if (!m.Name.StartsWith("SnapTo", System.StringComparison.Ordinal)) continue;
            // Жест чужого типа может и упасть на стенде — судим всё равно по
            // хозяину: перепись дыр читается ниже независимо от исхода.
            try { m.Invoke(el, null); }
            catch (System.Reflection.TargetInvocationException) { }
        }
        SceneChangeTracker.Poll();
    }

    /// <summary>Ставим элемент туда, где он мог бы врезаться. Сначала над
    /// столешницей, потом — если там ничего не прорезалось — в стену.</summary>
    private static int TryToOpenAHole(KitchenElement el)
    {
        el.transform.position = OverTheCountertop;
        MakeItSettle(el);
        if (OpenHoles() > 0) return OpenHoles();

        el.transform.position = InTheWall;
        MakeItSettle(el);
        return OpenHoles();
    }

    [Test]
    public void EveryTypeThatCutsItsHost_TakesTheHoleWithItWhenDeleted_AndBringsItBackOnUndo()
    {
        var offenders = new List<string>();
        int typesThatCut = 0;

        foreach (var (type, _) in EveryElementType.Makers)
        {
            EveryElementType.ClearScene();
            CommandStack.Clear();
            BuildHosts();

            var el = EveryElementType.Spawn(type, "Сторож " + type.Name);
            int opened = TryToOpenAHole(el);

            if (opened == 0)
            {
                if (el is ICutsItsHost)
                    offenders.Add($"{type.Name}: объявил ICutsItsHost, но перепись дыр его не увидела — "
                        + "механизм врезки новый, а OpenHoles о нём не знает");
                continue;
            }

            typesThatCut++;

            if (el is not ICutsItsHost)
            {
                offenders.Add($"{type.Name}: прорезал хозяина, но НЕ объявил ICutsItsHost — "
                    + "SceneMembership.Leave/Return его не позовут, и дыра переживёт хозяина");
                continue;
            }

            CommandStack.Execute(new DeleteCommand(el.gameObject));
            int afterDelete = OpenHoles();
            if (afterDelete != 0)
                offenders.Add($"{type.Name}: удалили хозяина выреза, а в сцене осталось {afterDelete} "
                    + "открытых вырезов — дыра пережила того, кто её прорезал");

            CommandStack.Undo();
            int afterUndo = OpenHoles();
            if (afterUndo != opened)
                offenders.Add($"{type.Name}: Ctrl+Z вернул хозяина, но вырезов стало {afterUndo} "
                    + $"вместо {opened} — обратный путь не восстановил дыру");
        }

        Assert.Greater(typesThatCut, 0,
            "ни один тип не прорезал хозяина — сторож перестал что-либо проверять "
            + "(сцена-стенд или способ прилипания разъехались с боевым кодом)");

        Assert.IsEmpty(offenders,
            "Вырез обязан уходить вместе с тем, кто его прорезал:\n" + string.Join("\n", offenders)
            + "\n\nЛечится не списком типов в DeleteCommand, а объявлением ICutsItsHost у гостя: "
            + "SceneMembership.Leave/Return зовут его сами.");
    }

    [Test]
    public void DeletingASink_ClosesTheCountertop_AndUndoOpensItAgain()
    {
        var (_, topGo) = BuildHosts();
        var top = topGo.GetComponent<KitchenElement>();

        var sinkGo = ElementFactory.CreateSink("Мойка", OverTheCountertop);
        var sink = sinkGo.GetComponent<SinkElement>();
        sink.SnapToPart();
        Assert.AreEqual(1, top.AttachedCutouts.Count, "мойка врезалась в столешницу");
        int verticesWithHole = top.GetComponent<MeshFilter>().sharedMesh.vertexCount;
        Assert.Greater(verticesWithHole, 24, "в столешнице появился проём, а не простая коробка");

        CommandStack.Execute(new DeleteCommand(sinkGo));

        Assert.AreEqual(0, top.AttachedCutouts.Count,
            "мойку удалили — столешница обязана зарасти: SetActive(false) не зовёт OnDestroy, "
            + "снять врезку больше некому");
        Assert.AreEqual(24, top.GetComponent<MeshFilter>().sharedMesh.vertexCount,
            "меш столешницы снова простая коробка");

        CommandStack.Undo();

        Assert.AreEqual(1, top.AttachedCutouts.Count, "Ctrl+Z вернул мойку — вырез открылся снова");
        Assert.AreEqual(verticesWithHole, top.GetComponent<MeshFilter>().sharedMesh.vertexCount,
            "и ровно тот же проём, что был до удаления");
        Assert.AreEqual(OverTheCountertop.y, sink.transform.position.y, 1e-4f,
            "мойка села на прежнее место");
    }

    [Test]
    public void DeletingAWindow_ClosesTheWall_AndUndoOpensItAgain()
    {
        var (wallGo, _) = BuildHosts();
        var wall = wallGo.GetComponent<Wall>();

        var windowGo = ElementFactory.CreateWindow(new Vector3Int(800, 1200, 100), "Окно", InTheWall);
        var window = windowGo.GetComponent<WindowElement>();
        window.AttachToWall(wall);
        Assert.IsTrue(wall.HasWindow(window), "окно врезалось в стену");

        CommandStack.Execute(new DeleteCommand(windowGo));

        Assert.IsFalse(wall.HasWindow(window), "окно удалили — проём в стене обязан закрыться");
        Assert.AreEqual(0, OpenHoles(), "и перепись дыр это видит");

        CommandStack.Undo();

        Assert.IsTrue(wall.HasWindow(window), "Ctrl+Z вернул окно — проём открылся снова");
    }

    /// <summary>F1. Удалили ХОЗЯИНА первым, потом гостя. <c>PartMount.Detach</c>
    /// искала хозяина только по имени через <c>PartRegistry.All</c>, а
    /// <c>SceneMembership.Leave</c> к этому моменту уже сняла столешницу с
    /// учёта — хозяин не находился, <c>UnregisterCutout</c> никто не звал, и
    /// после Ctrl+Z столешница возвращалась С ДЫРОЙ И БЕЗ МОЙКИ. Порядок в
    /// композитной команде решал исход.</summary>
    [Test]
    public void DeletingTheHostFirst_ThenTheSink_StillClosesTheHole_AndUndoBringsBothBack()
    {
        var (_, topGo) = BuildHosts();
        var top = topGo.GetComponent<KitchenElement>();

        var sinkGo = ElementFactory.CreateSink("Мойка", OverTheCountertop);
        var sink = sinkGo.GetComponent<SinkElement>();
        sink.SnapToPart();
        Assert.AreEqual(1, top.AttachedCutouts.Count, "мойка врезалась в столешницу");
        int verticesWithHole = top.GetComponent<MeshFilter>().sharedMesh.vertexCount;

        var delete = new CompositeCommand("удалить столешницу и мойку", new List<IUndoCommand>
        {
            new DeleteCommand(topGo),
            new DeleteCommand(sinkGo),
        });
        CommandStack.Execute(delete);

        Assert.AreEqual(0, top.AttachedCutouts.Count,
            "хозяина сняли с учёта РАНЬШЕ гостя — но вырез всё равно обязан закрыться: "
            + "искать хозяина только по имени в реестре уже поздно");

        CommandStack.Undo();

        Assert.AreEqual(1, top.AttachedCutouts.Count,
            "Ctrl+Z вернул обоих — мойка обязана снова сидеть в столешнице");
        Assert.AreEqual(verticesWithHole, top.GetComponent<MeshFilter>().sharedMesh.vertexCount,
            "и проём тот же, что был до удаления");
    }

    /// <summary>F5. Отмена удаления обязана ВОССТАНОВИТЬ посадку, а не вывести
    /// её заново. Прежде <c>Return</c> звал <c>SnapToPart</c>, тот шёл в
    /// <c>FindCatchingPart</c> и <c>CaptureCatch</c> ПЕРЕЗАПИСЫВАЛ смещения,
    /// пересчитав их из мирового положения: Ctrl+Z мог пересадить мойку на
    /// соседнюю доску или сдвинуть её. Число вершин и <c>position.y</c> этого
    /// не видят — поэтому проверяем имя хозяина и оба смещения.</summary>
    [Test]
    public void UndoingASinkDeletion_RestoresTheSameSeat_NotAFreshlyDerivedOne()
    {
        var (_, topGo) = BuildHosts();
        var top = topGo.GetComponent<KitchenElement>();

        var sinkGo = ElementFactory.CreateSink("Мойка", OverTheCountertop);
        var sink = sinkGo.GetComponent<SinkElement>();
        sink.SnapToPart();
        Assert.AreEqual(1, top.AttachedCutouts.Count, "мойка врезалась в столешницу");

        sink.OffsetXMM = 170;
        sink.OffsetYMM = 25;
        sink.SnapToPart();
        string hostBefore = sink.AttachedPartName;
        int offXBefore = sink.OffsetXMM;
        int offYBefore = sink.OffsetYMM;
        Assert.AreEqual(top.PartName, hostBefore, "мойка сидит именно на этой столешнице");
        Assert.AreEqual(170, offXBefore, "предусловие: посадка НЕ по центру доски");

        // Столешницу подвинули, пока мойка спала: сохранённая посадка (170, 25)
        // и мировое положение мойки разошлись на 300 мм. Именно на таком
        // расхождении видно, что делает Return — восстанавливает посадку или
        // выводит её заново из позиции.
        Vector3 sinkBefore = sink.transform.position;
        top.transform.position += new Vector3(0.3f, 0f, 0f);

        CommandStack.Execute(new DeleteCommand(sinkGo));
        CommandStack.Undo();

        Assert.AreEqual(hostBefore, sink.AttachedPartName,
            "Ctrl+Z обязан вернуть мойку тому же хозяину");
        Assert.AreEqual(offXBefore, sink.OffsetXMM,
            "и дословно то же смещение по X: SnapToPart → FindCatchingPart → CaptureCatch "
            + "пересчитывает смещения из мирового положения и выдал бы -130. Восстановление — "
            + "это восстановление, а не жест (conventions/SERIALIZATION.md про загрузочный проход)");
        Assert.AreEqual(offYBefore, sink.OffsetYMM, "и по Y");
        Assert.AreEqual(sinkBefore.x + 0.3f, sink.transform.position.x, 1e-3f,
            "и посадка применена: мойка уехала вместе со своей столешницей, ровно на её 300 мм");
    }

    [Test]
    public void UndoingTheCreationOfACooktop_ClosesTheCountertop()
    {
        var (_, topGo) = BuildHosts();
        var top = topGo.GetComponent<KitchenElement>();

        var cooktopGo = ElementFactory.CreateCooktop("Варочная", OverTheCountertop);
        var cooktop = cooktopGo.GetComponent<CooktopElement>();
        cooktop.SnapToPart();
        Assert.AreEqual(1, top.AttachedCutouts.Count, "варочная врезалась в столешницу");

        CommandStack.Execute(new CreateCommand(cooktopGo));
        CommandStack.Undo();

        Assert.AreEqual(0, top.AttachedCutouts.Count,
            "отмена создания — это тоже уход со сцены: вырез уходит вместе с варочной");

        CommandStack.Redo();

        Assert.AreEqual(1, top.AttachedCutouts.Count, "повтор создания вернул и варочную, и её вырез");
    }
}
