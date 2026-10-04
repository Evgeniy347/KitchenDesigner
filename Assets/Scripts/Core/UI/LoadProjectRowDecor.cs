using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class LoadProjectRowDecor
    {
        public const string ForgetNode = "LoadForget";

        private readonly DataTable _table;
        private readonly Func<string, bool> _isCurrent;
        private readonly Action<string> _forget;

        public LoadProjectRowDecor(DataTable table, Func<string, bool> isCurrent, Action<string> forget)
        {
            _table = table;
            _isCurrent = isCurrent;
            _forget = forget;
        }

        public void Decorate(DataRow row, RectTransform rect)
        {
            if (row.Tag is not RecentProjectRow project) return;
            bool current = _isCurrent(project.Path);
            AddBadges(rect, project, current);
            if (!current) AddForget(rect, project.Path);
        }

        private void AddBadges(RectTransform rect, RecentProjectRow project, bool current)
        {
            var name = rect.Find(DataTable.CellNodePrefix + LoadProjectRows.NameColumn);
            if (name == null) return;
            var label = name.GetComponent<TMP_Text>();
            var badges = new System.Collections.Generic.List<RectTransform>();
            if (!project.FileExists)
                badges.Add(RowBadge.Create(rect, "missing", Loc.T("window.load.badge.missing"), UIStyle.TextError,
                    UIStyle.BadgeErrorFill));
            else
            {
                if (current)
                    badges.Add(RowBadge.Create(rect, "current", Loc.T("window.load.badge.current"),
                        UIStyle.AccentText, UIStyle.AccentSubtle));
                if (project.FileNewerThanApp)
                    badges.Add(RowBadge.Create(rect, "newer", Loc.T("window.load.badge.newer"), UIStyle.TextWarning,
                        UIStyle.BadgeWarningFill));
            }
            if (badges.Count == 0) return;

            var rt = label.rectTransform;
            float badgesW = UIStyle.Space2 * badges.Count;
            foreach (var badge in badges) badgesW += badge.sizeDelta.x;
            float shown = Mathf.Min(Mathf.Ceil(label.GetPreferredValues(label.text).x), rt.sizeDelta.x - badgesW);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, shown), rt.sizeDelta.y);

            float x = rt.anchoredPosition.x + rt.sizeDelta.x + UIStyle.Space2;
            foreach (var badge in badges)
            {
                badge.anchoredPosition = new Vector2(x, 0f);
                x += badge.sizeDelta.x + UIStyle.Space2;
            }
        }

        private void AddForget(RectTransform rect, string path)
        {
            int column = LoadProjectRows.ActionColumn;
            var button = UIFactory.CreateButton(ForgetNode, rect, UIStyle.GlyphClose, Vector2.zero,
                new Vector2(UIStyle.ControlHCompact, UIStyle.ControlHCompact), null);
            QuietButton.Apply(button);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.raycastTarget = false;
            var confirm = ConfirmDeleteButton.Attach(button, () => _forget(path));
            confirm.ArmedChanged += armed => label.color = armed ? UIStyle.DangerText : UIStyle.TextSecondary;
            TooltipUI.Attach(button.gameObject, Loc.T("window.load.forget"));

            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(_table.ColumnLeft(column) + _table.ColumnWidth(column) * 0.5f
                - rt.sizeDelta.x * 0.5f, 0f);
        }
    }
}
