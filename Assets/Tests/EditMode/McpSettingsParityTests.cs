using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;
using KitchenDesigner.Tests.Geometry;

/// <summary>Настройки — вторая поверхность, где панель и агент расходились
/// молча. До этого сторожа set_setting знал ТРИ ключа, а get_settings отдавал
/// тринадцать значений: агент видел порог привязки, шаг сетки и интервал
/// автосохранения, но изменить их не мог ничем. Отказа он при этом не получал —
/// он просто не находил ключа, потому что список ключей был написан руками в
/// трёх местах сразу: в перечислении контракта, в прозе описания инструмента и
/// в ветвлении обработчика. Три копии одного списка, ни одна не выведена из
/// других.
///
/// Копия теперь одна — <see cref="SettingKeys"/>, откуда и чтение, и запись
/// берут ключи сами. Этот тест сторожит две вещи: что список контракта и проза
/// описания не разъехались с этой таблицей, и что настройка, которую человек
/// крутит в панели, доступна агенту.
///
/// Соответствие «ключ → свойство» нигде не записано и записано быть не должно:
/// это было бы ЧЕТВЁРТЫМ списком имён. Поэтому оно снимается опытом — ключ
/// посылается обработчику, и смотрится, какое свойство KitchenSettings от этого
/// изменилось.</summary>
public class McpSettingsParityTests
{
    /// <summary>Настройки, которые панель правит, а агент — нет, каждая с
    /// причиной. Список проверяется на гниль
    /// <see cref="EveryExemption_StillNamesALiveSetting_AndAStillMissingOne"/>.</summary>
    private static readonly (string property, string why)[] PanelOnly =
    {
        ("Photo*",
         "фоторежим целиком: качество, тени, сглаживание, экспозиция, свечение, виньетка. "
         + "Это настройки КАДРА, а не проекта — они не меняют ни одной детали и ни одного "
         + "размера, и агенту нечего ими добиваться. Решение записано в описании самого "
         + "инструмента set_setting и продублировано тестом "
         + "McpCommandHandlerTests.GetSettings_CarriesTheProjectSettings_ButNoViewOrPhotoSettings"),
        ("SpatialGrid",
         "пространственный индекс — переключатель для отладки производительности, а не "
         + "настройка проекта. Держится вне ответа тем же тестом GetSettings_Carries...: "
         + "решение принято там, и снимать его нужно там же, а не здесь"),
    };

    private KitchenSettings? _saved;
    private McpCommandHandler? _handler;
    private bool _savedVerbose;

    [SetUp]
    public void Setup()
    {
        Assume.That(KitchenSettings.Instance, Is.Not.Null, "нужен Resources/KitchenSettings");
        _saved = new KitchenSettings();
        _saved.ApplyFrom(KitchenSettings.Instance.ToData());
        _savedVerbose = SnapSystem.VerboseLog;
        _handler = new McpCommandHandler();
    }

    [TearDown]
    public void Teardown()
    {
        if (_saved != null) KitchenSettings.Instance.ApplyFrom(_saved.ToData());
        SnapSystem.VerboseLog = _savedVerbose;
    }

    // ---------- свойства модели ----------

