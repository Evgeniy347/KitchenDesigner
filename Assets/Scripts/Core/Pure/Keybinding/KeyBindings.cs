using System.Collections.Generic;
using System.Linq;

namespace KitchenDesigner.Core.Keybinding
{
    public sealed class KeyBindings
    {
        private readonly Dictionary<InputAction, KeyChord> _primaryOverrides =
            new Dictionary<InputAction, KeyChord>();
        private readonly Dictionary<InputAction, KeyChord> _altOverrides =
            new Dictionary<InputAction, KeyChord>();

        public KeyChord Primary(InputAction action) =>
            _primaryOverrides.TryGetValue(action, out var chord) ? chord : KeyBindingDefaults.PrimaryOf(action);

        public KeyChord Alt(InputAction action) =>
            _altOverrides.TryGetValue(action, out var chord) ? chord : KeyBindingDefaults.AltOf(action);

        public void SetPrimary(InputAction action, KeyChord chord)
        {
            if (chord == KeyBindingDefaults.PrimaryOf(action)) _primaryOverrides.Remove(action);
            else _primaryOverrides[action] = chord;
        }

        public void SetAlt(InputAction action, KeyChord chord)
        {
            if (chord.IsEmpty) _altOverrides.Remove(action);
            else _altOverrides[action] = chord;
        }

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
            var byChord = new Dictionary<KeyChord, HashSet<InputAction>>();

            foreach (var action in InputActionCatalog.All)
            {
                AddOccupant(byChord, Primary(action), action);
                AddOccupant(byChord, Alt(action), action);
            }

            return byChord
                .Where(pair => pair.Value.Count > 1)
                .Select(pair => new KeyBindingConflict(pair.Key, pair.Value.OrderBy(a => (int)a).ToArray()))
                .OrderBy(c => c.Actions[0])
                .ToArray();
        }

        private static void AddOccupant(
            Dictionary<KeyChord, HashSet<InputAction>> byChord, KeyChord chord, InputAction action)
        {
            if (chord.IsEmpty) return;
            if (!byChord.TryGetValue(chord, out var actions))
            {
                actions = new HashSet<InputAction>();
                byChord[chord] = actions;
            }
            actions.Add(action);
        }
    }
}
