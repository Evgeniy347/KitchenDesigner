using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class CoreValidationResult
    {
        public readonly List<CoreContact> Contacts = new List<CoreContact>();
        public readonly List<int> Violations = new List<int>();
        public readonly List<List<int>> IsolatedGroups = new List<List<int>>();
        public bool IsValid;

        public List<CoreViolation>? Diagnostics;

        public void AddDiagnostic(int element, int other, ViolationKind kind)
        {
            (Diagnostics ??= new List<CoreViolation>()).Add(new CoreViolation(element, other, kind));
        }

        public void Clear()
        {
            Contacts.Clear();
            Violations.Clear();
            IsolatedGroups.Clear();
            Diagnostics = null;
            IsValid = false;
        }
    }
}
