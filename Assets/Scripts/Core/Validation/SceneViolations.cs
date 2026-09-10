using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class SceneViolations
    {
        public const ViolationKind UndiagnosedKind = (ViolationKind)(-1);

        private static readonly SceneViolations _empty = new SceneViolations();

        private readonly List<ContactViolation> _found = new List<ContactViolation>();

        public static SceneViolations Empty => _empty;

        public IReadOnlyList<ContactViolation> Found => _found;

        public bool IsClean => _found.Count == 0;

        public static SceneViolations OfScene() => Of(PartRegistry.GetAll());

        public static SceneViolations Of(List<KitchenElement> scene) =>
            Of(ConstraintValidator.Validate(scene));

        public static SceneViolations Of(ValidationResult? result)
        {
            var snapshot = new SceneViolations();
            snapshot.Take(result);
            return snapshot;
        }

        public bool Holds(KitchenElement element, ViolationKind kind)
        {
            if (element == null) return false;
            for (int i = 0; i < _found.Count; i++)
                if (ReferenceEquals(_found[i].element, element) && _found[i].kind == kind)
                    return true;
            return false;
        }

        private void Take(ValidationResult? result)
        {
            if (result == null) return;

            if (result.diagnostics != null)
                foreach (var d in result.diagnostics)
                {
                    Add(d.element, d.other, d.kind);
                    if (d.other != null) Add(d.other, d.element, d.kind);
                }

            foreach (var v in result.violations)
                if (v != null && !Diagnosed(v)) Add(v, null, UndiagnosedKind);
        }

        private void Add(KitchenElement element, KitchenElement? other, ViolationKind kind)
        {
            if (element == null || Holds(element, kind)) return;
            _found.Add(new ContactViolation(element, other, kind));
        }

        private bool Diagnosed(KitchenElement element)
        {
            for (int i = 0; i < _found.Count; i++)
                if (ReferenceEquals(_found[i].element, element)) return true;
            return false;
        }
    }
}
