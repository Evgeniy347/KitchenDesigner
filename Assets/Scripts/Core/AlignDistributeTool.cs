using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class AlignDistributeTool
    {
        public static void Align(List<KitchenElement> elements, Axis axis, AlignmentMode mode)
        {
            if (elements == null || elements.Count < 2) return;

            Bounds bounds = GetBounds(elements);

            float targetPos;
            switch (mode)
            {
                case AlignmentMode.Min:
                    targetPos = axis == Axis.X ? bounds.min.x : (axis == Axis.Y ? bounds.min.y : bounds.min.z);
                    break;
                case AlignmentMode.Max:
                    targetPos = axis == Axis.X ? bounds.max.x : (axis == Axis.Y ? bounds.max.y : bounds.max.z);
                    break;
                default:
                    targetPos = axis == Axis.X ? bounds.center.x : (axis == Axis.Y ? bounds.center.y : bounds.center.z);
                    break;
            }

            foreach (var e in elements)
            {
                if (e == null) continue;
                Vector3 pos = e.transform.position;
                switch (axis)
                {
                    case Axis.X: pos.x = targetPos; break;
                    case Axis.Y: pos.y = targetPos; break;
                    case Axis.Z: pos.z = targetPos; break;
                }
                e.transform.position = GridManager.SnapToGrid(pos);
            }
        }

        public static void Distribute(List<KitchenElement> elements, Axis axis)
        {
            if (elements == null || elements.Count < 3) return;

            elements.Sort((a, b) =>
            {
                float va = GetAxisValue(a.transform.position, axis);
                float vb = GetAxisValue(b.transform.position, axis);
                return va.CompareTo(vb);
            });

            float first = GetAxisValue(elements[0].transform.position, axis);
            float last = GetAxisValue(elements[elements.Count - 1].transform.position, axis);
            float spacing = (last - first) / (elements.Count - 1);

            for (int i = 1; i < elements.Count - 1; i++)
            {
                Vector3 pos = elements[i].transform.position;
                float target = first + spacing * i;
                switch (axis)
                {
                    case Axis.X: pos.x = target; break;
                    case Axis.Y: pos.y = target; break;
                    case Axis.Z: pos.z = target; break;
                }
                elements[i].transform.position = GridManager.SnapToGrid(pos);
            }
        }

        private static float GetAxisValue(Vector3 v, Axis axis)
        {
            switch (axis)
            {
                case Axis.X: return v.x;
                case Axis.Y: return v.y;
                default: return v.z;
            }
        }

        private static Bounds GetBounds(List<KitchenElement> elements)
        {
            Bounds bounds = new Bounds(elements[0].transform.position, Vector3.zero);
            for (int i = 1; i < elements.Count; i++)
            {
                if (elements[i] != null)
                    bounds.Encapsulate(elements[i].transform.position);
            }
            return bounds;
        }

        public static void RefreshHighlights()
        {
            if (ElementHighlighter.Instance != null)
                ElementHighlighter.Instance.RefreshHighlights();
        }

        public static void HighlightSelected()
        {
            var sel = SelectionManager.Instance;
            if (sel == null) return;
            foreach (var e in sel.SelectedElements)
            {
                if (e != null)
                    Debug.Log("[Align] Selection: " + e.Describe());
            }
        }
    }

    public enum Axis { X, Y, Z }
    public enum AlignmentMode { Min, Center, Max }
}
