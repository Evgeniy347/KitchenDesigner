using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Настройки кухни хранятся ЧЕТЫРЬМЯ параллельными списками одних и тех же
/// полей: сами [SerializeField], ResetToDefaults, ToData и ApplyFrom. Списки
/// расходятся молча, и каждое направление — свой дефект: поле, попавшее в ToData
/// и забытое в ApplyFrom, сохраняется и не восстанавливается (потеря настройки
/// пользователя); поле, забытое в ResetToDefaults, протекает из теста в тест
/// через глобальный ScriptableObject.
///
/// Здесь эти списки сверяются между собой рефлексией по полям самого класса.
/// Рефлексия тут — не обход приватного API, а предмет проверки: соответствие
/// «_поле» ↔ «поле в KitchenSettingsData» И ЕСТЬ контракт формата файла.
/// Поэтому набор рефлексии проверяется отдельным тестом — скан по исчезнувшему
/// имени проходил бы зелёным, ничего не проверяя.
///
/// Не сохраняется в файл только то, что перечислено в <see cref="NotPersisted"/>
/// с причиной. Активность фоторежима (PhotoMode.Active) полем настроек не
/// является намеренно: проект открывается в обычном рабочем режиме.
/// </summary>
public class KitchenSettingsContractTests
{
    private static readonly Dictionary<string, string> NotPersisted =
        new Dictionary<string, string>();

    private static readonly string[] LegacyDataOnlyFields =
    {
        "viewSchema",
        "edgeOutline", "wallsEnabled", "lowerNearWalls", "wallOutline",
        "hideOpeningsOnLoweredWalls", "objectsVisible", "hideLightSources",
    };

    private readonly List<KitchenSettings> _made = new List<KitchenSettings>();

    [TearDown]
    public void Teardown()
    {
        foreach (var s in _made)
            if (s != null) UnityEngine.Object.DestroyImmediate(s);
        _made.Clear();
    }

    private KitchenSettings NewSettings()
    {
        var s = ScriptableObject.CreateInstance<KitchenSettings>();
        _made.Add(s);
        return s;
    }

    private static FieldInfo[] SerializedFields() =>
        typeof(KitchenSettings)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(f => f.GetCustomAttribute<SerializeField>() != null)
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToArray();

    private static string SaveFieldName(FieldInfo f) =>
        f.Name == "_normalView" ? "viewNormal"
        : f.Name == "_roomView" ? "viewRoom"
        : char.ToLowerInvariant(f.Name[1]) + f.Name.Substring(2);

    private static void MutateEverySetting(KitchenSettings s)
    {
        foreach (var f in SerializedFields())
        {
            var value = f.GetValue(s);
            if (value is ViewPreset preset)
            {
                foreach (ViewField field in Enum.GetValues(typeof(ViewField)))
                    preset.Set(field, !preset.Get(field));
            }
            else if (value is bool flag) f.SetValue(s, !flag);
            else if (value is float number) f.SetValue(s, number * 0.5f);
            else if (f.FieldType.IsEnum)
                f.SetValue(s, Enum.ToObject(f.FieldType, Convert.ToInt32(value) - 1));
            else if (value is int whole) f.SetValue(s, whole - 1);
            else Assert.Fail($"поле {f.Name} типа {f.FieldType.Name} не умеет меняться — "
                + "допишите правило, иначе тест перестанет его проверять");
        }
    }

    private static Dictionary<string, string> Values(KitchenSettings s)
    {
        var map = new Dictionary<string, string>();
        foreach (var f in SerializedFields())
        {
            var value = f.GetValue(s);
            if (value is ViewPreset preset)
                foreach (ViewField field in Enum.GetValues(typeof(ViewField)))
                    map[f.Name + "." + field] = preset.Get(field).ToString();
            else
                map[f.Name] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
        }
        return map;
    }

    private static List<string> Differences(
        Dictionary<string, string> expected, Dictionary<string, string> actual)
    {
        return expected
            .Where(pair => !actual.TryGetValue(pair.Key, out var got) || got != pair.Value)
            .Select(pair => $"{pair.Key}: ждали {pair.Value}, получили "
                + (actual.TryGetValue(pair.Key, out var got) ? got : "<нет поля>"))
            .ToList();
    }

    [Test]
    public void TheScanner_ActuallyFindsTheSettingsFields()
    {
        var names = SerializedFields().Select(f => f.Name).ToList();

        Assert.Greater(names.Count, 30,
            "скан по пустому набору прошёл бы зелёным, ничего не проверив");
        CollectionAssert.Contains(names, "_gridStep", "базовое поле настроек должно попадать в скан");
        CollectionAssert.Contains(names, "_photoSSGI", "поздние поля фоторежима — тоже");
        CollectionAssert.Contains(names, "_normalView", "пресеты вида — тоже");
    }

