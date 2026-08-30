using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Роль соседа для врезной техники: кто мешает коробу выреза, а кто только
/// служит магнитом для его кромки.
///
/// Раньше на этот вопрос отвечали ТРИ лестницы `is XxxElement` — две в
/// варочной (IsObstacle/IsSnapTarget) и одна в мойке. Они уже разошлись:
/// варочная считала, что мойка и другая варочная помехой не являются, а мойка
/// про варочную не знала вовсе — та блокировала ей посадку. Ровно тот случай,
/// про который CONVENTIONS.md → «Element type checks live in ONE place per
/// layer» говорит «они не «могут» разойтись, они расходятся».
///
/// Теперь роль объявляет САМ элемент (<see cref="KitchenElement.CutoutRole"/>),
/// а врезка её только читает.
/// </summary>
public class CutoutNeighbourRoleTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            UnityEngine.Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    private T Spawn<T>() where T : KitchenElement
    {
        var go = new GameObject(typeof(T).Name);
        _spawned.Add(go);
        var el = go.AddComponent<T>();
        el.PartName = typeof(T).Name;
        return el;
    }

    [Test]
    public void PlainPart_IsCarcass_BlocksAndAlignsTheCutout()
    {
        var part = Spawn<KitchenElement>();
        Assert.AreEqual(CutoutNeighbourRole.Carcass, part.CutoutRole,
            "боковина, перегородка, полка — конструктив: короб выреза в них упирается");
        Assert.IsTrue(part.BlocksCutout);
        Assert.IsTrue(part.AlignsCutout);
    }

    [Test]
    public void BasePlate_IsNoNeighbourAtAll()
    {
        var plate = BasePlate.Create();
        _spawned.Add(plate.gameObject);
        Assert.AreEqual(CutoutNeighbourRole.None, plate.Element.CutoutRole,
            "подложка — якорь сцены под всей кухней: она перекрывает любой вырез "
            + "и, посчитай её помехой, врезка была бы невозможна нигде");
    }

    [Test]
    public void Facade_AlignsTheCutout_ButNeverBlocksIt()
    {
        var facade = Spawn<FacadeElement>();
        Assert.IsFalse(facade.BlocksCutout,
            "фасад стоит ПЕРЕД коробом, короб уходит за него: фасад тумбы, "
            + "пересекающий проём на 8 мм, запрещал врезку целиком (реальный проект)");
        Assert.IsTrue(facade.AlignsCutout,
            "по плоскости фасада вырез выравнивают вручную — он магнит, а не помеха");
    }

    [Test]
    public void Drawer_Door_AndPanel_AlignTheCutout_ButNeverBlockIt()
    {
        foreach (var el in new KitchenElement[]
                 { Spawn<DrawerElement>(), Spawn<DoorElement>(), Spawn<PanelElement>() })
        {
            Assert.IsFalse(el.BlocksCutout,
                el.DisplayTypeName + ": выдвигается или висит снаружи короба — не конструктив");
            Assert.IsTrue(el.AlignsCutout,
                el.DisplayTypeName + ": по его плоскости технику выравнивают");
        }
    }

    [Test]
    public void Window_Light_Floor_AreNeitherObstacleNorMagnet()
    {
        foreach (var el in new KitchenElement[]
                 { Spawn<WindowElement>(), Spawn<LightSourceElement>(), Spawn<FloorElement>() })
            Assert.AreEqual(CutoutNeighbourRole.None, el.CutoutRole,
                el.DisplayTypeName + " не участвует во врезке ни одной стороной");
    }

    /// <summary>Симметрия, которой у двух лестниц не было: варочная исключала
    /// мойку из помех, а мойка варочную — нет.</summary>
    [Test]
    public void SinkAndCooktop_DoNotBlockEachOthersCutout_InBothDirections()
    {
        var sink = Spawn<SinkElement>();
        var cooktop = Spawn<CooktopElement>();

        Assert.IsFalse(sink.BlocksCutout,
            "мойка — такая же врезная техника, а не конструктив");
        Assert.IsFalse(cooktop.BlocksCutout,
            "варочная блокировала посадку мойки, хотя мойка варочную — нет: "
            + "две лестницы `is XxxElement` разошлись, роль спрашивается в одном месте");
        Assert.IsFalse(sink.AlignsCutout);
        Assert.IsFalse(cooktop.AlignsCutout);
    }

    /// <summary>Приборы, которые в нишу действительно встают, конструктивом
    /// остаются: короб выреза обязан о них спотыкаться.</summary>
    [Test]
    public void OvenAndDishwasher_StillBlockTheCutout()
    {
        Assert.IsTrue(Spawn<OvenElement>().BlocksCutout, "духовка занимает нишу целиком");
        Assert.IsTrue(Spawn<DishwasherElement>().BlocksCutout, "посудомойка занимает нишу целиком");
    }

    // ── Сторож: врезка больше не спрашивает тип ───────────────────────

    private static readonly (string pattern, string why)[] Banned =
    {
        (@"\bis\s+(?!KitchenElement\b)[A-Z]\w*Element\b", "лестница по типу: спросите CutoutRole"),
        (@"\bas\s+(?!KitchenElement\b)[A-Z]\w*Element\b", "приведение к конкретному типу — та же лестница"),
        (@"GetComponent\s*<\s*(?!KitchenElement\b)[A-Z]\w*Element\s*>", "тот же вопрос, заданный сцене"),
    };

    /// <summary>Файлы врезки: они читают роль соседа и не имеют права спрашивать
    /// его класс.</summary>
    private static readonly string[] CutoutSources = { "CooktopElement.cs", "SinkElement.cs" };

    private static string ElementsSourceDir()
    {
        var roots = new[]
        {
            Path.GetDirectoryName(typeof(CutoutNeighbourRoleTests).Assembly.Location),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var dir = new DirectoryInfo(root);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Assets", "Scripts", "Core", "Elements");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Не найден Assets/Scripts/Core/Elements ни от одной из точек: " + string.Join(", ", roots));
    }

    [Test]
    public void CutoutSources_NeverAskForAnElementType()
    {
        var dir = ElementsSourceDir();
        var violations = new List<string>();

        foreach (var name in CutoutSources)
        {
            var path = Path.Combine(dir, name);
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*"))
                    continue;
                foreach (var (pattern, why) in Banned)
                    if (Regex.IsMatch(lines[i], pattern))
                        violations.Add($"{name}:{i + 1} — {why}\n    {trimmed}");
            }
        }

        Assert.IsEmpty(violations,
            "Роль соседа объявляет сам элемент (CutoutRole), врезка её только читает. Найдено:\n"
            + string.Join("\n", violations));
    }

    /// <summary>Скан по несуществующему пути прошёл бы зелёным, не проверив
    /// ничего, — та же ловушка, что у UiElementTypeLadderTests.</summary>
    [Test]
    public void CutoutSources_AreActuallyOnDisk()
    {
        var dir = ElementsSourceDir();
        foreach (var name in CutoutSources)
            Assert.IsTrue(File.Exists(Path.Combine(dir, name)),
                "врезной файл " + name + " не найден — сторож проверял бы пустоту");
    }
}
