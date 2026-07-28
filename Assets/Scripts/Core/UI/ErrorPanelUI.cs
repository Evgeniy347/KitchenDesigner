using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.Analysis;

namespace KitchenDesigner.Core.UI
{
    /// <summary>
    /// Окно «Ошибки»: таблица проблем сцены (уровень, код, деталь, текст) с
    /// анализом по требованию. Источник данных — <see cref="SceneAnalyzer"/>
    /// (сейчас коллизии). Фильтры: мультиселект по уровням, мультиселект по кодам
    /// и один полнотекстовый поиск. Анализ выполняется при открытии и по кнопке
    /// «Обновить»; переключение фильтров только перестраивает строки.
    /// </summary>
    public class ErrorPanelUI : MonoBehaviour, IProjectWindow
    {
        private const float PanelW = 940f;
        private const float PanelH = 640f;
        private const float Pad = 16f;
        private const float RowH = 24f;
        private const float RowStep = 26f;

        // Смещения колонок относительно левого края контента.
        private const float ColLevel = 0f;
        private const float ColCode = 110f;
        private const float ColDetail = 205f;
        private const float ColMessage = 470f;

        private static readonly float ContentW = PanelW - Pad * 2f - 14f; // минус скроллбар
        private static readonly float ColMessageW = ContentW - ColMessage;

        private static readonly Color RowZebra = UIStyle.Field;

        private GameObject? _root;
        private RectTransform? _content;
        private MultiSelectDropdown? _levelFilter;
        private MultiSelectDropdown? _codeFilter;
        private TMP_InputField? _searchField;
        private TMP_Text? _searchHint;
        private TMP_Text? _countLabel;

        private readonly List<AnalysisIssue> _allIssues = new List<AnalysisIssue>();
        private int _lastSceneVersion;

        /// <summary>Сколько строк сейчас в таблице (без учёта фильтров — все issues).</summary>
        public int TotalIssueCount => _allIssues.Count;

        /// <summary>Сколько строк видно в таблице (с учётом фильтров).</summary>
        public int VisibleIssueCount => _content != null ? _content.childCount : 0;

        public void Build(Transform layer)
        {
            var panel = UIFactory.CreatePanel("ErrorPanel", layer, Vector2.zero, new Vector2(PanelW, PanelH));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;
            WindowDrag.Attach(panel.rectTransform, 40f);
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("ErrTitle", panel.transform, "Ошибки", UIStyle.FontTitle,
                new Vector2(Pad, -10), new Vector2(200, 28), TextAnchor.MiddleLeft)
                .rectTransform.SetAnchor(new Vector2(0, 1), new Vector2(Pad, -10));

            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            BuildFilters(panel.transform);
            BuildHeader(panel.transform);
            BuildScrollArea(panel.transform);

            _countLabel = UIFactory.CreateLabel("ErrCount", panel.transform, "", UIStyle.FontSmall,
                Vector2.zero, new Vector2(ContentW, 22), TextAnchor.MiddleLeft);
            _countLabel.color = UIStyle.TextSecondary;
            _countLabel.rectTransform.SetAnchor(new Vector2(0, 1), new Vector2(Pad, -(PanelH - 28f)));

            _root.SetActive(false);
        }

        private void BuildFilters(Transform parent)
        {
            const float labelY = -44f;
            const float fieldY = -64f;
            const float fieldH = 28f;

            // Уровень — мультиселект.
            Caption(parent, "FltLevelLbl", "Уровень", new Vector2(Pad, labelY));
            _levelFilter = MultiSelectDropdown.Create("FltLevel", parent, "Все",
                new Vector2(Pad, fieldY), new Vector2(160, fieldH), RebuildRows);

            // Код — мультиселект.
            Caption(parent, "FltCodeLbl", "Код ошибки", new Vector2(Pad + 176, labelY));
            _codeFilter = MultiSelectDropdown.Create("FltCode", parent, "Все",
                new Vector2(Pad + 176, fieldY), new Vector2(160, fieldH), RebuildRows);

            // Полнотекстовый поиск.
            Caption(parent, "FltSearchLbl", "Поиск по тексту", new Vector2(Pad + 352, labelY));
            _searchField = UIFactory.CreateInputField("FltSearch", parent, "",
                new Vector2(Pad + 352, fieldY), new Vector2(300, fieldH));
            UIFactory.AnchorTopLeft(_searchField.GetComponent<RectTransform>());
            _searchField.GetComponent<RectTransform>().anchoredPosition = new Vector2(Pad + 352, fieldY);
            _searchField.onValueChanged.AddListener(_ => RebuildRows());
            var hint = UIFactory.CreateLabel("FltSearchHint", _searchField.transform, "Код, деталь или текст…",
                UIStyle.FontSmall, Vector2.zero, new Vector2(280, fieldH), TextAnchor.MiddleLeft);
            hint.color = UIStyle.TextSecondary;
            hint.raycastTarget = false;
            var hRt = hint.rectTransform;
            hRt.anchorMin = Vector2.zero; hRt.anchorMax = Vector2.one;
            hRt.offsetMin = new Vector2(8, 0); hRt.offsetMax = Vector2.zero;
            _searchHint = hint;

            // Обновить — перезапуск анализа.
            var refresh = UIFactory.CreateButton("ErrRefresh", parent, "Обновить",
                new Vector2(PanelW - Pad - 120f, fieldY), new Vector2(120, fieldH), Analyze);
            UIFactory.AnchorTopLeft(refresh.GetComponent<RectTransform>());
            refresh.GetComponent<RectTransform>().anchoredPosition = new Vector2(PanelW - Pad - 120f, fieldY);
        }

