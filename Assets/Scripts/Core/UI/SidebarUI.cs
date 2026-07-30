using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Левый сайдбар добавления объектов: сворачивается в узкую полосу
    /// кнопкой-переключателем, закрепляется булавкой (откреплён — авто-сворачивание
    /// по клику вне панели). Группы-аккордеоны берутся из SidebarCatalog.</summary>
    public class SidebarUI : MonoBehaviour
    {
        private const float ExpandedW = 220f;
        private const float CollapsedW = 52f;
        private const float TopOffset = 52f; // под верхним тулбаром

        private const float Pad = 8f;
        private const float HeaderH = 30f;
        /// <summary>Ширина пункта: панель минус паддинги и отступ вложенности.</summary>
        public const float ItemW = ExpandedW - 2f * Pad - 14f;
        /// <summary>Однострочный пункт — высота как была до переноса.</summary>
        private const float ItemH = 26f;
        private const float ItemPadH = 8f; // поля текста слева/справа
        private const float ItemPadV = 8f;
        private const int ItemMaxLines = 2;
        public const int ItemFont = UIStyle.FontSmall;
        /// <summary>Запас в два глифа при оценке ширины: она считается по средней
        /// букве, а у названия из широких («Ш», «Ж») реальная строка длиннее.
        /// Без запаса пограничное имя («Духовка Bosch HBA514BB3») считается
        /// однострочным, а TMP переносит его — и вторая строка обрезается.</summary>
        private const float ItemGlyphReserve = 2f;

        private RectTransform? _panel;
        private GameObject? _fullRoot;
        private GameObject? _miniRoot;
        private TMP_Text? _collapseLabel;
        private Image? _pinBg;

        private bool _expanded = true;
        private bool _pinned = true;

        private class GroupUI
        {
            public RectTransform? header;
            public readonly List<RectTransform> items = new List<RectTransform>();
            public bool open = true;
        }
        private readonly List<GroupUI> _groups = new List<GroupUI>();

        // Пункты, которые режим редактора делает серыми и некликабельными.
        private class ItemStyle
        {
            public Button button = null!;
            public TMP_Text? label;
            public Color baseColor;
            public EditModeManager.Category cat;
        }
        private readonly List<ItemStyle> _itemStyles = new List<ItemStyle>();

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("Sidebar", canvas, new Vector2(0, -TopOffset),
                new Vector2(ExpandedW, 1000f));
            UIFactory.AnchorTopLeft(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(0, -TopOffset);
            _panel = panel.rectTransform;

            var collapseBtn = UIFactory.CreateButton("SbCollapse", _panel, "«",
                new Vector2(4, -4), new Vector2(36, 28), () => SetExpanded(!_expanded));
            UIFactory.AnchorTopLeft(collapseBtn.GetComponent<RectTransform>());
            collapseBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(4, -4);
            _collapseLabel = collapseBtn.GetComponentInChildren<TMP_Text>();

            var pinBtn = UIFactory.CreateIconButton("SbPin", _panel, IconFactory.Pin,
                new Vector2(46, -4), new Vector2(28, 28), TogglePin);
            UIFactory.AnchorTopLeft(pinBtn.GetComponent<RectTransform>());
            pinBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(46, -4);
            _pinBg = pinBtn.GetComponent<Image>();

            _fullRoot = CreateRoot("SbFull", new Vector2(0, -36), new Vector2(ExpandedW, 960f));
            BuildFull();

            _miniRoot = CreateRoot("SbMini", new Vector2(0, -36), new Vector2(CollapsedW, 960f));
            BuildMini();

            ApplyState();

            EditModeManager.Changed += ApplyModeStyling;
            ApplyModeStyling();
        }

        private void OnDestroy()
        {
            EditModeManager.Changed -= ApplyModeStyling;
        }

        private GameObject CreateRoot(string name, Vector2 pos, Vector2 size)
        {
            var rt = UIFactory.CreateRect(name, _panel!);
            UIFactory.AnchorTopLeft(rt);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt.gameObject;
        }

        /// <summary>Высота пункта палитры: длинное название переносится по словам
        /// (до двух строк), и кнопка растёт под него. Раньше высота была жёстко
        /// 26 px, а перенос TMP включён по умолчанию — вторая строка рисовалась
        /// ЗА кнопкой и налезала на соседний пункт («Варочная Bosch PUE611BB5E»).
        /// Чистая функция: считается без метрик шрифта, см. DropdownItemFit.</summary>
        public static float ItemHeight(string name)
        {
            int lines = ItemLines(name);
            return Mathf.Max(ItemH, Mathf.Ceil(lines * ItemFont * DropdownItemFit.LineHeightFactor + ItemPadV));
        }

        /// <summary>Сколько строк займёт название в пункте палитры (не больше двух).</summary>
        public static int ItemLines(string name)
        {
            float textW = ItemW - 2f * ItemPadH - ItemGlyphReserve * ItemFont * DropdownItemFit.GlyphWidthFactor;
            return DropdownItemFit.LinesFor(name, textW, ItemFont, ItemMaxLines);
        }

        private void BuildFull()
        {
            foreach (var g in SidebarCatalog.Build())
            {
                var gu = new GroupUI();
                var header = UIFactory.CreateButton("SbGrp_" + g.title, _fullRoot!.transform, g.title,
                    Vector2.zero, new Vector2(ExpandedW - 2 * Pad, HeaderH), () => ToggleGroup(gu));
                UIFactory.AnchorTopLeft(header.GetComponent<RectTransform>());
                gu.header = header.GetComponent<RectTransform>();

                var headerLabel = header.GetComponentInChildren<TMP_Text>();
                if (headerLabel != null)
                {
                    headerLabel.enableWordWrapping = false;
                    headerLabel.overflowMode = TextOverflowModes.Ellipsis;
                }

                foreach (var it in g.items)
                {
                    var item = it; // фиксируем для замыкания
                    var btn = UIFactory.CreateButton("SbItem_" + g.title + "_" + it.name, _fullRoot.transform,
                        it.name, Vector2.zero, new Vector2(ItemW, ItemHeight(it.name)), () => Spawn(item));
                    UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
                    gu.items.Add(btn.GetComponent<RectTransform>());

                    var style = new ItemStyle { button = btn, cat = ItemCategory(item) };
                    style.label = btn.GetComponentInChildren<TMP_Text>();
                    if (style.label != null)
                    {
                        style.label.fontSize = ItemFont;
                        style.label.enableWordWrapping = true;
                        // Страховка: оценка ширины приблизительная, и если TMP
                        // возьмёт строку сверх расчёта — она обрежется внутри
                        // кнопки, а не наедет на соседа.
                        style.label.overflowMode = TextOverflowModes.Truncate;
                        style.label.margin = new Vector4(ItemPadH, 2f, ItemPadH, 2f);
                        style.baseColor = style.label.color;
                    }
                    TooltipUI.Attach(btn.gameObject, it.name);
                    _itemStyles.Add(style);
                }
                _groups.Add(gu);
            }
            RelayoutFull();
        }

        /// <summary>Категория пункта каталога для режима редактора: стена/пол —
        /// «помещение»; короб/окно/дверь — всегда доступны; остальное — обычные.</summary>
        private static EditModeManager.Category ItemCategory(SidebarCatalog.Item it)
        {
            if (it.isWindow || it.isDoor) return EditModeManager.Category.Always;
            if (it.name == EditModeManager.KorobName) return EditModeManager.Category.Always;
            if (it.isWall || it.isFloor) return EditModeManager.Category.Room;
            return EditModeManager.Category.Regular;
        }

        // Серые + некликабельные пункты для объектов, недоступных в текущем режиме.
        private void ApplyModeStyling()
        {
            foreach (var s in _itemStyles)
            {
                bool active = EditModeManager.IsCategoryActive(s.cat);
                s.button.interactable = active;
                if (s.label != null)
                    s.label.color = active ? s.baseColor : UIStyle.TextSecondary;
            }
        }

        private void RelayoutFull()
        {
            float y = -Pad;
            foreach (var gu in _groups)
            {
                gu.header!.anchoredPosition = new Vector2(Pad, y);
                y -= gu.header.sizeDelta.y + 4f;
                foreach (var item in gu.items)
                {
                    item.gameObject.SetActive(gu.open);
                    if (gu.open)
                    {
                        item.anchoredPosition = new Vector2(Pad + 14f, y);
                        // Шаг — по фактической высоте: у двухстрочного пункта она больше.
                        y -= item.sizeDelta.y + 3f;
                    }
                }
            }
        }

        private void BuildMini()
        {
            const float pad = 6f;
            float y = -pad;
            int index = 0;
            foreach (var g in SidebarCatalog.Build())
            {
                int i = index;
                var btn = UIFactory.CreateButton("SbMini_" + g.title, _miniRoot!.transform, g.shortLabel,
                    Vector2.zero, new Vector2(CollapsedW - 2 * pad, 38f), () => OpenGroup(i));
                UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
                btn.GetComponent<RectTransform>().anchoredPosition = new Vector2(pad, y);
                y -= 42f;
                index++;
            }
        }

        private void ToggleGroup(GroupUI gu)
        {
            gu.open = !gu.open;
            RelayoutFull();
        }

        private void OpenGroup(int index)
        {
            SetExpanded(true);
            if (index >= 0 && index < _groups.Count)
            {
                _groups[index].open = true;
                RelayoutFull();
            }
        }

        private void Spawn(SidebarCatalog.Item item)
        {
            if (UIManager.Instance == null) return;
            if (item.isFloor)
            {
                UIManager.Instance.SpawnFloor(item.dims, item.name);
                return;
            }
            if (item.isLightSource)
            {
                UIManager.Instance.SpawnLightSource(item.name);
                return;
            }
            if (item.isSink)
            {
                UIManager.Instance.SpawnSink(item.name);
                return;
            }
            if (item.isCooktop)
            {
                UIManager.Instance.SpawnCooktop(item.name, item.applianceModel);
                return;
            }
            if (item.isOven)
            {
                UIManager.Instance.SpawnOven(item.name);
                return;
            }
            if (item.isDishwasher)
            {
                UIManager.Instance.SpawnDishwasher(item.name);
                return;
            }
            if (item.isDrawer)
                UIManager.Instance.SpawnDrawer(item.drawerType, item.drawerLength, item.drawerColor, item.drawerWidth, item.name,
                    item.drawerSystem == "movento" ? DrawerSystem.Movento : DrawerSystem.Gtv);
            else if (item.isWindow)
                UIManager.Instance.SpawnWindow(item.dims, item.name);
            else if (item.isDoor)
                UIManager.Instance.SpawnDoor(item.dims, item.name);
            else if (item.isRadiusTable)
                UIManager.Instance.SpawnRadiusTable(item.dims, item.name);
            else if (item.isFurniture)
                UIManager.Instance.SpawnTable(item.dims, item.name);
            else if (item.isPillar)
                UIManager.Instance.SpawnPillar(item.pillarMidHeightMM, item.name);
            else if (item.isPanel)
                UIManager.Instance.SpawnPanel(item.dims, item.name,
                    item.gapLeft, item.gapRight, item.gapTop, item.gapBottom);
            else if (item.isRadialShelf)
                UIManager.Instance.SpawnRadialShelf(item.dims, item.name);
            else if (item.isAssembled)
                UIManager.Instance.SpawnAssembledFacade(item.dims, item.name);
            else if (item.isFacade)
                UIManager.Instance.SpawnFacade(item.dims, item.name, item.gapLeft, item.gapRight, item.gapTop, item.gapBottom);
            else if (item.isWall)
                UIManager.Instance.SpawnWall(item.dims, item.name);
            else
                UIManager.Instance.SpawnBoard(item.dims, item.name);
        }

        private void SetExpanded(bool expanded)
        {
            _expanded = expanded;
            ApplyState();
        }

        private void TogglePin()
        {
            _pinned = !_pinned;
            ApplyState();
        }

        private void ApplyState()
        {
            var size = _panel!.sizeDelta;
            size.x = _expanded ? ExpandedW : CollapsedW;
            _panel.sizeDelta = size;

            _fullRoot!.SetActive(_expanded);
            _miniRoot!.SetActive(!_expanded);
            if (_collapseLabel != null) _collapseLabel.text = _expanded ? "«" : "»";
            if (_pinBg != null)
            {
                _pinBg.gameObject.SetActive(_expanded); // булавка видна только в развёрнутом
                _pinBg.color = _pinned ? new Color(0.30f, 0.55f, 0.34f, 1f) : UIFactory.ButtonColor;
            }
        }

        // Откреплённый сайдбар сворачивается при клике вне его области.
        private void Update()
        {
            using var _ = PerfMarkers.SidebarUpdate.Auto();
            if (_pinned || !_expanded) return;
            if (Input.GetMouseButtonDown(0) &&
                !RectTransformUtility.RectangleContainsScreenPoint(_panel!, Input.mousePosition, null))
            {
                SetExpanded(false);
            }
        }
    }
}
