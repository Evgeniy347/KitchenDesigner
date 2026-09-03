using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>
/// ElementData и ProjectData — это ФОРМАТ файла проекта: имя поля = ключ JSON.
///
/// JsonUtility НИКОГДА не оставляет вложенный сериализуемый объект нулевым: он
/// конструирует его даже тогда, когда в файле такого блока нет. Значит проверка
/// «if (data.settings != null)» в загрузчике не проверяет ничего — ветка берётся
/// всегда, а каждое поле внутри приезжает со значением по умолчанию для СВОЕГО
/// ТИПА. Единственное, что защищает старый проект, — инициализатор поля. Поле
/// без него — дефект, а не стиль: десять полей KitchenSettingsData без
/// инициализаторов молча выключали привязку, сетку и автосохранение у всех, кто
/// открывал проект, сохранённый до появления блока настроек.
///
/// Поэтому здесь рефлексией проверяется, что у КАЖДОГО поля формата есть
/// значение по умолчанию, а не type default: строки не null, массивы пустые, а
/// числа равны тем же константам, из которых берут дефолт сами элементы. Список
/// исключений (<see cref="IntentionallyNull"/>) требует причины на каждую
/// запись — иначе через полгода не отличить осознанное исключение от недосмотра.
/// </summary>
public class SaveFormatDefaultsTests
{
    private static readonly Dictionary<string, string> IntentionallyNull =
        new Dictionary<string, string>
        {
            ["ProjectData.basePlate"] =
                "отсутствие подложки — это null, а не пустой ElementData: basePlateValid отвечает за наличие",
            ["ProjectData.settings"] =
                "null отличает проект, сохранённый до появления блока настроек; сам блок восстанавливает KitchenSettings.ApplyFrom",
            ["KitchenSettingsData.viewNormal"] =
                "ракурс не обязателен: ToData всегда пишет клон, а ApplyFrom зовёт ViewPreset.CopyFrom, который на null не меняет пресет",
            ["KitchenSettingsData.viewRoom"] =
                "то же, что и viewNormal: отсутствие ракурса в файле оставляет текущий",
            ["KitchenSettingsData.viewPhoto"] =
                "то же, что и viewNormal: отсутствие ракурса в файле оставляет текущий",
        };

    private static readonly Dictionary<string, string> FixedLengthArrays =
        new Dictionary<string, string>
        {
            ["ElementData.dimensionsMM"] = "не список, а тройка мм: x, y, z",
            ["ElementData.position"] = "не список, а тройка метров: x, y, z",
            ["ElementData.rotation"] = "не список, а кватернион: x, y, z, w",
        };

    private static readonly Type[] SaveFormatTypes =
    {
        typeof(ElementData), typeof(ProjectData), typeof(GroupData), typeof(RoomData),
        typeof(FloorplanScopeData), typeof(WindowStateData),
        typeof(GrooveEntry), typeof(TextureOverlayEntry), typeof(KitchenSettingsData),
    };

    private static FieldInfo[] PersistedFields(Type t) =>
        t.GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(f => !f.IsInitOnly)
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToArray();

    [Test]
    public void EveryReferenceField_OfTheSaveFormat_HasAnInitializer()
    {
        var missing = new List<string>();
        foreach (var type in SaveFormatTypes)
        {
            var fresh = Activator.CreateInstance(type);
            foreach (var f in PersistedFields(type))
            {
                if (f.FieldType.IsValueType) continue;
                string key = type.Name + "." + f.Name;
                if (IntentionallyNull.ContainsKey(key)) continue;
                if (f.GetValue(fresh) == null) missing.Add(key);
            }
        }
        CollectionAssert.IsEmpty(missing,
            "поле формата сохранения без инициализатора: сериализатор Unity построит объект даже для "
            + "старого файла без этого ключа, и поле приедет нулевым — так уже терялись "
            + "настройки пользователя");
    }

    [Test]
    public void EveryArrayField_OfTheSaveFormat_StartsEmptyNotNull()
    {
        var wrong = new List<string>();
        foreach (var type in SaveFormatTypes)
        {
            var fresh = Activator.CreateInstance(type);
            foreach (var f in PersistedFields(type))
            {
                if (!f.FieldType.IsArray) continue;
                if (FixedLengthArrays.ContainsKey(type.Name + "." + f.Name)) continue;
                if (f.GetValue(fresh) is Array a && a.Length != 0)
                    wrong.Add(type.Name + "." + f.Name);
            }
        }
        CollectionAssert.IsEmpty(wrong,
            "массив формата обязан стартовать ПУСТЫМ: непустой дефолт дописал бы в проект "
            + "элементы, которых пользователь не создавал");
    }

