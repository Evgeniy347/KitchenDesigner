using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal sealed class LevelsListView
    {
        public const string CurrentMarkerNode = "LvCurrent_";

        private readonly RectTransform _host;
        private readonly float _width;
        private readonly Action<Level> _onDelete;
        private readonly RectTransform _rows;
        private readonly RectTransform _addRect;
        private readonly List<GameObject> _rowObjects = new();

        public LevelsListView(RectTransform host, float width, Action<Level> onDelete)
        {
            _host = host;
            _width = width;
            _onDelete = onDelete;
            BuildHeader();
            _rows = UIFactory.CreateRect("LvRows", host);
            Anchor(_rows, 0f, LevelsColumns.HeaderH, width, 0f);
            var add = UIFactory.CreateButton("LvAdd", host, Loc.T("window.levels.add"), Vector2.zero,
                new Vector2(0f, UIStyle.ControlHCompact), LevelEdits.AddAboveTheTop);
            var label = add.GetComponentInChildren<TMP_Text>();
            add.GetComponent<RectTransform>().sizeDelta = new Vector2(
                WindowFooter.WidthFor(label, label.text), UIStyle.ControlHCompact);
            _addRect = (RectTransform)add.transform;
        }

        public RectTransform Rows => _rows;

        public float Rebuild(IReadOnlyList<Level> levels, string currentId)
        {
            foreach (var go in _rowObjects) DestroyNow.The(go);
            _rowObjects.Clear();

            float y = 0f;
            foreach (var level in levels)
            {
                BuildRow(level, levels.Count, level.id == currentId, y);
                y += LevelsColumns.RowH;
            }

            _rows.sizeDelta = new Vector2(_width, y);
            float addY = LevelsColumns.HeaderH + y + LevelsColumns.AddGap;
            Anchor(_addRect, 0f, addY, _addRect.sizeDelta.x, _addRect.sizeDelta.y);
            return addY + _addRect.sizeDelta.y;
        }

        private void BuildHeader()
        {
            float nameW = LevelsColumns.NameW(_width);
            Header("LvHeaderName", Loc.T("window.levels.name"), LevelsColumns.NameX, nameW, TextAlignmentOptions.Left);
            Header("LvHeaderElevation", Loc.T("window.levels.elevation"), LevelsColumns.ElevationX(_width),
                LevelsColumns.NumberW, TextAlignmentOptions.Right);
            Header("LvHeaderHeight", Loc.T("window.levels.height"), LevelsColumns.HeightX(_width),
                LevelsColumns.NumberW, TextAlignmentOptions.Right);
        }

        private void Header(string name, string text, float x, float width, TextAlignmentOptions align)
        {
            var label = UIFactory.CreateLabel(name, _host, text, UIStyle.FontCaption, Vector2.zero,
                new Vector2(width, LevelsColumns.HeaderH), TextAnchor.MiddleLeft);
            label.color = UIStyle.TextSecondary;
            label.alignment = align;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            bool right = align == TextAlignmentOptions.Right;
            if (right) label.margin = new Vector4(0f, 0f, UIStyle.TableCellPadX, 0f);
            Anchor(label.rectTransform, x, 0f, width, LevelsColumns.HeaderH);
        }

        private void BuildRow(Level level, int count, bool current, float y)
        {
            float fieldY = y + (LevelsColumns.RowH - LevelsColumns.FieldH) * 0.5f;
            float nameW = LevelsColumns.NameW(_width);

            if (current) AddMarker(level, y);

            var name = UIFactory.CreateInputField("LvName_" + level.id, _rows, level.name, Vector2.zero,
                new Vector2(nameW, LevelsColumns.FieldH));
            name.onEndEdit.AddListener(v => LevelEdits.Rename(level, v));
            Place(name.gameObject, LevelsColumns.NameX, fieldY);

            var elevation = NumberField("LvElevation_" + level.id, LevelEdits.Mm(level.floorElevationMm),
                LevelsColumns.ElevationX(_width), fieldY);
            elevation.onEndEdit.AddListener(v => LevelEdits.SetElevation(level, v, elevation));

            var height = NumberField("LvHeight_" + level.id, LevelEdits.Mm(level.heightMm),
                LevelsColumns.HeightX(_width), fieldY);
            height.onEndEdit.AddListener(v => LevelEdits.SetHeight(level, v, height));

            var delete = QuietDeleteButton.Create("LvDelete_" + level.id, _rows, LevelsColumns.DeleteW,
                Loc.T("common.delete"), () => _onDelete(level));
            QuietDeleteButton.SetInteractable(delete, count > 1);
            Place(delete.gameObject, LevelsColumns.DeleteX(_width), y + (LevelsColumns.RowH - LevelsColumns.DeleteW) * 0.5f);
        }

        private TMP_InputField NumberField(string name, string text, float x, float y)
        {
            var field = UIFactory.CreateNumberField(name, _rows, text, Vector2.zero,
                new Vector2(LevelsColumns.NumberW, LevelsColumns.FieldH), Loc.T("unit.mm"));
            field.textComponent!.alignment = TextAlignmentOptions.Right;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            Place(field.gameObject, x, y);
            return field;
        }

        private void AddMarker(Level level, float y)
        {
            var cell = UIFactory.CreateRect(CurrentMarkerNode + level.id, _rows);
            cell.sizeDelta = new Vector2(LevelsColumns.MarkerW, LevelsColumns.RowH);
            var dot = UIFactory.CreateRect("Dot", cell);
            dot.sizeDelta = new Vector2(LevelsColumns.MarkerDot, LevelsColumns.MarkerDot);
            var image = dot.gameObject.AddComponent<Image>();
            RoundedRectSprites.Apply(image, RoundedRectSprites.Fill(LevelsColumns.MarkerDot * 0.5f));
            image.color = UIStyle.SelectionBar;
            image.raycastTarget = false;
            var hit = cell.gameObject.AddComponent<Image>();
            hit.color = UIStyle.RaycastOnly;
            TooltipUI.Attach(cell.gameObject, Loc.T("window.levels.current"));
            Place(cell.gameObject, 0f, y);
            _rowObjects.Add(cell.gameObject);
        }

        private void Place(GameObject go, float x, float y)
        {
            var rt = (RectTransform)go.transform;
            Anchor(rt, x, y, rt.sizeDelta.x, rt.sizeDelta.y);
            if (go.GetComponent<TMP_InputField>() != null || go.GetComponent<Button>() != null) _rowObjects.Add(go);
        }

        private void Anchor(RectTransform rt, float x, float y, float width, float height)
        {
            float left = LayoutDirection.IsRtl ? _width - x - width : x;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(left, -y);
        }
    }
}
