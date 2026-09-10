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

    private static string[]? _files;

    private static string[] ProductionFilesOutsideDiagnostics() =>
        _files ??= Directory.GetFiles(ScriptsRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(DiagnosticsDir(), StringComparison.Ordinal))
            .ToArray();

    /// <summary>Дерево исходников (около 690 файлов) читается ОДИН раз на класс.
    /// Раньше `File.ReadAllText` жил внутри `FirstOrDefault`, вызываемого на каждый
    /// маркер — O(маркеры × файлы). Это починили, но словарь всё равно строился
    /// ЗАНОВО в каждом из двух сканирующих тестов: 1400 чтений диска вместо 690.
    /// Кэш не ослабляет сторожа — оба множества по-прежнему выводятся из исходника,
    /// а что скан вообще что-то видит, стережёт
    /// <see cref="TheScan_SeesBothTheMarkersAndTheSources"/>.</summary>
    private static Dictionary<string, string>? _sourceText;

    private static Dictionary<string, string> SourceText() =>
        _sourceText ??= ProductionFilesOutsideDiagnostics().ToDictionary(f => f, File.ReadAllText);

    private static Dictionary<string, string>? _markerNames;

    private static Dictionary<string, string> MarkerNameByFieldName()
    {
        if (_markerNames != null) return _markerNames;

        var names = PerfMarkers.NamesInDeclarationOrder;
        var map = new Dictionary<string, string>();
        foreach (var field in typeof(PerfMarkers).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(PerfMarker)) continue;
            var marker = (PerfMarker)field.GetValue(null)!;
            map[field.Name] = names[marker.Slot];
        }
        return _markerNames = map;
    }

    /// <summary>Регулярка на имя поля собирается один раз, а не заново в каждом из
    /// двух тестов на каждый из ~40 маркеров.</summary>
    private static readonly Dictionary<string, Regex> SiteOf =
        new Dictionary<string, Regex>(StringComparer.Ordinal);

    private static Regex SitePattern(string fieldName)
    {
        if (SiteOf.TryGetValue(fieldName, out var cached)) return cached;
        var made = new Regex(@"\bPerfMarkers\." + Regex.Escape(fieldName) + @"\b", RegexOptions.Compiled);
        SiteOf[fieldName] = made;
        return made;
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
        var text = SourceText();

        var offenders = new List<string>();
        foreach (var (fieldName, markerName) in MarkerNameByFieldName())
        {
            var pattern = SitePattern(fieldName);
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
        // Раньше `File.ReadAllText` жил внутри `FirstOrDefault`, вызываемого на каждый
        // маркер — O(маркеры × файлы), порядка 40 × 700 чтений диска на один прогон. Дерево
        // читается один раз на КЛАСС (SourceText), дальше сторож только ищет по уже
        // прочитанному тексту уже собранной регуляркой.
        var text = SourceText();
        var offenders = new List<string>();

        foreach (var (fieldName, markerName) in MarkerNameByFieldName())
        {
            var pattern = SitePattern(fieldName);
            var site = text.FirstOrDefault(pair => pattern.IsMatch(pair.Value)).Key;
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