    [Test]
    public void ExceptionLists_NameOnlyFieldsThatExist()
    {
        foreach (var key in IntentionallyNull.Keys.Concat(FixedLengthArrays.Keys))
        {
            var parts = key.Split('.');
            var type = SaveFormatTypes.FirstOrDefault(t => t.Name == parts[0]);
            Assert.IsNotNull(type, $"в списке исключений тип {parts[0]}, которого нет в формате");
            Assert.IsNotNull(type!.GetField(parts[1]),
                $"исключение {key} пережило своё поле и молча освободит следующее с таким именем");
        }
    }

    [Test]
    public void ElementData_CoordinateTuples_KeepTheirLength()
    {
        var d = new ElementData();
        Assert.AreEqual(3, d.dimensionsMM.Length, "размеры — ровно x, y, z в мм");
        Assert.AreEqual(3, d.position.Length, "позиция — ровно x, y, z в метрах");
        Assert.AreEqual(4, d.rotation.Length,
            "поворот — кватернион из четырёх компонент: ElementData.Rotation читает rotation[3] как w");
    }

    [Test]
    public void ScanSeesTheSaveFormat_NotAnEmptySet()
    {
        Assert.Greater(PersistedFields(typeof(ElementData)).Length, 90,
            "скан по пустому набору проходит зелёным и не проверяет ничего");
        CollectionAssert.Contains(PersistedFields(typeof(ElementData)).Select(f => f.Name).ToArray(),
            "lightTemperatureK", "скан обязан видеть поля ElementData поимённо");
    }

    [Test]
    public void ElementData_MaterialDefaults_AreTheCatalogDefaultId()
    {
        var d = new ElementData();
        Assert.AreEqual(AppConstants.DEFAULT_MATERIAL_ID, d.materialId);
        Assert.AreEqual(AppConstants.DEFAULT_MATERIAL_ID, d.legsMaterialId);
        Assert.AreEqual(AppConstants.DEFAULT_MATERIAL_ID, d.tabletopMaterialId);
        Assert.AreEqual(AppConstants.DEFAULT_MATERIAL_ID, new TextureOverlayEntry().materialId,
            "старый проект без materialId у накладки обязан получить серый декор, а не пустую строку");
    }

    [Test]
    public void ElementData_LightDefaults_ComeFromTheSameConstantsAsTheLamp()
    {
        var d = new ElementData();
        Assert.AreEqual(LampSpec.DEFAULT_TEMPERATURE_K, d.lightTemperatureK);
        Assert.AreEqual(LampSpec.DEFAULT_POWER_W, d.lightPowerW);
        Assert.AreEqual(LampSpec.DEFAULT_BEAM_DEG, d.lightBeamDeg);
        Assert.AreEqual(LampSpec.DEFAULT_RANGE_MIN_MM, d.lightRangeMinMM);
        Assert.AreEqual(LampSpec.DEFAULT_RANGE_MAX_MM, d.lightRangeMaxMM);
        Assert.AreEqual((int)LampSpec.DEFAULT_SHAPE, d.lightShape);
        Assert.AreEqual((int)LampSpec.DEFAULT_SHADOW, d.lightShadow);
    }

    [Test]
    public void ElementData_GeometryDefaults_ComeFromAppConstants()
    {
        var d = new ElementData();
        Assert.AreEqual(AppConstants.ASSEMBLED_DEFAULT_GROOVES, d.grooveCount);
        Assert.AreEqual(AppConstants.RADIAL_CORNER_RADIUS_DEFAULT, d.cornerRadius);
        Assert.AreEqual(AppConstants.EDGE_THICKNESS_DEFAULT_MM, d.edgeThicknessMM);
        Assert.IsTrue(d.movable, "деталь старого проекта без флага обязана остаться подвижной");
        Assert.IsTrue(d.edgeBanding, "кромка включена по умолчанию: старый проект её не выключал");
    }

    [Test]
    public void ProjectData_Version_IsTheSaveFormatVersion()
    {
        Assert.AreEqual(AppConstants.SAVE_FORMAT_VERSION, new ProjectData().version);
    }
}
