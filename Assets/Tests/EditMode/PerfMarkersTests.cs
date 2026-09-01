using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using KitchenDesigner.Core;

public class PerfMarkersTests
{
    private static FieldInfo[] MarkerFields() =>
        typeof(PerfMarkers)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(ProfilerMarker))
            .ToArray();

    [Test]
    public void TheScan_ActuallySeesTheMarkers()
    {
        Assert.Greater(MarkerFields().Length, 10,
            "рефлексия не нашла маркеров — тест ниже зеленел бы, ничего не проверив");
    }

    [Test]
    public void TouchingTheNameList_RegistersEveryMarker_SoRecordersAttachImmediately()
    {
        var names = PerfMarkers.NamesInDeclarationOrder;

        Assert.AreEqual(MarkerFields().Length, names.Count,
            "обращение к списку имён прогревает класс целиком: после него ВСЕ маркеры "
            + "созданы, и ProfilerRecorder цепляется к ним сразу, а не «когда-нибудь». "
            + "Расхождение значит, что список наполняется не каждым Reg() — а если "
            + "объявить его НИЖЕ маркеров, инициализаторы полей пойдут по порядку "
            + "объявления и Reg() упадёт в null ещё в статическом конструкторе");
    }

    [Test]
    public void EveryMarkerName_IsFilledIn_AndUnique()
    {
        var names = PerfMarkers.NamesInDeclarationOrder;
        CollectionAssert.AllItemsAreNotNull(names);
        Assert.IsFalse(names.Any(string.IsNullOrWhiteSpace), "пустое имя маркера");
        Assert.AreEqual(names.Count, names.Distinct().Count(),
            "два маркера с одним именем сольются в один рекордер, и один из замеров "
            + "молча пропадёт из дампа");
    }

    [Test]
    public void FirstAndLastName_PinTheDeclarationOrder()
    {
        var names = PerfMarkers.NamesInDeclarationOrder;
        Assert.AreEqual("CameraController.Update", names[0],
            "порядок имён — это порядок колонок CSV: перестановка объявлений сдвигает "
            + "колонки уже собранных файлов относительно новых");
        Assert.AreEqual("SidebarUI.Update", names[names.Count - 1]);
    }

    [Test]
    public void PerfMarkers_IsCompiledUnconditionally_BecauseTheMarkersSitInProductionCode()
    {
        var path = Path.Combine(Application.dataPath, "Scripts", "Core", "Diagnostics", "PerfMarkers.cs");
        Assert.IsTrue(File.Exists(path), "скан не видит файла — проверять было бы нечего");

        var source = File.ReadAllText(path);
        StringAssert.DoesNotContain("#if", source,
            "маркеры расставлены по боевому коду: под #if их пришлось бы обкладывать "
            + "директивами в каждом вызывающем файле. В релизной сборке "
            + "ProfilerMarker.Auto() и так вырождается в пустышку");
    }
}