    private static IEnumerable<PropertyInfo> SettingProperties() =>
        typeof(KitchenSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

    private static Dictionary<string, object?> Snapshot()
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in SettingProperties()) map[p.Name] = p.GetValue(KitchenSettings.Instance);
        map["SnapVerboseLog"] = SnapSystem.VerboseLog;
        return map;
    }

    private static IEnumerable<string> Changed(
        Dictionary<string, object?> before, Dictionary<string, object?> after) =>
        before.Keys.Where(k => !Equals(before[k], after[k]));

    // ---------- поверхность панели ----------

    /// <summary>Что вкладки настроек пишут в модель. Разбор исходника, а не
    /// опыт: строку панели можно дёрнуть только собрав всю панель на канве, а
    /// вкладок шесть и виджетов под сотню — опыт стоил бы минуту на каждый
    /// прогон ради списка, который читается регулярным выражением надёжно:
    /// имена свойств KitchenSettings достаточно своеобразны, чтобы совпасть
    /// случайно.</summary>
    private static SortedSet<string> PanelWrites()
    {
        var known = new HashSet<string>(SettingProperties().Select(p => p.Name), StringComparer.Ordinal);
        var assign = new Regex(@"\.([A-Z][A-Za-z0-9]*)\s*=[^=]", RegexOptions.Compiled);
        var found = new SortedSet<string>(StringComparer.Ordinal);

        var dir = RepoPaths.Subdir("Assets", "Scripts", "Core", "UI");
        foreach (var file in Directory.GetFiles(dir, "Settings*Tab.cs", SearchOption.TopDirectoryOnly))
        {
            var text = SourceLines.CodeOnly(File.ReadAllText(file));
            foreach (Match m in assign.Matches(text))
                if (known.Contains(m.Groups[1].Value)) found.Add(m.Groups[1].Value);
        }

        return found;
    }

    // ---------- поверхность MCP ----------

    private bool SetSetting(string wire, JToken value, bool numeric)
    {
        var p = new JObject { ["name"] = wire };
        p[numeric ? "number" : "value"] = value;
        var request = new McpRequest { id = "settings", method = "set_setting", Params = p };
        return _handler!.Handle(request).type == "result";
    }

    /// <summary>Какое свойство меняет каждый ключ — по факту, а не по имени.
    /// Пробное значение подбирается так, чтобы гарантированно отличаться от
    /// текущего: у флага это его отрицание, у числа — сдвиг, переживающий любой
    /// клампинг сеттера.</summary>
    private HashSet<string> McpWrites()
    {
        var written = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in SettingKeys.All)
        {
            var before = Snapshot();

            bool ok;
            if (key.IsNumber)
            {
                var current = Convert.ToSingle(key.Read());
                ok = SetSetting(key.Wire, current + 7f, numeric: true)
                     || SetSetting(key.Wire, current - 7f, numeric: true);
            }
            else
            {
                ok = SetSetting(key.Wire, !Convert.ToBoolean(key.Read()), numeric: false);
            }

            Assert.IsTrue(ok,
                "объявленный ключ настройки отвергнут собственным обработчиком: " + key.Wire);

            foreach (var property in Changed(before, Snapshot())) written.Add(property);
        }

        return written;
    }

    private static bool Exempt(string property) =>
        PanelOnly.Any(e => e.property.EndsWith("*", StringComparison.Ordinal)
            ? property.StartsWith(e.property.TrimEnd('*'), StringComparison.Ordinal)
            : e.property == property);

    // ---------- сверка ----------

    [Test]
    public void EverySettingThePanelCanChange_IsAlsoSettableThroughMcp()
    {
        var missing = PanelWrites().Except(McpWrites())
            .Where(p => !Exempt(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.IsEmpty(missing,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: человек меняет эту настройку в панели настроек, а агент через "
            + "set_setting — нет. Ключа для неё просто не существует, и агент об этом не "
            + "узнает: он не получит отказа, он не найдёт, что послать. "
            + "ЧТО СДЕЛАТЬ: добавить строку в таблицу SettingKeys.All и тот же ключ в "
            + "перечисление ParamsSetSetting.name — чтение, запись и схема возьмут его "
            + "оттуда сами; проверить, что описание инструкции в McpToolRegistry называет "
            + "новый ключ. Если настройка намеренно не отдаётся агенту — запись с причиной "
            + "в PanelOnly. "
            + McpUiParityRule.SettingAddresses
            + "Только в панели: " + string.Join(", ", missing));
    }

    [Test]
    public void EverySettingMcpCanChange_IsAlsoInThePanel_OrIsNotASetting()
    {
        var panel = PanelWrites();
        var extra = McpWrites()
            .Where(p => !panel.Contains(p) && p != "SnapVerboseLog")
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.IsEmpty(extra,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: агент меняет эту настройку, а в панели её нет — человек не "
            + "увидит, откуда взялось поведение, которого он не задавал, и не сможет вернуть "
            + "его обратно. "
            + "ЧТО СДЕЛАТЬ: добавить строку в подходящую вкладку настроек. "
            + McpUiParityRule.SettingAddresses
            + "Только в MCP: " + string.Join(", ", extra));
    }

    /// <summary>Список ключей жил в трёх местах и разъезжался. Схему читает
    /// клиентская библиотека, таблицу — обработчик: ключ, забытый в схеме,
    /// агент не попробует, а ключ, обещанный схемой сверх таблицы, получит
    /// отказ на то, что сам же контракт и предложил.</summary>
    [Test]
    public void TheContractEnum_ListsExactlyTheKeys_TheHandlerAccepts()
    {
        var declared = McpContractEnums.Of(typeof(ParamsSetSetting), nameof(ParamsSetSetting.name));
        var table = SettingKeys.All.Select(k => k.Wire).ToList();

        Assert.IsNotEmpty(declared, "контракт не объявил ни одного ключа — сторож ослеп");

        CollectionAssert.AreEquivalent(table, declared,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: перечисление ParamsSetSetting.name и таблица SettingKeys.All "
            + "обещают РАЗНЫЕ наборы ключей. "
            + "ЧТО СДЕЛАТЬ: привести перечисление к таблице — таблица главная, из неё живут "
            + "и set_setting, и get_settings. "
            + "только в схеме: [" + string.Join(", ", declared.Except(table)) + "]; "
            + "только в таблице: [" + string.Join(", ", table.Except(declared)) + "]");
    }

    /// <summary>Тот же список ещё раз, прозой в описании инструмента. Схему
    /// читает библиотека, прозу — агент, и расходятся они молча.</summary>
    [Test]
    public void TheToolDescription_NamesEveryKey_TheSchemaDeclares()
    {
        var tool = McpToolRegistry.Tools.First(t => t.Name == "set_setting");
        var missing = SettingKeys.All
            .Where(k => !tool.Description.Contains(k.Wire, StringComparison.Ordinal))
            .Select(k => k.Wire)
            .ToList();

        Assert.IsEmpty(missing,
            "описание set_setting не называет ключ, который контракт предлагает: схему читает "
            + "клиентская библиотека, а прозу — агент, и ключ, забытый в прозе, он не "
            + "попробует. Дописать в описание инструмента: " + string.Join(", ", missing));
    }

    /// <summary>Дефект, ради которого таблица и заведена: агент ВИДЕЛ порог
    /// привязки, шаг сетки и интервал автосохранения в ответе get_settings, но
    /// изменить их не мог — set_setting знал три ключа из тринадцати.</summary>
    [Test]
    public void EveryValueGetSettingsReports_CanAlsoBeSet()
    {
        var resp = _handler!.Handle(new McpRequest
        {
            id = "settings", method = "get_settings", Params = new JObject()
        });
        Assert.AreEqual("result", resp.type, "get_settings отказал — сторожить нечего");

        var reported = JObject.FromObject(resp.data!).Properties().Select(p => p.Name).ToList();
        Assert.GreaterOrEqual(reported.Count, 10,
            "ответ get_settings обязан нести больше десятка значений — иначе сверка ниже "
            + "зеленеет на пустом");

        var settable = SettingKeys.All.Select(k => k.Field).ToList();
        var readOnly = reported.Except(settable).ToList();

        Assert.IsEmpty(readOnly,
            McpUiParityRule.Rule
            + "ЧТО СЛОМАНО: get_settings показывает агенту значение, которое set_setting "
            + "изменить не может. Агент видит настройку, пробует её задать и не находит "
            + "ключа — это худший вид расхождения, потому что ответ инструмента сам и обещал "
            + "ему эту настройку. "
            + "ЧТО СДЕЛАТЬ: завести ключ в SettingKeys.All. "
            + McpUiParityRule.SettingAddresses
            + "Только читается: " + string.Join(", ", readOnly));
    }

    /// <summary>Сторож сторожа: обе поверхности снимаются кодом, который может
    /// онеметь. Разбор вкладок может смотреть не в тот каталог, опыт — падать
    /// на каждом ключе, и тогда обе стороны пусты, разница пуста, тест зелен.</summary>
    [Test]
    public void TheProbe_ActuallySeesBothSurfaces()
    {
        var panel = PanelWrites();
        Assert.GreaterOrEqual(panel.Count, 20,
            "вкладки настроек правят не меньше двух десятков свойств; меньше — значит разбор "
            + "смотрит не в тот каталог или регулярное выражение перестало ловить");
        CollectionAssert.Contains(panel, nameof(KitchenSettings.GridStep),
            "шаг сетки стоит на вкладке проекта — если разбор его не видит, он не видит ничего");
        CollectionAssert.Contains(panel, nameof(KitchenSettings.PhotoBloomPct),
            "сила свечения стоит на вкладке света — положительный контроль на то, что "
            + "разбирается не одна вкладка, а все");

        var mcp = McpWrites();
        CollectionAssert.Contains(mcp, nameof(KitchenSettings.SnapThreshold),
            "порог привязки — та самая настройка, которую агент раньше видел и не мог "
            + "изменить; если опыт её не увидел, он не доехал до обработчика");
        CollectionAssert.Contains(mcp, nameof(KitchenSettings.GridEnabled),
            "сетка включалась агентом и до таблицы — этот ключ обязан работать всегда");
        Assert.GreaterOrEqual(mcp.Count, 10,
            "тринадцать ключей обязаны менять не меньше десяти разных свойств");
    }

    [Test]
    public void EveryExemption_StillNamesALiveSetting_AndAStillMissingOne()
    {
        var known = SettingProperties().Select(p => p.Name).ToList();
        var mcp = McpWrites();

        foreach (var (property, why) in PanelOnly)
        {
            Assert.IsNotEmpty(why,
                "исключение без причины через полгода не отличить от недосмотра: " + property);

            var matching = property.EndsWith("*", StringComparison.Ordinal)
                ? known.Where(k => k.StartsWith(property.TrimEnd('*'), StringComparison.Ordinal)).ToList()
                : known.Where(k => k == property).ToList();

            CollectionAssert.IsNotEmpty(matching,
                "исключение числится за настройкой, которой больше нет: " + property);

            foreach (var name in matching)
                Assert.IsFalse(mcp.Contains(name),
                    "настройка уже правится агентом, а запись об исключении осталась и теперь "
                    + "молча прикроет следующую забытую — убрать из PanelOnly: " + name);
        }
    }
}
