using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class AttachTargetHover
    {
        private readonly TMP_Dropdown _dropdown;
        private readonly string _noneCaption;

        public static AttachTargetHover Watch(TMP_Dropdown dropdown, string noneCaption)
            => new AttachTargetHover(dropdown, noneCaption);

        private AttachTargetHover(TMP_Dropdown dropdown, string noneCaption)
        {
            _dropdown = dropdown;
            _noneCaption = noneCaption;

            DropdownHover.Attach(dropdown, Enter, Leave);
            HoverGuard.Attach(dropdown.gameObject, Leave);
        }

        public void Enter(int option)
        {
            var named = NamedBy(option);
            if (named == null)
            {
                Leave();
                return;
            }

            HoverTint.Show(named, UIStyle.HoverHighlight3D);
        }

        public void Leave() => HoverPreviewGate.HideAll();

        public KitchenElement? NamedBy(int option)
        {
            var options = _dropdown.options;
            if (option < 0 || option >= options.Count) return null;

            var name = options[option].text;
            if (string.IsNullOrEmpty(name) || name == _noneCaption) return null;

            foreach (var element in PartRegistry.All)
                if (element != null && element.PartName == name) return element;
            return null;
        }
    }
}
