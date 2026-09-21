using System.Collections.Generic;
using System.Linq;

namespace KitchenDesigner.Core.Keybinding
{
    public sealed class KeyBindings
    {
        private readonly Dictionary<InputAction, InputBinding> _primaryOverrides =
            new Dictionary<InputAction, InputBinding>();
        private readonly Dictionary<InputAction, InputBinding> _altOverrides =
            new Dictionary<InputAction, InputBinding>();

        public InputBinding PrimaryBinding(InputAction action) =>
            _primaryOverrides.TryGetValue(action, out var binding) ? binding : KeyBindingDefaults.PrimaryOf(action);

        public InputBinding AltBinding(InputAction action) =>
            _altOverrides.TryGetValue(action, out var binding) ? binding : KeyBindingDefaults.AltOf(action);

        public void SetPrimaryBinding(InputAction action, InputBinding binding)
        {
            if (binding == KeyBindingDefaults.PrimaryOf(action)) _primaryOverrides.Remove(action);
            else _primaryOverrides[action] = binding;
        }

        public void SetAltBinding(InputAction action, InputBinding binding)
        {
            if (binding.IsEmpty) _altOverrides.Remove(action);
            else _altOverrides[action] = binding;
        }

        public void SetPrimary(InputAction action, KeyChord chord) =>
            SetPrimaryBinding(action, InputBinding.FromKey(chord));

        public bool IsDefault(InputAction action) =>
            !_primaryOverrides.ContainsKey(action) && !_altOverrides.ContainsKey(action);

        public void ClearOverrides()
        {
            _primaryOverrides.Clear();
            _altOverrides.Clear();
        }

        public IEnumerable<InputAction> OverriddenActions =>
            InputActionCatalog.All.Where(a => !IsDefault(a));

        public IReadOnlyList<KeyBindingConflict> FindConflicts()
        {
            var byBinding = new Dictionary<InputBinding, HashSet<InputAction>>();

            foreach (var action in InputActionCatalog.All)
            {
                AddOccupant(byBinding, PrimaryBinding(action), action);
                AddOccupant(byBinding, AltBinding(action), action);
            }

            return byBinding
                .Where(pair => pair.Value.Count > 1)
                .Select(pair => new KeyBindingConflict(pair.Key, pair.Value.OrderBy(a => (int)a).ToArray()))
                .OrderBy(c => c.Actions[0])
                .ToArray();
        }

        private static void AddOccupant(
            Dictionary<InputBinding, HashSet<InputAction>> byBinding, InputBinding binding, InputAction action)
        {
            if (binding.IsEmpty) return;
            if (!byBinding.TryGetValue(binding, out var actions))
            {
                actions = new HashSet<InputAction>();
                byBinding[binding] = actions;
            }
            actions.Add(action);
        }
    }
}
