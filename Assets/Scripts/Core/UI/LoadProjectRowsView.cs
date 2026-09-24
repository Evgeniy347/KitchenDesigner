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
                "Недавних проектов пока нет", UIStyle.FontBody,
                new Vector2(0, 0), new Vector2(rowWidth, RowHeight), TextAnchor.MiddleCenter);
            label.color = UIStyle.TextSecondary;
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void BuildRow(RectTransform content, float rowWidth, float y,
            RecentProjectRow row, Action<string> onOpen)
        {
            var rowRect = UIFactory.CreateRect("Row", content);
            rowRect.anchorMin = new Vector2(0, 1);
            rowRect.anchorMax = new Vector2(1, 1);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0, y);
            rowRect.sizeDelta = new Vector2(0, RowHeight);

            var bg = rowRect.gameObject.AddComponent<Image>();
            bg.color = row.FileExists ? RowNormalColor : RowMissingColor;

            var button = rowRect.gameObject.AddComponent<UIButton>();
            button.targetGraphic = bg;
            button.colors = UIFactory.InteractiveColors();
            button.onClick.AddListener(() =>
            {
                if (row.FileExists) onOpen(row.Path);
            });

            string fileName = System.IO.Path.GetFileName(row.Path);
            string title = row.FileExists ? fileName : ("! Не найден: " + fileName);
            var titleLabel = UIFactory.CreateLabel("Title", rowRect, title, UIStyle.FontBody,
                new Vector2(10, -4), new Vector2(rowWidth - 20, 22), TextAnchor.UpperLeft);
            titleLabel.color = row.FileExists ? UIStyle.Text : UIStyle.HighlightError;
            titleLabel.overflowMode = TextOverflowModes.Ellipsis;
            titleLabel.enableWordWrapping = false;
            AnchorTopLeftOfRow(titleLabel.rectTransform);

            if (!row.FileExists) return;

            string meta = "Создан " + row.CreatedLabel + "  ·  Изменён " + row.ModifiedLabel + "  ·  Версия ";
            var metaLabel = UIFactory.CreateLabel("Meta", rowRect, meta, UIStyle.FontSmall,
                new Vector2(10, -26), new Vector2(rowWidth - 90, 18), TextAnchor.UpperLeft);
            metaLabel.color = UIStyle.TextSecondary;
            metaLabel.overflowMode = TextOverflowModes.Ellipsis;
            metaLabel.enableWordWrapping = false;
            AnchorTopLeftOfRow(metaLabel.rectTransform);

            metaLabel.ForceMeshUpdate();
            float metaWidth = metaLabel.GetPreferredValues(meta, rowWidth - 90, 18).x;

            string versionText = (row.VersionMismatch ? "! " : "") + row.VersionLabel;
            var versionLabel = UIFactory.CreateLabel("Version", rowRect, versionText, UIStyle.FontSmall,
                new Vector2(10 + metaWidth, -26), new Vector2(90, 18), TextAnchor.UpperLeft);
            versionLabel.color = row.VersionMismatch ? UIStyle.HighlightError : UIStyle.TextSecondary;
            AnchorTopLeftOfRow(versionLabel.rectTransform);
        }

        private static void AnchorTopLeftOfRow(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
        }
    }
}
