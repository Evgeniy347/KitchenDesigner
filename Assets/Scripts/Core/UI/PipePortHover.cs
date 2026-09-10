using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core.UI
{
    internal sealed class PipePortHover
    {
        private readonly Func<KitchenElement?> _owner;
        private readonly IReadOnlyList<PipeNodeKind> _choices;
        private readonly Action<KitchenElement, int> _paint;

        public PipePortHover(Func<KitchenElement?> owner, IReadOnlyList<PipeNodeKind> choices,
            Action<KitchenElement, int> paint)
        {
            _owner = owner;
            _choices = choices;
            _paint = paint;
        }

        public void Guard(GameObject root) => HoverGuard.Attach(root, Clear);

        public void Watch(TMP_Dropdown? dropdown, int port)
        {
            if (dropdown == null) return;
            PointerHover.Attach(dropdown.gameObject, () => Enter(port), Clear);
            DropdownHover.Attach(dropdown, option => EnterOption(port, option), Clear);
        }

        public void WatchSlot(GameObject slot, int port) =>
            PointerHover.Attach(slot, () => Enter(port), Clear);

        public void Enter(int port)
        {
            HoverPreviewGate.HideAll();
            var owner = _owner();
            if (owner == null) return;
            _paint(owner, port);
        }

        public void EnterOption(int port, int option)
        {
            var owner = _owner();
            if (owner == null)
            {
                Clear();
                return;
            }

            _paint(owner, port);

            var scene = PartRegistry.GetAll();
            var replaced = PipeEndFittings.NeighbourAt(owner, port, scene);
            var kind = PipeConnectionRule.KindAt(_choices, option);
            if (!kind.HasValue)
            {
                if (replaced == null) { ScenePreview.Leave(); return; }
                ScenePreview.Hover(NoChoiceKey(owner, port), () => null, replaced, owner);
                return;
            }

            ScenePreview.Hover(PipeEndFittings.PreviewKey(owner, port, kind.Value),
                () => PipeEndFittings.Preview(owner, port, kind.Value, scene), replaced, owner);
        }

        private static string NoChoiceKey(KitchenElement owner, int port) =>
            owner == null ? string.Empty : owner.PartName + ":" + port + ":none";

        public void Clear() => HoverPreviewGate.HideAll();

        public void Commit()
        {
            ScenePreview.Commit();
            PartHighlighter.Hide();
        }
    }
}
