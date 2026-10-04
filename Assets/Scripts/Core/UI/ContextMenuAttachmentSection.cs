using System.Collections.Generic;
using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal sealed class ContextMenuAttachmentSection
    {
        private static string DrawerFacadeLabelText => Loc.T("element.common.drawerFront");
        private static string HostFacadeLabelText => Loc.T("element.common.front");
        private static string AttachToLabelText => Loc.T("element.common.attachTo");
        private static string AttachToNoneText => Loc.T("element.common.notAttached");
        private static string FacadeNoneText => Loc.T("element.common.noFront");

        private readonly IContextMenuHost _host;
        private TMP_Text? _drawerFacadeLabel;
        private NameDropdownBinder _facade = null!;
        private NameDropdownBinder _parent = null!;

        public ContextMenuAttachmentSection(IContextMenuHost host) => _host = host;

        public NameDropdownBinder Facade => _facade;

        public NameDropdownBinder Parent => _parent;

        private KitchenElement? Target => _host.Target;

        public void BuildFacade()
        {
            (_drawerFacadeLabel, var facadeDropdown) = _host.Rows.NamedDropdown("CtxDrawerFacade",
                DrawerFacadeLabelText, new List<string> { FacadeNoneText }, _ => { },
                RowVisibility.When(() => Target is IFacadeHost));
            _facade = new NameDropdownBinder(facadeDropdown, FacadeNoneText,
                () => (Target as IFacadeHost)?.AttachedFacadeName ?? "",
                AttachableFacadeNames, AttachedFacadeIsDetached, CommitAttachedFacade);
        }

        public void BuildParent()
        {
            (_, var attachToDropdown) = _host.Rows.NamedDropdown("CtxAttachTo", AttachToLabelText,
                new List<string> { AttachToNoneText }, _ => { },
                RowVisibility.When(() => AttachLinks.CanChooseParent(Target)));
            _parent = new NameDropdownBinder(attachToDropdown, AttachToNoneText,
                () => Target != null ? Target.AttachedToName : "",
                AttachToCandidateNames, () => AttachLinks.IsDetached(Target), CommitAttachedTo);
            AttachTargetHover.Watch(attachToDropdown, AttachToNoneText);
        }

        public void ShowFor(KitchenElement element)
        {
            _parent.Rebuild();
            _parent.SetValue(element.AttachedToName);

            var facadeHost = element as IFacadeHost;
            if (facadeHost != null && _drawerFacadeLabel != null)
                _drawerFacadeLabel.text = _host.TargetFacets.Has(ElementFacet.Drawer)
                    ? DrawerFacadeLabelText : HostFacadeLabelText;
            _facade.Rebuild();
            _facade.SetValue(facadeHost != null ? facadeHost.AttachedFacadeName : "");
        }

        public void RefreshCaptionColor()
        {
            if (AttachLinks.CanBeChild(Target)) _parent.UpdateCaptionColor();
        }

        private IEnumerable<string> AttachableFacadeNames()
        {
            var host = Target as IFacadeHost;
            if (host == null) yield break;
            var attachedName = host.AttachedFacadeName;
            foreach (var facade in FacadeLinks.All())
            {
                if (string.IsNullOrEmpty(facade.PartName)) continue;
                bool isAttached = !string.IsNullOrEmpty(attachedName) && facade.PartName == attachedName;
                if (!isAttached && !DrawerLinks.IsFacadeInContact(host, facade)) continue;
                yield return facade.PartName;
            }
        }

        private bool AttachedFacadeIsDetached()
        {
            var host = Target as IFacadeHost;
            if (host == null) return false;
            var attachedName = host.AttachedFacadeName;
            if (string.IsNullOrEmpty(attachedName)) return false;
            var attached = FacadeLinks.FindByName(attachedName);
            if (attached != null) return !DrawerLinks.IsFacadeInContact(host, attached);
            return true;
        }

        private void CommitAttachedFacade(string name)
        {
            if (!(Target is IFacadeHost host)) return;
            var previous = host.FindAttachedFacade();
            host.AttachedFacadeName = name;
            host.OnAttachedFacadeChanged(previous,
                string.IsNullOrEmpty(name) ? null : host.FindAttachedFacade());
        }

        private IEnumerable<string> AttachToCandidateNames()
        {
            var target = Target;
            if (target == null || !AttachLinks.CanChooseParent(target)) yield break;
            var attachedName = target.AttachedToName;
            foreach (var el in PartRegistry.GetAll())
            {
                if (el == null || el == target || string.IsNullOrEmpty(el.PartName)) continue;
                if (!AttachLinks.CanAttach(target, el)) continue;
                bool isAttached = !string.IsNullOrEmpty(attachedName) && el.PartName == attachedName;
                if (!isAttached && !AttachLinks.InContact(target, el)) continue;
                yield return el.PartName;
            }
        }

        private void CommitAttachedTo(string name)
        {
            var target = Target;
            if (target == null || !AttachLinks.CanChooseParent(target)) return;
            if (name == target.AttachedToName) return;

            var before = UndoableProperties.Capture(target);
            target.AttachedToName = name;
            var after = UndoableProperties.Capture(target);
            var command = SetPropertiesCommand.TryCreate(target, before, after);
            if (command != null) CommandStack.Execute(command);
        }
    }
}
