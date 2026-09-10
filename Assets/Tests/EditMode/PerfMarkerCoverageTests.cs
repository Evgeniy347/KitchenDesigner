using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class PerfMarkerCoverageTests
{
    private static string ScriptsRoot() => Path.Combine(Application.dataPath, "Scripts");

    private static string DiagnosticsDir() =>
        Path.Combine(ScriptsRoot(), "Core", "Diagnostics");

    private static string[] ProductionFilesOutsideDiagnostics() =>
        Directory.GetFiles(ScriptsRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(DiagnosticsDir(), StringComparison.Ordinal))
            .ToArray();

    private static Dictionary<string, string> MarkerNameByFieldName()
    {
        var names = PerfMarkers.NamesInDeclarationOrder;
        var map = new Dictionary<string, string>();
        foreach (var field in typeof(PerfMarkers).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(PerfMarker)) continue;
            var marker = (PerfMarker)field.GetValue(null)!;
            map[field.Name] = names[marker.Slot];
        }
        return map;
    }

    [Test]
    public void TheScan_SeesBothTheMarkersAndTheSources()
    {
        Assert.Greater(MarkerNameByFieldName().Count, 10,
            "рефлексия не нашла маркеров — сторож ниже зеленел бы, ничего не проверив");
        Assert.Greater(ProductionFilesOutsideDiagnostics().Length, 100,
            "скан не видит исходников — сторож ниже зеленел бы, ничего не проверив");
    }

    [Test]
    public void EveryDeclaredMarker_HasExactlyOneMeasurementSite_OutsideDiagnostics()
    {
        var files = ProductionFilesOutsideDiagnostics();
        var text = files.ToDictionary(f => f, File.ReadAllText);

        var offenders = new List<string>();
        foreach (var (fieldName, markerName) in MarkerNameByFieldName())
        {
            var pattern = new Regex(@"\bPerfMarkers\." + Regex.Escape(fieldName) + @"\b");
            int sites = text.Sum(pair => pattern.Matches(pair.Value).Count);

            if (sites != 1)
                offenders.Add($"{markerName}: мест замера {sites}, а должно быть ровно одно");
        }

        CollectionAssert.IsEmpty(offenders,
            "профиль печатает СПИСОК ИМЁН из PerfMarkers. Имя без места замера — молчащий "
            + "датчик: строка «0.00ms» против метода читается как «этот метод бесплатен», "
            + "и оптимизировать идут не туда. Два места на одно имя — тоже ложь: замеры "
            + "сложатся в одну строку, и по ней уже не понять, какая из веток стоила. "
            + "Найдено: " + string.Join("; ", offenders));
    }

    [Test]
    public void EveryMarkerName_NamesTheFile_ItIsActuallyMeasuredIn()
    {
        var files = ProductionFilesOutsideDiagnostics();
        var offenders = new List<string>();

        foreach (var (fieldName, markerName) in MarkerNameByFieldName())
        {
            var pattern = new Regex(@"\bPerfMarkers\." + Regex.Escape(fieldName) + @"\b");
            var site = files.FirstOrDefault(f => pattern.IsMatch(File.ReadAllText(f)));
            if (site == null) continue;

            string owner = markerName.Substring(0, Math.Max(markerName.IndexOf('.'), 0));
            string stem = Path.GetFileNameWithoutExtension(site)!;

            if (owner.Length == 0)
                offenders.Add($"{markerName}: имя без точки — по нему не найти класс");
            else if (!stem.StartsWith(owner, StringComparison.Ordinal)
                     && !owner.StartsWith(stem, StringComparison.Ordinal))
                offenders.Add($"{markerName} замеряется в {stem}.cs");
        }

        CollectionAssert.IsEmpty(offenders,
            "имя маркера — это адрес: по «Wall.SyncOpenings» в дампе идут открывать "
            + "Wall.cs. Имя, разъехавшееся с местом замера, посылает читателя дампа "
            + "в чужой файл — и тем дороже, чем позднее это заметят: " + string.Join("; ", offenders));
    }

    [Test]
    public void TheOnlyMarkersLeftOnTheUnityProfiler_AreTheTwoWallOpeningOnes()
    {
        var unityOnly = typeof(PerfMarkers)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Unity.Profiling.ProfilerMarker))
            .Select(f => f.Name)
            .OrderBy(n => n)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "DoorSnapToWall", "WindowSnapToWall" }, unityOnly,
            "маркер типа ProfilerMarker меряет ЧЕРЕЗ Unity, а его Begin/End помечены "
            + "[Conditional(\"ENABLE_PROFILER\")] — в обычной сборке плеера он не даёт "
            + "ничего, поэтому такие маркеры и не попадают в список F9-профиля: имя без "
            + "числа хуже отсутствующего имени. Эти двое остались, потому что их "
            + "единственное место замера — WallOpeningElement.SnapToWall, и файл в тот "
            + "момент правил другой агент. Освободится файл — перевести оба на Reg() "
            + "и удалить этот тест; появится третий — так делать нельзя");
    }

    [Test]
    public void TheFacadeOpeningPath_IsCoveredEndToEnd_BecauseThatIsWhereTheFrameGoes()
    {
        var names = PerfMarkers.NamesInDeclarationOrder;

        var mustBeThere = new[]
        {
            "FacadeElement.StepDoor",
            "FacadeElement.ApplyDoor",
            "OpeningCollision.FindMaxProgress",
            "OpeningCollision.BuildObstacles",
            "OpeningCollision.ScanForBlock",
            "AttachLinks.Descendants",
            "SceneChangeTracker.Poll",
            "SceneChangeTracker.SettleDerivedLinks",
            "ScrewLegHostLink.ApplyAll",
            "PipeFittingSizeLink.ApplyAll",
            "SceneAnalyzer.Analyze",
        };

        CollectionAssert.IsSubsetOf(mustBeThere, names,
            "открытие фасада роняло кадр с 60 fps до 5, а профиль показывал «время "
            + "уходит мимо маркеров»: путь открытия не был обвешан вовсе. Снять маркер "
            + "с любого звена этой цепи значит вернуть слепое пятно ровно туда, где "
            + "просадку и ловили");
    }
}
