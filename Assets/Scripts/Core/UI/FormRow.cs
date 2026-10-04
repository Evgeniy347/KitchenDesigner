using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class FormRow
    {
        internal FormRow(RectTransform root, float height, CollapsibleSection? section)
        {
            Root = root;
            Height = height;
            Section = section;
        }

        public RectTransform Root { get; }

        public TextMeshProUGUI? Label { get; internal set; }

        public Selectable? Control { get; internal set; }

        public float Height { get; }

        public float GapAfter { get; internal set; }

        public CollapsibleSection? Section { get; }

        public bool IsSectionHeader { get; internal set; }

        public Func<bool>? VisibleWhen { get; set; }

        public bool IsShown => (Section == null || Section.Expanded) && (VisibleWhen == null || VisibleWhen());
    }
}
