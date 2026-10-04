using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class LoadProjectWindowUITests
{
    private GameObject? _canvasGo;
    private string[]? _recentBackup;
    private string? _prevLastPath;
    private readonly System.Collections.Generic.List<string> _tempFiles = new();

    [SetUp]
    public void SetUp()
    {
        _recentBackup = RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values;
        _prevLastPath = SaveLoadManager.LastPath;
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_recentBackup != null)
            RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = _recentBackup;
        SaveLoadManager.LastPath = _prevLastPath!;
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
        _tempFiles.Clear();
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    private string MakeProjectFile(string name, string appVersion, string? modifiedUtc = null)
    {
        string path = Path.Combine(Application.temporaryCachePath, name);
        File.WriteAllText(path, "{\"version\":1,\"appVersion\":\"" + appVersion + "\"}");
        if (modifiedUtc != null)
            File.SetLastWriteTimeUtc(path, System.DateTime.Parse(modifiedUtc,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal));
        _tempFiles.Add(path);
        return path;
    }

    private static void Remember(params string[] paths) =>
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = paths;

    private LoadProjectWindowUI Build()
    {
        var ui = _canvasGo!.AddComponent<LoadProjectWindowUI>();
        ui.Build(_canvasGo.transform);
        return ui;
    }

    private Transform Footer => _canvasGo!.transform.Find("LoadProjectWindow/LoadProjectWindowFooter")!;

    private static string[] NamesShown(LoadProjectWindowUI ui) =>
        ui.Table.ShownRows.Select(r => r.Cell(LoadProjectRows.NameColumn)).ToArray();

    /// <summary>L5/L7: this window used to register with <c>ProjectWindows</c>, so its
    /// visibility was captured by every save/autosave and restored on the next load - an
    /// autosave firing while the user had the dialog open popped it back up at the next
    /// launch. It is a modal open/new-project dialog, not a panel worth remembering across
    /// sessions, so it deliberately stays OUT of that registry now.</summary>
    [Test]
    public void Build_DoesNotRegisterWithProjectWindows()
    {
        var ui = Build();
        Assert.AreEqual("loadProject", ui.WindowId);
        CollectionAssert.DoesNotContain(
            new System.Collections.Generic.List<IProjectWindow>(ProjectWindows.All), ui,
            "окно «Открыть проект» — модальный диалог: его видимость не должна попадать в " +
            "сохранённое состояние проекта и всплывать заново при следующем запуске");
    }

    [Test]
    public void CapturingProjectWindowState_WhileTheDialogIsOpen_DoesNotReopenItOnTheNextApply()
    {
        var ui = Build();
        ui.SetVisible(true);

        var captured = ProjectWindows.Capture();
        ui.SetVisible(false);

        ProjectWindows.Apply(captured);

        Assert.IsFalse(ui.IsVisible,
            "сохранение (в т.ч. автосохранение) со случайно открытым окном «Открыть проект» не " +
            "должно заставлять его всплывать при восстановлении состояния окон");
    }

    [Test]
    public void SetVisible_True_ShowsTheWindow_AndFalse_HidesIt()
    {
        var ui = Build();
        Assert.IsFalse(ui.IsVisible);
        ui.SetVisible(true);
        Assert.IsTrue(ui.IsVisible);
        ui.SetVisible(false);
        Assert.IsFalse(ui.IsVisible);
    }

    [Test]
    public void Build_FooterHasNewProjectBrowseAndOpen_WithTheirLabels()
    {
        Build();
        var newBtn = Footer.Find("LoadNewProject");
        var browse = Footer.Find("LoadOpenFile");
        var open = Footer.Find("LoadOpenSelected");
        Assert.IsNotNull(newBtn);
        Assert.IsNotNull(browse);
        Assert.IsNotNull(open);
        Assert.AreEqual("Новый проект", newBtn!.GetComponentInChildren<TMP_Text>().text);
        Assert.AreEqual("Обзор…", browse!.GetComponentInChildren<TMP_Text>().text);
        Assert.AreEqual("Открыть", open!.GetComponentInChildren<TMP_Text>().text);
    }

    [Test]
    public void SetVisible_HeightNeverExceedsHalfTheCanvas()
    {
        var rect = (RectTransform)_canvasGo!.transform;
        rect.sizeDelta = new Vector2(900, 500);

        var ui = Build();
        ui.SetVisible(true);

        var panel = (RectTransform)ui.WindowRect!;
        Assert.LessOrEqual(panel.sizeDelta.y, 500 * LoadWindowLayout.MaxScreenHeightFraction + 0.5f);
    }

    [Test]
    public void Footer_NewProjectStaysLeft_AndOpenIsTheRightmostPrimaryAction()
    {
        var ui = Build();
        ui.SetVisible(true);

        float X(string node)
        {
            var corners = new Vector3[4];
            ((RectTransform)Footer.Find(node)!).GetWorldCorners(corners);
            return corners[0].x;
        }

        Assert.Less(X("LoadNewProject"), X("LoadOpenFile"), "«Новый проект» и «Обзор…» — вторичные, слева");
        Assert.Less(X("LoadOpenFile"), X("LoadOpenSelected"), "«Открыть» — основная, крайняя справа (D5)");
        Assert.AreEqual(UIStyle.Accent, Footer.Find("LoadOpenSelected")!.GetComponent<Image>().color);
    }

    [Test]
    public void ClickingAnExistingRecentRow_OnlySelectsIt()
    {
        var path = MakeProjectFile("lpwui_existing.kdproj", BuildInfo.Version);
        Remember(path);
        SaveLoadManager.LastPath = "";

        var ui = Build();
        ui.SetVisible(true);
        ui.Table.Select(null);
        ui.Table.Select(ui.Table.ShownRows[0]);

        Assert.AreNotEqual(path, SaveLoadManager.LastPath, "один клик выбирает строку, а не открывает проект");
        Assert.IsTrue(ui.IsVisible);
        Assert.IsTrue(Footer.Find("LoadOpenSelected")!.GetComponent<Button>().interactable);
    }

    [Test]
    public void DoubleClickingAnExistingRecentRow_LoadsThatProject_AndClosesTheWindow()
    {
        var path = MakeProjectFile("lpwui_double.kdproj", BuildInfo.Version);
        Remember(path);
        SaveLoadManager.LastPath = "";

        var ui = Build();
        ui.SetVisible(true);
        Assert.AreEqual(1, ui.Table.ShownRows.Count, "строка существующего проекта обязана построиться");
        ui.Table.Activate(ui.Table.ShownRows[0]);

        Assert.AreEqual(path, SaveLoadManager.LastPath);
        Assert.IsFalse(ui.IsVisible,
            "успешная загрузка из окна обязана закрыть его — иначе поверх сцены остаётся "
            + "модальное окно выбора проекта");
    }

    [Test]
    public void TheOpenButton_LoadsTheSelectedProject_AndClosesTheWindow()
    {
        var path = MakeProjectFile("lpwui_open_button.kdproj", BuildInfo.Version);
        Remember(path);
        SaveLoadManager.LastPath = "";

        var ui = Build();
        ui.SetVisible(true);
        Footer.Find("LoadOpenSelected")!.GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(path, SaveLoadManager.LastPath);
        Assert.IsFalse(ui.IsVisible, "«Открыть» закрывает окно после успешной загрузки");
    }

    [Test]
    public void ActivatingARecentRow_WhenTheFileIsCorrupt_LoadFails_AndTheWindowStaysOpen()
    {
        var path = Path.Combine(Application.temporaryCachePath, "lpwui_corrupt.kdproj");
        File.WriteAllText(path, "not valid json {");
        _tempFiles.Add(path);
        Remember(path);
        SaveLoadManager.LastPath = "";
        LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex(@"^\[SaveLoad\] Load failed:"));

        var ui = Build();
        ui.SetVisible(true);
        Assert.AreEqual(1, ui.Table.ShownRows.Count, "файл существует на диске — строка обязана быть доступной");
        ui.Table.Activate(ui.Table.ShownRows[0]);

        Assert.IsTrue(ui.IsVisible,
            "загрузка провалилась — окно обязано остаться открытым, а не закрыться на отказ");
    }

    [Test]
    public void ClickingNewProject_WhenTheNativeDialogIsCancelled_TheWindowStaysOpen()
    {
        var ui = Build();
        ui.SetVisible(true);

        var newBtn = Footer.Find("LoadNewProject");
        Assert.IsNotNull(newBtn);
        LogAssert.Expect(LogType.Assert, new System.Text.RegularExpressions.Regex("^Cancelling FileDialog"));
        newBtn!.GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(ui.IsVisible,
            "диалог сохранения в batch-режиме редактора всегда возвращает отмену — окно "
            + "обязано остаться открытым, а не закрыться до того, как известен результат");
    }

    [Test]
    public void ClickingBrowse_WhenTheNativeDialogIsCancelled_TheWindowStaysOpen()
    {
        var ui = Build();
        ui.SetVisible(true);

        var browse = Footer.Find("LoadOpenFile");
        Assert.IsNotNull(browse);
        LogAssert.Expect(LogType.Assert, new System.Text.RegularExpressions.Regex("^Cancelling FileDialog"));
        browse!.GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(ui.IsVisible,
            "диалог открытия в batch-режиме редактора всегда возвращает отмену — окно "
            + "обязано остаться открытым, а не закрыться до того, как известен результат");
    }

    [Test]
    public void EmptyRecentList_ButACurrentProjectIsOpen_ShowsItsRow()
    {
        Remember();
        var path = MakeProjectFile("lpwui_seeded_current.kdproj", BuildInfo.Version);
        SaveLoadManager.LastPath = path;

        var ui = Build();
        ui.SetVisible(true);

        Assert.AreEqual(1, ui.Table.ShownRows.Count,
            "список недавних пуст, но пользователь уже работает в проекте — окно обязано "
            + "показать хотя бы его, а не «Сохранённых проектов пока нет»");
    }

    [Test]
    public void EmptyRecentList_ShowsTheEmptyState_WithANewProjectAction()
    {
        Remember();
        SaveLoadManager.LastPath = "";

        var ui = Build();
        ui.SetVisible(true);

        var empty = ui.Table.Empty!;
        Assert.IsTrue(empty.Root.gameObject.activeSelf, "пустой список — не пустое поле, а состояние D9");
        Assert.AreEqual("Сохранённых проектов пока нет", empty.Title.text);
        Assert.IsNotNull(empty.ActionButton, "основная кнопка следующего шага — «Новый проект»");
        Assert.IsTrue(empty.ActionButton!.gameObject.activeSelf);
        Assert.IsFalse(Footer.Find("LoadOpenSelected")!.GetComponent<Button>().interactable,
            "выбирать нечего — «Открыть» выключена");
    }

    [Test]
    public void TheDefaultOrder_IsModifiedDescending_AndTheHeaderGlyphSaysSo()
    {
        var older = MakeProjectFile("lpwui_order_a.kdproj", BuildInfo.Version, "2026-01-01T10:00:00Z");
        var newer = MakeProjectFile("lpwui_order_b.kdproj", BuildInfo.Version, "2026-03-01T10:00:00Z");
        Remember(older, newer);
        SaveLoadManager.LastPath = "";

        var ui = Build();
        ui.SetVisible(true);

        CollectionAssert.AreEqual(new[] { "lpwui_order_b.kdproj", "lpwui_order_a.kdproj" }, NamesShown(ui),
            "по умолчанию «Изменён ↓»: свежие проекты сверху, а не в порядке открытия");
        StringAssert.Contains(DataTable.SortGlyphDescending,
            ui.Table.HeaderLabel(LoadProjectRows.ModifiedColumn).text);
    }

    [Test]
    public void ClickingTheNameHeader_SortsByName_AndAMissingFileGoesLastByDate()
    {
        var b = MakeProjectFile("lpwui_sort_b.kdproj", BuildInfo.Version, "2026-01-01T10:00:00Z");
        var a = MakeProjectFile("lpwui_sort_a.kdproj", BuildInfo.Version, "2026-03-01T10:00:00Z");
        string gone = Path.Combine(Application.temporaryCachePath, "lpwui_sort_gone.kdproj");
        if (File.Exists(gone)) File.Delete(gone);
        Remember(gone, b, a);
        SaveLoadManager.LastPath = "";

        var ui = Build();
        ui.SetVisible(true);
        Assert.AreEqual("lpwui_sort_gone.kdproj", NamesShown(ui).Last(),
            "у пропавшего файла нет даты изменения — при порядке «Изменён ↓» он уходит вниз");

        ui.Table.ToggleSort(LoadProjectRows.NameColumn);

        CollectionAssert.AreEqual(
            new[] { "lpwui_sort_a.kdproj", "lpwui_sort_b.kdproj", "lpwui_sort_gone.kdproj" }, NamesShown(ui));
    }

    [Test]
    public void TheSearchField_FiltersTheRowsByName_AndNothingFoundIsSaidOutLoud()
    {
        var kitchen = MakeProjectFile("lpwui_kitchen.kdproj", BuildInfo.Version);
        var dacha = MakeProjectFile("lpwui_dacha.kdproj", BuildInfo.Version);
        Remember(kitchen, dacha);
        SaveLoadManager.LastPath = "";

        var ui = Build();
        ui.SetVisible(true);
        Assert.AreEqual(2, ui.Table.ShownRows.Count);

        ui.Search.text = "KITCHEN";
        CollectionAssert.AreEqual(new[] { "lpwui_kitchen.kdproj" }, NamesShown(ui), "поиск по имени без учёта регистра");

        ui.Search.text = "no_such_project_999";
        Assert.AreEqual(0, ui.Table.ShownRows.Count);
        Assert.AreEqual("Ничего не найдено", ui.Table.Empty!.Title.text,
            "пустой результат поиска — не «проектов нет»: проекты есть, их скрыл запрос");
        Assert.IsFalse(ui.Table.Empty.ActionButton!.gameObject.activeSelf,
            "«Новый проект» в пустом результате поиска не предлагается: проекты есть");
    }

    [Test]
    public void StateLabels_NameTheCurrentNewerAndMissingProjects()
    {
        var current = MakeProjectFile("lpwui_label_current.kdproj", BuildInfo.Version, "2026-03-01T10:00:00Z");
        var newer = MakeProjectFile("lpwui_label_newer.kdproj", "99.0", "2026-02-01T10:00:00Z");
        string gone = Path.Combine(Application.temporaryCachePath, "lpwui_label_gone.kdproj");
        if (File.Exists(gone)) File.Delete(gone);
        Remember(current, newer, gone);
        SaveLoadManager.LastPath = current;

        var ui = Build();
        ui.SetVisible(true);

        string BadgeOf(int row, string key)
        {
            var badge = ui.Table.RowRect(row).Find(RowBadge.NodePrefix + key);
            return badge != null ? badge.GetComponentInChildren<TMP_Text>().text : "";
        }

        CollectionAssert.AreEqual(
            new[] { "lpwui_label_current.kdproj", "lpwui_label_newer.kdproj", "lpwui_label_gone.kdproj" },
            NamesShown(ui));
        float WidthOf(int row, string key) => ((RectTransform)ui.Table.RowRect(row).Find(RowBadge.NodePrefix + key)!).sizeDelta.x;
        Assert.Greater(WidthOf(2, "missing"), 80f,
            "метки строятся при открытии окна: ширину по тексту TMP меряет верно только в активной иерархии "
            + "(в неактивной «файл не найден» давал 22 px, и метка наезжала на имя)");
        var goneName = ui.Table.CellLabel(2, LoadProjectRows.NameColumn)!.rectTransform;
        Assert.Greater(goneName.sizeDelta.x, 120f, "имя пропавшего файла не сжато метками до пары пикселей");
        Assert.AreEqual("открыт", BadgeOf(0, "current"));
        Assert.AreEqual("новее программы", BadgeOf(1, "newer"));
        Assert.AreEqual("файл не найден", BadgeOf(2, "missing"));
        Assert.AreEqual("", BadgeOf(0, "newer"), "открытый проект той же версии не «новее программы»");
        Assert.AreEqual("", BadgeOf(1, "current"));
    }

    [Test]
    public void MissingFile_IsDisabled_RedBadged_AndCannotBeSelectedOrOpened()
    {
        var real = MakeProjectFile("lpwui_missing_real.kdproj", BuildInfo.Version, "2026-03-01T10:00:00Z");
        string gone = Path.Combine(Application.temporaryCachePath, "lpwui_missing_gone.kdproj");
        if (File.Exists(gone)) File.Delete(gone);
        Remember(gone, real);
        SaveLoadManager.LastPath = "";

        var ui = Build();
        ui.SetVisible(true);

        var missing = ui.Table.ShownRows.Single(r => !r.Enabled);
        int index = ui.Table.ShownRows.ToList().IndexOf(missing);
        var badge = ui.Table.RowRect(index).Find(RowBadge.NodePrefix + "missing")!.GetComponentInChildren<TMP_Text>();
        Assert.AreEqual(UIStyle.TextError, badge.color, "пропавший файл помечен словом и красным цветом");
        Assert.AreEqual(UIStyle.TextDisabled, ui.Table.CellLabel(index, LoadProjectRows.NameColumn)!.color);

        ui.Table.Select(missing);
        ui.Table.Activate(missing);
        Assert.AreNotSame(missing, ui.Table.Selected, "строку пропавшего файла выбрать нельзя");
        Assert.IsFalse(SaveLoadManager.HasLastPath && SaveLoadManager.LastPath == gone,
            "двойной клик по пропавшему файлу не имеет права ничего открывать");
    }

    [Test]
    public void VersionMismatch_IsRedAndMarkedWithABang_NotJustColoured()
    {
        var mismatched = MakeProjectFile("lpwui_version_old.kdproj", "0.1", "2026-03-01T10:00:00Z");
        var current = MakeProjectFile("lpwui_version_current.kdproj", BuildInfo.Version, "2026-02-01T10:00:00Z");
        Remember(mismatched, current);
        SaveLoadManager.LastPath = "";

        var ui = Build();
        ui.SetVisible(true);

        var odd = ui.Table.CellLabel(0, LoadProjectRows.VersionColumn)!;
        var fine = ui.Table.CellLabel(1, LoadProjectRows.VersionColumn)!;
        Assert.AreEqual(UIStyle.TextError, odd.color);
        StringAssert.Contains(UIStyle.GlyphWarning, odd.text, "несовпадение версии помечено не только цветом, но и «!»");
        Assert.AreEqual(UIStyle.TextSecondary, fine.color);
        StringAssert.DoesNotContain(UIStyle.GlyphWarning, fine.text);
    }

    [Test]
    public void Forgetting_NeedsTwoClicks_RemovesOnlyTheListEntry_AndKeepsTheFile()
    {
        var keep = MakeProjectFile("lpwui_forget_keep.kdproj", BuildInfo.Version, "2026-03-01T10:00:00Z");
        var drop = MakeProjectFile("lpwui_forget_drop.kdproj", BuildInfo.Version, "2026-02-01T10:00:00Z");
        Remember(keep, drop);
        SaveLoadManager.LastPath = "";

        var ui = Build();
        ui.SetVisible(true);
        int index = NamesShown(ui).ToList().IndexOf("lpwui_forget_drop.kdproj");
        var cross = ui.Table.RowRect(index).Find(LoadProjectRowDecor.ForgetNode)!.GetComponent<Button>();

        cross.onClick.Invoke();
        Assert.AreEqual(2, ui.Table.ShownRows.Count, "первый клик только взводит ×: «?!» вместо удаления");
        Assert.AreEqual(UIStyle.GlyphConfirm, cross.GetComponentInChildren<TMP_Text>().text);

        cross.onClick.Invoke();
        CollectionAssert.AreEqual(new[] { "lpwui_forget_keep.kdproj" }, NamesShown(ui));
        CollectionAssert.AreEqual(new[] { keep }, RecentProjects.Paths());
        Assert.IsTrue(File.Exists(drop), "запись убирается из списка, файл на диске остаётся");
    }

    [Test]
    public void TheCurrentProjectRow_HasNoForgetCross()
    {
        var current = MakeProjectFile("lpwui_nocross_current.kdproj", BuildInfo.Version, "2026-03-01T10:00:00Z");
        var other = MakeProjectFile("lpwui_nocross_other.kdproj", BuildInfo.Version, "2026-02-01T10:00:00Z");
        Remember(current, other);
        SaveLoadManager.LastPath = current;

        var ui = Build();
        ui.SetVisible(true);

        Assert.IsNull(ui.Table.RowRect(0).Find(LoadProjectRowDecor.ForgetNode),
            "открытый проект из списка не убирают: при пустом списке его всё равно вернёт как «текущий»");
        Assert.IsNotNull(ui.Table.RowRect(1).Find(LoadProjectRowDecor.ForgetNode));
    }
}
