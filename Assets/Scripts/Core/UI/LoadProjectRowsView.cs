using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal sealed class LoadProjectRowsView
    {
        internal const float RowHeight = 48f;
        internal const float RowGap = 6f;

        private static readonly Color RowNormalColor = UIStyle.SurfaceInactive;
        private static readonly Color RowMissingColor = UIStyle.RowError;

        internal void Rebuild(RectTransform content, float rowWidth, IReadOnlyList<string> paths,
            Action<string> onOpen)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                DestroyNow.The(content.GetChild(i).gameObject);

            float y = 0f;
            foreach (var path in paths)
            {
                BuildRow(content, rowWidth, y, RecentProjectRowSource.For(path), onOpen);
                y -= RowHeight + RowGap;
            }

            if (paths.Count == 0)
                BuildEmptyHint(content, rowWidth);

            content.sizeDelta = new Vector2(0, paths.Count == 0 ? RowHeight : -y);
        }

        private static void BuildEmptyHint(RectTransform content, float rowWidth)
        {
            var label = UIFactory.CreateLabel("LoadEmptyHint", content,
                Loc.T("window.load.noRecent"), UIStyle.FontBody,
                new Vector2(0, 0), new Vector2(rowWidth, RowHeight), TextAnchor.MiddleCenter);
            label.color = UIStyle.TextSecondary;
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
        }

        private const float RowSidePad = 10f;
        private const float MetaColGap = 6f;
        private const float CreatedColFraction = 0.39f;
        private const float VersionColFraction = 0.22f;

        private static void BuildRow(RectTransform content, float rowWidth, float y,
            RecentProjectRow row, Action<string> onOpen)
        {
            var rowRect = UIFactory.CreateRect("Row", content);
            AnchorTopLeftOfRow(rowRect);
            rowRect.anchoredPosition = new Vector2(0, y);
            rowRect.sizeDelta = new Vector2(rowWidth, RowHeight);

            var bg = rowRect.gameObject.AddComponent<Image>();
            bg.color = row.FileExists ? RowNormalColor : RowMissingColor;

            var button = rowRect.gameObject.AddComponent<UIButton>();
            button.targetGraphic = bg;
            button.colors = UIFactory.InteractiveColors();
            button.interactable = row.FileExists;
            button.onClick.AddListener(() =>
            {
                if (row.FileExists) onOpen(row.Path);
            });

            string fileName = System.IO.Path.GetFileName(row.Path);
            string title = row.FileExists ? fileName : (Loc.T("window.load.notFound") + fileName);
            var titleLabel = UIFactory.CreateLabel("Title", rowRect, title, UIStyle.FontBody,
                new Vector2(RowSidePad, -4), new Vector2(rowWidth - 2 * RowSidePad, 22), TextAnchor.UpperLeft);
            titleLabel.color = row.FileExists ? UIStyle.Text : UIStyle.HighlightError;
            titleLabel.overflowMode = TextOverflowModes.Ellipsis;
            titleLabel.enableWordWrapping = false;
            AnchorTopLeftOfRow(titleLabel.rectTransform);

            if (!row.FileExists) return;

            BuildMetaColumns(rowRect, rowWidth, row);
        }

        private static void BuildMetaColumns(RectTransform rowRect, float rowWidth, RecentProjectRow row)
        {
            float available = rowWidth - 2 * RowSidePad - 2 * MetaColGap;
            float createdW = available * CreatedColFraction;
            float versionW = available * VersionColFraction;
            float modifiedW = available - createdW - versionW;

            var createdLabel = BuildMetaColumn(rowRect, "Created", Loc.T("window.load.created") + row.CreatedLabel,
                RowSidePad, createdW, UIStyle.TextSecondary);

            float versionX = RowSidePad + createdW + MetaColGap;
            string versionText = Loc.T("window.load.version") + (row.VersionMismatch ? "! " : "") + row.VersionLabel;
            BuildMetaColumn(rowRect, "Version", versionText, versionX, versionW,
                row.VersionMismatch ? UIStyle.HighlightError : UIStyle.TextSecondary);

            float modifiedX = versionX + versionW + MetaColGap;
            BuildMetaColumn(rowRect, "Modified", Loc.T("window.load.modified") + row.ModifiedLabel,
                modifiedX, modifiedW, UIStyle.TextSecondary);
        }

        private static TMP_Text BuildMetaColumn(RectTransform rowRect, string name, string text,
            float x, float width, Color color)
        {
            var label = UIFactory.CreateLabel(name, rowRect, text, UIStyle.FontSmall,
                new Vector2(x, -26), new Vector2(width, 18), TextAnchor.UpperLeft);
            label.color = color;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableWordWrapping = false;
            AnchorTopLeftOfRow(label.rectTransform);
            return label;
        }

        private static void AnchorTopLeftOfRow(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
        }
    }
}
