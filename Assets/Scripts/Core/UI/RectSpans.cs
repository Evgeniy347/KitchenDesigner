using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class RectSpans
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        public static void Collect(RectTransform region, List<ContentSpan> into)
        {
            into.Clear();
            float regionTop = region.rect.yMax;
            for (int i = 0; i < region.childCount; i++)
                Descend(region.GetChild(i), region, regionTop, into);
        }

        private static void Descend(Transform node, RectTransform region, float regionTop,
            List<ContentSpan> into)
        {
            if (!node.gameObject.activeSelf) return;

            if (node is RectTransform rect)
            {
                into.Add(SpanOf(rect, region, regionTop));
                if (ClipsItsOwnContent(rect.gameObject)) return;
            }

            for (int i = 0; i < node.childCount; i++)
                Descend(node.GetChild(i), region, regionTop, into);
        }

        private static bool ClipsItsOwnContent(GameObject go) =>
            go.GetComponent<ScrollRect>() != null
            || go.GetComponent<Mask>() != null
            || go.GetComponent<RectMask2D>() != null
            || go.GetComponent<TMP_InputField>() != null;

        private static ContentSpan SpanOf(RectTransform rect, RectTransform region, float regionTop)
        {
            rect.GetWorldCorners(Corners);
            float top = regionTop - region.InverseTransformPoint(Corners[1]).y;
            float bottom = regionTop - region.InverseTransformPoint(Corners[0]).y;
            return new ContentSpan(rect.name, top, bottom);
        }
    }
}