    [Test]
    public void EverySerializedSetting_HasItsFieldInTheSaveFormat()
    {
        var missing = new List<string>();
        foreach (var f in SerializedFields())
        {
            if (NotPersisted.TryGetValue(f.Name, out var reason))
            {
                Assert.IsNotEmpty(reason, $"{f.Name} исключён из файла без причины");
                continue;
            }
            if (typeof(KitchenSettingsData).GetField(SaveFieldName(f)) == null)
                missing.Add($"{f.Name} -> {SaveFieldName(f)}");
        }

        Assert.IsEmpty(missing,
            "настройка есть в приложении, но её негде хранить — после перезапуска "
            + "пользователь получит её обратно из коробки: " + string.Join(", ", missing));
    }

    [Test]
    public void EveryFieldOfTheSaveFormat_BelongsToASetting_OrIsDeclaredLegacy()
    {
        var known = SerializedFields().Select(SaveFieldName)
            .Concat(LegacyDataOnlyFields)
            .ToHashSet(StringComparer.Ordinal);

        var orphans = typeof(KitchenSettingsData)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(f => !f.IsLiteral && !known.Contains(f.Name))
            .Select(f => f.Name)
            .ToList();

        Assert.IsEmpty(orphans,
            "поле формата, за которым не стоит ни одна настройка: пишется и никогда "
            + "не читается: " + string.Join(", ", orphans));
    }

    [Test]
    public void EverySetting_SurvivesToDataAndApplyFrom()
    {
        var settings = NewSettings();
        MutateEverySetting(settings);
        var expected = Values(settings);

        var data = settings.ToData();
        settings.ResetToDefaults();
        settings.ApplyFrom(data);

        var lost = Differences(expected, Values(settings));
        Assert.IsEmpty(lost,
            "настройка сохраняется, но не восстанавливается — это потеря настроек "
            + "пользователя: " + string.Join(" | ", lost));
    }

    [Test]
    public void ResetToDefaults_ReturnsEverySettingToItsInitializer()
    {
        var fresh = NewSettings();
        var expected = Values(fresh);

        var settings = NewSettings();
        MutateEverySetting(settings);
        settings.ResetToDefaults();

        var leftBehind = Differences(expected, Values(settings));
        Assert.IsEmpty(leftBehind,
            "инициализаторы полей срабатывают только при СОЗДАНИИ ассета, а Instance "
            + "грузится из Resources с уже сохранённым состоянием — забытое в "
            + "ResetToDefaults поле протекает из теста в тест: "
            + string.Join(" | ", leftBehind));
    }

    [Test]
    public void ToData_WritesTheLegacyFlatFlags_FromTheNormalPreset()
    {
        var settings = NewSettings();
        settings.NormalView.wallsEnabled = false;
        settings.NormalView.hideLightSources = true;
        settings.RoomView.wallsEnabled = true;
        settings.RoomView.hideLightSources = false;

        var data = settings.ToData();

        Assert.IsFalse(data.wallsEnabled,
            "плоские поля больше не читаются, но пишутся из «обычного» пресета: "
            + "сборка без пресетов обязана открыть проект осмысленно");
        Assert.IsTrue(data.hideLightSources, "и берутся именно из «обычного», а не из «помещения»");
    }

    [Test]
    public void PhotoExposure_IsStoredInHundredthsOfEV_AndStopsAtThreeStops()
    {
        var settings = NewSettings();

        settings.PhotoExposurePct = 1000;
        Assert.AreEqual(300, settings.PhotoExposurePct, "+3 EV — верхний предел экспозиции");

        settings.PhotoExposurePct = -1000;
        Assert.AreEqual(-300, settings.PhotoExposurePct, "−3 EV — нижний предел");
    }

    [Test]
    public void ViewPreset_EveryFieldIsAddressedSeparately_NoneAliasesAnother()
    {
        var fields = (ViewField[])Enum.GetValues(typeof(ViewField));
        foreach (var field in fields)
        {
            var preset = new ViewPreset();
            foreach (var other in fields) preset.Set(other, false);
            preset.Set(field, true);

            foreach (var other in fields)
                Assert.AreEqual(other == field, preset.Get(other),
                    $"{field} и {other} обязаны быть разными тумблерами: у Get/Set есть "
                    + "ветка по умолчанию, и новый член ViewField молча слипается с "
                    + "hideLightSources, если его туда не дописали");
        }
    }

    [Test]
    public void ViewPreset_CopyFrom_CarriesEveryField()
    {
        var source = new ViewPreset();
        foreach (ViewField field in Enum.GetValues(typeof(ViewField)))
            source.Set(field, !source.Get(field));

        var copy = new ViewPreset();
        copy.CopyFrom(source);

        foreach (ViewField field in Enum.GetValues(typeof(ViewField)))
            Assert.AreEqual(source.Get(field), copy.Get(field),
                $"{field} потерялся при копировании пресета");
    }
}