        private static void Caption(Transform parent, string name, string text, Vector2 pos)
        {
            var lbl = UIFactory.CreateLabel(name, parent, text, UIStyle.FontSection,
                Vector2.zero, new Vector2(170, 18), TextAnchor.MiddleLeft);
            lbl.color = UIStyle.TextSecondary;
            lbl.rectTransform.SetAnchor(new Vector2(0, 1), pos);
        }

        private void BuildHeader(Transform parent)
        {
            var header = UIFactory.CreateRect("ErrHeader", parent);
            UIFactory.AnchorTopLeft(header);
            header.sizeDelta = new Vector2(ContentW, 22);
            header.anchoredPosition = new Vector2(Pad, -102);

            HeaderCell(header, "Уровень", ColLevel, ColCode - ColLevel);
            HeaderCell(header, "Код", ColCode, ColDetail - ColCode);
            HeaderCell(header, "Деталь", ColDetail, ColMessage - ColDetail);
            HeaderCell(header, "Текст ошибки", ColMessage, ColMessageW);

            var sep = UIFactory.CreateRect("ErrHeaderSep", parent);
            UIFactory.AnchorTopLeft(sep);
            sep.sizeDelta = new Vector2(ContentW, 2);
            sep.anchoredPosition = new Vector2(Pad, -124);
            sep.gameObject.AddComponent<Image>().color = UIStyle.Separator;
        }

        private static void HeaderCell(RectTransform header, string text, float x, float w)
        {
            var lbl = UIFactory.CreateLabel("H_" + text, header, text, UIStyle.FontSmall,
                Vector2.zero, new Vector2(w, 22), TextAnchor.MiddleLeft);
            lbl.color = UIStyle.TextSecondary;
            var rt = lbl.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, 0);
        }

