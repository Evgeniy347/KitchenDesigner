using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;
using KitchenDesigner.Tests.Geometry;

/// <summary>
/// Стражи ВИДИМОСТИ значка подсказки «i» (docs/UI-GUIDELINES.md → §13).
///
/// <c>HintTextGuardTests</c> сверяет ключи и тексты — то есть НАПОЛНЕНИЕ. Про то,
/// виден ли значок человеку и стоит ли он там, где обещан, до этого набора не
/// спрашивал никто, и это стоило дефекта: три «i» строк стены
/// (<c>WallFieldsEditor</c>, 7d2a2924) вешались на РОДИТЕЛЯ подписи, а панель
/// свойств плоская — строк как объектов в ней нет, их изображает
/// <c>ContextMenuLayout</c>, раздавая подписям и контролам y и <c>SetActive</c>.
/// Значок в раздачу не попадал: он оставался включённым в панели ЛЮБОГО элемента
/// (три «i» в меню обычной детали) и навсегда замирал на y=0 — в вертикальной
/// середине панели, поверх чужой строки. Механизм существовал, подсказки не было
/// ни у кого: у стены значки стояли не на своих строках, у всех остальных
/// стояли вообще без повода.
///
/// Отсюда форма стражей: значок ПРИКЛЕЕН к своему контролу (тест 1), поэтому
/// гаснет и ездит вместе с ним, и стоит НА нём ненулевым прямоугольником
/// (тест 2). Третий — наблюдаемая пара противоположных входов: у стены значки
/// есть, у детали их нет (agents/TEST-DESIGN.md → противоположные входы).
/// </summary>
public class HintBadgeVisibilityTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;

    /// <summary>Панель строится ОДИН раз на класс: сборка контекстного меню стоит
    /// ~0,3 с, а все три стража перебирают типы одной и той же переоткрываемой
    /// панелью — ровно как боевой сценарий (см. сводку
    /// <see cref="ContextMenuDisabledRowTests"/>).</summary>
    [OneTimeSetUp]
    public void BuildThePanelOnce()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [OneTimeTearDown]
    public void DropThePanel()
    {
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
    }

    /// <summary>Панель переживает тест, значит потестовое состояние сбрасывается здесь:
    /// взведённое «Удалить» живёт в статике и держит ссылку на элемент, которого уже
    /// нет, — падение было бы MissingReference, а не по делу (та же причина, что в
    /// <see cref="ContextMenuDisabledRowTests"/>).</summary>
    [SetUp]
    public void Setup()
    {
        ((IContextMenuHost)_menu!).Fields.ForgetLastApplyFrame();
        ConfirmDeleteButton.DisarmAll();
    }

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        if (_menu != null) _menu!.Close();
        EveryElementType.ClearScene();
    }

    [Test]
    public void HintBadge_IsGluedToItsControl_AndNotToTheBarePanel()
    {
        var orphans = new List<string>();

        foreach (var badge in Badges())
        {
            var owner = badge.transform.parent;
            if (owner == null)
            {
                orphans.Add(badge.name + " — без родителя");
                continue;
            }
            if (owner.GetComponent<TMP_Text>() != null) continue;
            if (owner.GetComponent<Selectable>() != null) continue;
            orphans.Add($"{badge.name} → «{owner.name}»");
        }

        Assert.IsEmpty(orphans,
            "Правило: значок «i» живёт ВНУТРИ подписи или контрола, который он поясняет. "
            + "Панель свойств плоская — строки в ней не объекты, а записи ContextMenuLayout, и "
            + "видимость со сдвигом получают только те прямоугольники, что зарегистрированы в "
            + "строке. Значок, повешенный на контейнер панели, не зарегистрирован нигде: он "
            + "остаётся включённым у элемента, который эту строку никогда не показывает, и "
            + "навсегда замирает на y=0. Вешайте через HintBadge.AttachAfterLabel — он делает "
            + "значок ребёнком подписи. Безнадзорные значки: " + string.Join(" | ", orphans));
    }

    /// <summary>Самый тихий способ отнять подсказку: ключ заявлен, текст написан, а значка
    /// нет вообще. <c>HintBadge.AttachAfterLabel</c> возвращал null, когда подписи с таким
    /// ключом строки в реестре панели нет, и наполнение оставалось безупречным —
    /// <c>HintTextGuardTests</c> спрашивает про ключи и тексты, а не про строки. Так три
    /// «i» вкладки «Управление» прожили день: ползунок не регистрировал свою подпись
    /// (ecf6f365), а поймал это человек глазами.
    ///
    /// Вопрос задаётся ПРОДУКТУ: обе настоящие панели строятся, и у каждого ключа из
    /// скана обязан найтись выросший значок. Множество ключей берётся из того же скана,
    /// которым живёт сторож наполнения (<c>HintTextGuardTests.DeclaredKeys</c>), — второе
    /// описание того же множества разъехалось бы с первым.</summary>
    [Test]
    public void EveryDeclaredHintKey_GrewABadge_OnTheRealPanel()
    {
        var declared = HintKeyScan.DeclaredKeys();
        Assert.That(declared.Count, Is.GreaterThanOrEqualTo(5),
            "скан по «hint:» не нашёл ни одного размеченного контрола — тогда проверка ниже "
            + "зеленеет вхолостую, что бы ни случилось с панелями");

        var grown = new HashSet<string>();
        CollectKeys(Panel(), grown);
        CollectKeysOfTheSettingsWindow(grown);

        var lost = new List<string>();
        foreach (var (file, key) in declared)
            if (!grown.Contains(key)) lost.Add(file + " → " + key);

        Assert.IsEmpty(lost,
            "Ключ подсказки заявлен, а значок «i» не вырос ни на одной построенной панели. "
            + "Чаще всего это подпись, которой нет в реестре строк панели: "
            + "HintBadge.AttachAfterLabel получает null и ставить значок не на что — "
            + "наполнение при этом безупречно, и человек не видит ничего. Панель названа "
            + "файлом, в котором ключ объявлен; если это ТРЕТЬЯ панель, её сборку надо "
            + "добавить в этот тест. Не выросли: " + string.Join(" | ", lost));
    }

    /// <summary>Окно настроек строится здесь, а не в <c>OneTimeSetUp</c>: остальным стражам
    /// класса оно не нужно, а уцелеть оно не имеет права — вкладки подписаны на статические
    /// <c>EditModeManager.Changed</c> и <c>PhotoMode.Changed</c>, и отписка живёт в
    /// <c>OnDestroy</c> (agents/TEST-DESIGN.md → «!= null … load-bearing»). Отсюда
    /// try/finally: панель уничтожается и тогда, когда сборка упала на полпути.</summary>
    private static void CollectKeysOfTheSettingsWindow(HashSet<string> into)
    {
        var canvas = UIFactory.CreateCanvas("SettingsCanvasForHints");
        try
        {
            var settings = canvas.gameObject.AddComponent<SettingsPanelUI>();
            settings.Build(canvas.transform);
            var window = canvas.transform.Find("SettingsPanel");
            if (window != null) CollectKeys(window!, into);
        }
        finally
        {
            EditModeManager.Reset();
            Object.DestroyImmediate(canvas.gameObject);
        }
    }

    private static void CollectKeys(Transform root, HashSet<string> into)
    {
        foreach (var badge in root.GetComponentsInChildren<HintBadge>(true))
            into.Add(badge.Key);
    }

    [Test]
    public void EveryElementType_VisibleHintBadge_StandsOnTheControlItExplains()
    {
        var offenders = new List<string>();

        foreach (var (type, _) in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(type, "Проба_" + type.Name);
            _menu!.Open(element);
            CollectMisplaced(type.Name, offenders);
            _menu!.Close();
            EveryElementType.ClearScene();
        }

        var wall = SpawnWall();
        _menu!.Open(wall);
        CollectMisplaced("Wall", offenders);

        Assert.IsEmpty(offenders,
            "Правило: видимый значок «i» имеет ненулевой прямоугольник размером "
            + "UIStyle.HintBadgeSize и стоит НА строке, которую поясняет. Значок нулевого "
            + "размера, значок, уехавший со своей строки, и значок, оставшийся включённым в "
            + "панели чужого элемента, — это один и тот же дефект: механизм подсказок есть, "
            + "а подсказки человек не получает. Нарушители: " + string.Join(" | ", offenders));
    }

    [Test]
    public void Wall_ShowsItsFourHints_WhileAPartShowsNone()
    {
        var part = EveryElementType.Spawn(typeof(KitchenElement), "Деталь");
        _menu!.Open(part);
        Assert.AreEqual(0, VisibleBadges().Count,
            "У обычной детали ни одна строка подсказку не заявляла — значит в её панели не "
            + "должно быть ни одного «i». Три значка стены, доставшиеся детали, — это и есть "
            + "дефект, ради которого написан набор: они не только лишние, но и стоят посреди "
            + "чужих строк. Видно: "
            + string.Join(", ", VisibleBadges().ConvertAll(b => b.name)));
        _menu!.Close();
        EveryElementType.ClearScene();

        var wall = SpawnWall();
        _menu!.Open(wall);
        var shown = VisibleBadges();
        Assert.AreEqual(4, shown.Count,
            "Стена заявила «i» на «Несущая», «Технология», «Шов» и «Запас» (WallFieldsEditor) — "
            + "все четыре обязаны быть видны. Значок «Несущей» живёт внутри самого тумблера, в "
            + "дорожке HintBadge.LaneWidth, которую строка отняла у подписи: он гаснет вместе с "
            + "тумблером и не перекрывает клик-зону. "
            + "Противоположный вход к проверке детали: без него зелёным "
            + "будет и код, который не показывает подсказку НИКОМУ. Видно: "
            + string.Join(", ", shown.ConvertAll(b => b.name)));
    }

    /// <summary>Третий способ отнять у человека подсказку — положить значок ПОВЕРХ контрола
    /// (docs/UI-GUIDELINES.md §13: «перекрытый клик по переключателю хуже отсутствующей
    /// подсказки»). Тумблер «Несущая» — единственная строка, где подписи нет в реестре и
    /// значок садится внутрь самого контрола: строка обязана отнять у подписи дорожку
    /// <c>HintBadge.LaneWidth</c>, а не подсунуть значок на клик-зону. Проверка смотрит на
    /// то, что решает исход клика, — на прямоугольники графики с <c>raycastTarget</c>.</summary>
    [Test]
    public void HintBadgeInsideAControl_StandsClearOfItsClickZone()
    {
        var wall = SpawnWall();
        _menu!.Open(wall);

        var overlaps = new List<string>();
        foreach (var badge in VisibleBadges())
        {
            var owner = badge.transform.parent as RectTransform;
            if (owner == null || owner.GetComponent<Selectable>() == null) continue;

            var badgeRect = WorldRect((RectTransform)badge.transform);
            foreach (var graphic in owner.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.raycastTarget) continue;
                if (graphic.transform.IsChildOf(badge.transform)) continue;
                var rect = WorldRect(graphic.rectTransform);
                if (rect.Overlaps(badgeRect))
                    overlaps.Add($"{badge.name} ⟂ «{graphic.name}»");
            }
        }

        Assert.IsEmpty(overlaps,
            "Значок «i», севший внутрь контрола, обязан стоять в свободной дорожке "
            + "HintBadge.LaneWidth: он перехватывает клик своей картинкой, и всё, что он "
            + "накрыл, перестаёт нажиматься. Перекрытия: " + string.Join(" | ", overlaps));
    }

    /// <summary>Четвёртый способ отнять подсказку — поставить значок ТОЧНО в точку,
    /// которой в снимке представлена сама подпись: центр её прямоугольника. Так и вышло
    /// у ящика — «i» строки «Длина» (подпись 44 px в колонке 126 px) села ровно в центр
    /// колонки, и <c>UiNodeOverlap</c> нашёл два узла в одной точке. Поймал это только
    /// PlayMode-снимок, то есть самый дорогой прогон в репозитории; здесь тот же вопрос
    /// задан EditMode-панелью каждого типа. Допуск берётся из самого инварианта, чтобы
    /// сторож и снимок не разошлись.</summary>
    [Test]
    public void EveryElementType_HintBadge_StandsClearOfTheCentreOfItsOwnLabel()
    {
        var offenders = new List<string>();

        foreach (var (type, _) in EveryElementType.Makers)
        {
            var element = EveryElementType.Spawn(type, "Проба_" + type.Name);
            _menu!.Open(element);
            CollectBadgesOnLabelCentres(type.Name, offenders);
            _menu!.Close();
            EveryElementType.ClearScene();
        }

        var wall = SpawnWall();
        _menu!.Open(wall);
        CollectBadgesOnLabelCentres("Wall", offenders);

        Assert.IsEmpty(offenders,
            "Значок «i» и подпись, которую он поясняет, — два узла снимка, и точка узла это "
            + "центр его прямоугольника. Совпали центры — значок стоит в геометрической "
            + "середине подписи, а не в дорожке за её текстом, и золотой снимок краснеет "
            + "инвариантом UiNodeOverlap (допуск " + UiNodeOverlap.CellPixels + " px). "
            + "Подпись с подсказкой обязана ужиматься до «текст + дорожка значка» "
            + "(HintBadge.AttachAfterLabel), тогда её центр уезжает влево от дорожки. "
            + "Нарушители: " + string.Join(" | ", offenders));
    }

    private void CollectBadgesOnLabelCentres(string typeName, List<string> offenders)
    {
        foreach (var badge in VisibleBadges())
        {
            var owner = badge.transform.parent as RectTransform;
            if (owner == null || owner.GetComponent<TMP_Text>() == null) continue;

            var badgeRect = WorldRect((RectTransform)badge.transform);
            var labelRect = WorldRect(owner);
            float dx = Mathf.Abs(badgeRect.center.x - labelRect.center.x);
            float dy = Mathf.Abs(badgeRect.center.y - labelRect.center.y);
            if (dx >= UiNodeOverlap.CellPixels || dy >= UiNodeOverlap.CellPixels) continue;

            offenders.Add($"{typeName}: {badge.name} в центре «{owner.name}» "
                + $"(расхождение {dx:F1}×{dy:F1} px)");
        }
    }

    private void CollectMisplaced(string typeName, List<string> offenders)
    {
        foreach (var badge in VisibleBadges())
        {
            var rect = (RectTransform)badge.transform;
            if (rect.rect.width < UIStyle.HintBadgeSize || rect.rect.height < UIStyle.HintBadgeSize)
            {
                offenders.Add($"{typeName}: {badge.name} — прямоугольник {rect.rect.size}");
                continue;
            }

            var owner = badge.transform.parent as RectTransform;
            if (owner == null)
            {
                offenders.Add($"{typeName}: {badge.name} — родитель не RectTransform");
                continue;
            }

            var centre = (Vector2)rect.TransformPoint(rect.rect.center);
            if (!Contains(WorldRect(owner), centre))
                offenders.Add($"{typeName}: {badge.name} — центр {centre} вне «{owner.name}» "
                    + WorldRect(owner));
        }
    }

    private List<HintBadge> Badges()
    {
        var all = new List<HintBadge>();
        all.AddRange(Panel().GetComponentsInChildren<HintBadge>(true));
        return all;
    }

    private List<HintBadge> VisibleBadges()
    {
        var shown = new List<HintBadge>();
        foreach (var badge in Panel().GetComponentsInChildren<HintBadge>(false))
            if (badge.gameObject.activeInHierarchy) shown.Add(badge);
        return shown;
    }

    private static Rect WorldRect(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return new Rect(corners[0].x, corners[0].y,
            corners[2].x - corners[0].x, corners[2].y - corners[0].y);
    }

    /// <summary>Полуоткрытый <c>Rect.Contains</c> роняет центр, легший ровно на верхнюю
    /// или правую грань подписи, — а значок ростом с подпись (LabelH = HintBadgeSize)
    /// именно туда и попадает.</summary>
    private static bool Contains(Rect r, Vector2 p) =>
        p.x >= r.xMin && p.x <= r.xMax && p.y >= r.yMin && p.y <= r.yMax;

    private static KitchenElement SpawnWall()
    {
        var go = ElementFactory.CreateWall(new Vector3Int(3000, 2500, 100), "Стена", Vector3.zero);
        var element = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(element, "фабрика стены обязана вернуть объект с KitchenElement");
        Assert.IsNotNull(go.GetComponent<Wall>(), "у стены обязан быть компонент Wall");
        return element!;
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;
}
