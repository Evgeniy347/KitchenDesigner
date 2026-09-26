using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using KitchenDesigner.Tests.Geometry;

/// <summary>Баг-репорт: пользователь работает в проекте, открывает «Загрузить» — список
/// «Недавних проектов пока нет», хотя он и есть текущий проект. Причина была в том, что
/// КАЖДЫЙ путь, которым проект становится текущим (старт, .kdproj из командной строки,
/// «Загрузить», клик по недавнему, «Новый проект», Save, Save As, автосохранение,
/// MCP save_project/load_project), сам решал, регистрировать ли путь в списке недавних —
/// и один из них (первое «Сохранить» без ранее открытого файла) этого не делал.
///
/// Починка: единственное место, которое пишет `LastPath`, обязано делать это через
/// <see cref="KitchenDesigner.Core.SaveLoadManagerInstance.AdoptCurrentPath"/>, а не
/// присваиванием напрямую — эта функция и только она вызывает
/// <see cref="KitchenDesigner.Core.RecentProjects.Remember"/>. Этот тест — сторож ПРОТИВ
/// обхода: он сканирует продакшен-код на прямое присваивание `LastPath = ...` и требует,
/// чтобы оно встречалось только там, где сама функция и её тонкая обвязка (сеттер
/// свойства, статический фасад, выход из демо-режима) и определены. Новый путь, который
/// решит присвоить `LastPath` в обход <c>AdoptCurrentPath</c>, покрасит именно этот тест —
/// не через год, а в том же коммите.</summary>
public class RecentProjectsRegistrationCoverageTests
{
    private static readonly (string file, string why)[] AllowList =
    {
        ("SaveLoadManagerInstance.cs",
            "здесь и определены LastPath, и AdoptCurrentPath, который единственный обязан звать RecentProjects.Remember"),
        ("SaveLoadManager.cs",
            "статический фасад: `Instance.LastPath = value` — тонкая переадресация в SaveLoadManagerInstance, а не отдельное решение"),
        ("DemoProjectLoader.cs",
            "OpenDemo намеренно ОЧИЩАЕТ LastPath после открытия демо-проекта — демо не должно становиться «текущим проектом»"),
    };

    private static readonly Regex AssignmentPattern =
        SourceCorpus.Rule(@"(?<!\w)LastPath\s*=(?![=>])");

    private static string ScriptsDir() => RepoPaths.Subdir("Assets", "Scripts");

    private static List<string> RawAssignments()
    {
        var hits = new List<string>();
        foreach (var file in SourceCorpus.Files(ScriptsDir()))
        {
            var lines = SourceCorpus.Lines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (SourceCorpus.StartsAComment(trimmed)) continue;
                if (AssignmentPattern.IsMatch(lines[i]))
                    hits.Add(Path.GetFileName(file) + ":" + (i + 1) + "\n    " + trimmed);
            }
        }
        return hits;
    }

    private static bool IsAllowed(string hit)
    {
        foreach (var (file, _) in AllowList)
            if (hit.StartsWith(file + ":", StringComparison.Ordinal)) return true;
        return false;
    }

    [Test]
    public void NoFileOutsideTheAllowList_AssignsLastPath_Directly()
    {
        var offenders = new List<string>();
        foreach (var hit in RawAssignments())
            if (!IsAllowed(hit)) offenders.Add(hit);

        Assert.IsEmpty(offenders,
            "Правило: единственный писатель LastPath — SaveLoadManagerInstance.AdoptCurrentPath, "
            + "потому что он же кладёт путь в список недавних проектов (RecentProjects.Remember). "
            + "Прямое присваивание LastPath в обход него сделает проект «текущим», но НЕ добавит "
            + "его в «Загрузить» — ровно баг из отчёта. Почини: замени присваивание вызовом "
            + "SaveLoadManager.AdoptCurrentPath(path), либо, если это намеренный выход из "
            + "«текущего проекта» (как в DemoProjectLoader), добавь файл в allow-list этого теста "
            + "с причиной. Найдено:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void EveryAllowListEntry_NamesAFileThatStillExists_AndHasAReason()
    {
        var files = SourceCorpus.Files(ScriptsDir());
        foreach (var (file, why) in AllowList)
        {
            bool exists = false;
            foreach (var f in files)
                if (Path.GetFileName(f) == file) { exists = true; break; }
            Assert.IsTrue(exists,
                "запись allow-list называет несуществующий файл " + file
                + " — запись пережила свой файл и начнёт освобождать следующий с этим именем");
            Assert.IsNotEmpty(why, "запись allow-list " + file + " без причины неотличима от недосмотра");
        }
    }

    [Test]
    public void TheScan_ActuallySeesProductionFiles_AndTheKnownAssignments()
    {
        Assert.Greater(SourceCorpus.Files(ScriptsDir()).Length, 100,
            "скан обязан видеть Assets/Scripts целиком");
        var hits = RawAssignments();
        Assert.IsNotEmpty(hits,
            "пустой список означает сломанный скан, а не то, что LastPath нигде не присваивается — "
            + "AdoptCurrentPath и его обвязка присваивают его сегодня");
    }

    [Test]
    public void ThePattern_MatchesARealAssignment_AndIgnoresALookalike()
    {
        Assert.IsTrue(AssignmentPattern.IsMatch("            LastPath = path;"),
            "голое присваивание внутри SaveLoadManagerInstance — то, что правило обязано ловить");
        Assert.IsTrue(AssignmentPattern.IsMatch("            set => _files.LastPath = value;"),
            "присваивание через точку — та же форма зависимости");
        Assert.IsFalse(AssignmentPattern.IsMatch("        public bool HasLastPath => Instance.HasLastPath;"),
            "HasLastPath — другое имя, а не LastPath со скрытым префиксом");
        Assert.IsFalse(AssignmentPattern.IsMatch("        public string LastPath { get; }"),
            "объявление свойства — не присваивание");
        Assert.IsFalse(AssignmentPattern.IsMatch("            if (SaveLoadManager.LastPath == path)"),
            "сравнение через == — не присваивание");
    }
}