        private void BuildScrollArea(Transform parent)
        {
            var viewport = UIFactory.CreateRect("ErrViewport", parent);
            viewport.anchorMin = new Vector2(0, 0);
            viewport.anchorMax = new Vector2(1, 1);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.offsetMin = new Vector2(Pad, 40);          // нижний паддинг под счётчик
            viewport.offsetMax = new Vector2(-(Pad + 14f), -130); // шапка сверху, скроллбар справа
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.01f);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _content = UIFactory.CreateRect("ErrContent", viewport);
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            var sbRect = UIFactory.CreateRect("ErrScrollbar", parent);
            sbRect.anchorMin = new Vector2(1, 0);
            sbRect.anchorMax = new Vector2(1, 1);
            sbRect.pivot = new Vector2(1, 0.5f);
            sbRect.offsetMin = new Vector2(-(Pad - 2f), 40);
            sbRect.offsetMax = new Vector2(-4, -130);
            sbRect.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.13f, 0.6f);
            var scrollbar = sbRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = UIFactory.CreateRect("Handle", sbRect);
            handle.sizeDelta = new Vector2(8, 100);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = new Color(0.38f, 0.40f, 0.46f, 1f);
            scrollbar.targetGraphic = handleImg;
            scrollbar.handleRect = handle;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        // ── Автообновление ──────────────────────────────────────────────

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            if (_lastSceneVersion == SceneRevision.Version) return;
            Analyze();
        }

        // ── Видимость ───────────────────────────────────────────────────

        public string WindowId => "errors";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;

        private void OnDestroy() => ProjectWindows.Unregister(this);

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            if (visible) Analyze();
            _root.SetActive(visible);
        }

        // ── Анализ и отрисовка ──────────────────────────────────────────

        private void Analyze()
        {
            _allIssues.Clear();
            _allIssues.AddRange(SceneAnalyzer.Analyze());
            _lastSceneVersion = SceneRevision.Version;

            // Опции фильтров — только реально встречающиеся значения.
            var levels = new List<string>();
            foreach (IssueLevel lv in System.Enum.GetValues(typeof(IssueLevel)))
            {
                string name = LevelName(lv);
                foreach (var iss in _allIssues)
                    if (iss.Level == lv) { levels.Add(name); break; }
            }
            var codes = new List<string>();
            foreach (var iss in _allIssues)
                if (!codes.Contains(iss.Code)) codes.Add(iss.Code);
            codes.Sort(System.StringComparer.Ordinal);

            _levelFilter?.SetOptions(levels);
            _codeFilter?.SetOptions(codes);

            RebuildRows();
        }

        private void RebuildRows()
        {
            if (_content == null) return;

            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            string search = _searchField != null ? _searchField.text.Trim() : "";
            if (_searchHint != null) _searchHint.gameObject.SetActive(search.Length == 0);

            float y = 0f;
            int shown = 0;
            foreach (var iss in _allIssues)
            {
                if (!PassesFilters(iss, search)) continue;
                BuildRow(iss, y, shown);
                y -= RowStep;
                shown++;
            }

            _content.sizeDelta = new Vector2(0, -y + 4f);

            if (_countLabel != null)
            {
                if (_allIssues.Count == 0)
                    _countLabel.text = "Ошибок не найдено";
                else if (shown == _allIssues.Count)
                    _countLabel.text = $"Всего: {_allIssues.Count}";
                else
                    _countLabel.text = $"Показано: {shown} из {_allIssues.Count}";
            }
        }

        private bool PassesFilters(AnalysisIssue iss, string search)
        {
            if (_levelFilter != null && !_levelFilter.IsAllowed(LevelName(iss.Level))) return false;
            if (_codeFilter != null && !_codeFilter.IsAllowed(iss.Code)) return false;
            if (search.Length == 0) return true;

            return Contains(iss.Code, search) || Contains(iss.Detail, search) || Contains(iss.Message, search);
        }

        private static bool Contains(string haystack, string needle) =>
            haystack != null && haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private void BuildRow(AnalysisIssue iss, float y, int index)
        {
            // Строка — кнопка: клик выделяет связанные детали в сцене,
            // двойной клик — ещё и фокусирует камеру на них (как клавиша F).
            var btn = UIFactory.CreateButton("Row", _content!, "", Vector2.zero,
                new Vector2(ContentW, RowH), () => SelectIssue(iss));
            var dbl = btn.gameObject.AddComponent<ListRowDoubleClick>();
            dbl.OnDoubleClick = () => RevealIssue(iss);
            var row = btn.GetComponent<RectTransform>();
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0, y);
            row.sizeDelta = new Vector2(0, RowH);
            // Зебра для читаемости; чётные строки — прозрачный фон.
            btn.GetComponent<Image>().color = index % 2 == 1 ? RowZebra : new Color(0, 0, 0, 0.01f);
            // Пустая авто-подпись кнопки не нужна — ячейки рисуем сами.
            var autoLabel = btn.transform.Find("Row_Label");
            if (autoLabel != null) Destroy(autoLabel.gameObject);

            Cell(row, LevelName(iss.Level), ColLevel, ColCode - ColLevel, LevelColor(iss.Level));
            Cell(row, iss.Code, ColCode, ColDetail - ColCode, UIStyle.Text);
            Cell(row, iss.Detail, ColDetail, ColMessage - ColDetail, UIStyle.Text);
            Cell(row, iss.Message, ColMessage, ColMessageW, UIStyle.Text);
        }

        /// <summary>Выделить детали проблемы и навести на них камеру. Тот же путь,
        /// что двойной клик по строке; используется и кнопкой тулбара.</summary>
        public static void RevealIssue(AnalysisIssue iss)
        {
            SelectIssue(iss);
            FocusOnIssue(iss);
        }

        private static void SelectIssue(AnalysisIssue iss)
        {
            var sel = SelectionManager.Instance;
            if (sel == null || iss.Target == null) return;

            if (iss.Secondary != null)
                sel.SelectOnly(new List<KitchenElement> { iss.Target, iss.Secondary });
            else
                sel.Select(iss.Target);
        }

        private static void FocusOnIssue(AnalysisIssue iss)
        {
            if (iss.Target == null) return;
            CameraController.Instance?.FocusOn(iss.Target.transform.position);
        }

        private static void Cell(RectTransform row, string text, float x, float w, Color color)
        {
            var lbl = UIFactory.CreateLabel("Cell", row, text, UIStyle.FontSmall,
                Vector2.zero, new Vector2(w, RowH), TextAnchor.MiddleLeft);
            lbl.color = color;
            lbl.raycastTarget = false;
            lbl.enableWordWrapping = false;
            lbl.overflowMode = TextOverflowModes.Ellipsis;
            var rt = lbl.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(x + 4f, 0);
        }

        private static string LevelName(IssueLevel level) => level switch
        {
            IssueLevel.Error => "Ошибка",
            IssueLevel.Warning => "Предупреждение",
            IssueLevel.Info => "Инфо",
            _ => level.ToString(),
        };

        /// <summary>Цвет уровня проблемы: таблица окна, значок тулбара, статус-бар.</summary>
        public static Color LevelColor(IssueLevel level) => level switch
        {
            IssueLevel.Error => UIStyle.HighlightError,
            IssueLevel.Warning => UIStyle.HighlightWarning,
            IssueLevel.Info => UIStyle.TextSecondary,
            _ => UIStyle.Text,
        };
    }
}
