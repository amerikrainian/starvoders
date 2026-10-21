using System;
using System.Collections.Generic;
using System.Linq;

namespace StarVoders.Core.Input
{
    /// <summary>
    /// The player's own keys for the mod's actions: each action keeps any number of bindings (a key
    /// with any of Ctrl, Shift, Alt held), added, replaced, removed and reset from the mod menu's key
    /// list. The registered bindings are the defaults (<see cref="Snapshot"/>, taken after every
    /// registration and before <see cref="Load"/>); only the actions that differ from them are saved,
    /// so a default changed in a later version still reaches every action the player never touched.
    /// <para>The saved form is one line, an entry per changed action, <c>;</c>-separated:
    /// <c>mod.menu=keyboard:M|ctrl,shift keyboard:F10;fight.coords=</c> (an empty list: unbound).
    /// Entries naming no registered action are kept as they are, so a key saved by a feature that is
    /// absent this session survives the next save.</para>
    /// <para>Only actions whose every default binding can be saved are rebindable: the fight's keys
    /// that follow the game's own bindings (its Options rebind them) cannot be.</para>
    /// </summary>
    public static class KeyBindings
    {
        /// <summary>The settings store key the saved form lives under.</summary>
        public const string StoreKey = "key_bindings";

        /// <summary>Saves the saved form (the module points it at the settings store).</summary>
        public static Action<string> Persist;

        /// <summary>Actions that must keep a key: without one the player could not reach the mod menu
        /// to put it back, or move and press anything.</summary>
        public static readonly HashSet<string> Protected = new HashSet<string>
        {
            "ui.up", "ui.down", "ui.activate", "ui.back", "mod.menu",
        };

        private static readonly Dictionary<string, List<InputBinding>> _defaults = new Dictionary<string, List<InputBinding>>();
        private static readonly List<KeyValuePair<string, string>> _unknown = new List<KeyValuePair<string, string>>();

        /// <summary>Record every registered action's bindings as its defaults.</summary>
        public static void Snapshot()
        {
            _defaults.Clear();
            foreach (var a in InputManager.Actions) _defaults[a.Key] = new List<InputBinding>(a.Bindings);
        }

        /// <summary>Forget the defaults and kept entries (module teardown, tests).</summary>
        public static void Clear()
        {
            _defaults.Clear();
            _unknown.Clear();
            Persist = null;
        }

        public static bool IsRebindable(InputAction action)
        {
            if (action == null || !_defaults.TryGetValue(action.Key, out var defaults) || defaults.Count == 0) return false;
            return defaults.TrueForAll(b => InputBinding.CanDeserialize(b.Type));
        }

        /// <summary>Whether the action has its default keys (in any order).</summary>
        public static bool IsDefault(InputAction action)
        {
            if (action == null || !_defaults.TryGetValue(action.Key, out var defaults)) return true;
            var now = new HashSet<string>(action.Bindings.Select(b => b.Chord));
            return now.SetEquals(defaults.Select(b => b.Chord));
        }

        /// <summary>The action's default keys, spoken as the key list says them.</summary>
        public static string DefaultsDisplay(InputAction action)
        {
            if (action == null || !_defaults.TryGetValue(action.Key, out var defaults) || defaults.Count == 0) return null;
            return string.Join(", ", defaults.Select(b => b.DisplayName));
        }

        // ---- changes ----

        public static void Add(InputAction action, InputBinding binding)
        {
            action.AddBinding(binding);
            Save();
        }

        public static void Replace(InputAction action, InputBinding old, InputBinding binding)
        {
            action.RemoveBinding(old);
            action.AddBinding(binding);
            Save();
        }

        /// <summary>Remove one key; false (nothing removed) when it is a protected action's last.</summary>
        public static bool Remove(InputAction action, InputBinding binding)
        {
            if (!CanRemove(action)) return false;
            action.RemoveBinding(binding);
            Save();
            return true;
        }

        public static bool CanRemove(InputAction action) => action.Bindings.Count > 1 || !Protected.Contains(action.Key);

        public static void Reset(InputAction action)
        {
            if (!_defaults.TryGetValue(action.Key, out var defaults)) return;
            Restore(action, defaults);
            Save();
        }

        public static void ResetAll()
        {
            foreach (var a in InputManager.Actions)
                if (IsRebindable(a) && !IsDefault(a)) Restore(a, _defaults[a.Key]);
            _unknown.Clear();
            Save();
        }

        private static void Restore(InputAction action, List<InputBinding> bindings)
        {
            action.ClearBindings();
            foreach (var b in bindings) action.AddBinding(b);
        }

        // ---- conflicts ----

        /// <summary>The other action the key would collide with: one in the same category, or with
        /// either in Global (always live, so a key there and anywhere else would leave one dead
        /// wherever the other is live). Null when none.</summary>
        public static InputAction Conflict(InputAction action, InputBinding binding)
        {
            foreach (var other in InputManager.Actions)
                if (other != action && Clash(action, other) && Shares(other, binding)) return other;
            return null;
        }

        private static bool Clash(InputAction a, InputAction b)
            => a.Category == b.Category || a.Category == InputCategory.Global || b.Category == InputCategory.Global;

        /// <summary>Other actions with the key in another category: allowed (the deeper category takes
        /// it where both are live, as the defaults' Home does in a menu and in the fight), but said.</summary>
        public static List<InputAction> Overlaps(InputAction action, InputBinding binding)
        {
            var list = new List<InputAction>();
            foreach (var other in InputManager.Actions)
                if (other != action && !Clash(action, other) && Shares(other, binding)) list.Add(other);
            return list;
        }

        /// <summary>The action's own binding with the same key, or null.</summary>
        public static InputBinding Existing(InputAction action, InputBinding binding)
        {
            foreach (var b in action.Bindings)
                if (b.Combos.Intersect(binding.Combos).Any()) return b;
            return null;
        }

        private static bool Shares(InputAction action, InputBinding binding)
        {
            foreach (var b in action.Bindings)
                if (b.Combos.Intersect(binding.Combos).Any()) return true;
            return false;
        }

        // ---- the saved form ----

        /// <summary>Apply a saved form over the defaults: each named rebindable action takes exactly
        /// the saved keys (none: unbound, except a protected action, which keeps its defaults).
        /// Bindings that do not parse are skipped.</summary>
        public static void Load(string saved)
        {
            _unknown.Clear();
            foreach (var entry in Parse(saved))
            {
                var action = InputManager.Find(entry.Key);
                if (action == null) { _unknown.Add(new KeyValuePair<string, string>(entry.Key, Join(entry.Value))); continue; }
                if (!IsRebindable(action)) continue;
                var bindings = new List<InputBinding>();
                foreach (var (type, data) in entry.Value)
                {
                    var b = InputBinding.Deserialize(type, data);
                    if (b == null) CoreLog.Warning("KeyBindings: skipping " + entry.Key + "'s unreadable key " + type + ":" + data);
                    else if (!bindings.Exists(x => x.Chord == b.Chord)) bindings.Add(b);
                }
                if (bindings.Count == 0 && Protected.Contains(action.Key)) continue;
                Restore(action, bindings);
            }
        }

        /// <summary>The saved form of the current keys: the changed actions, then the kept entries.</summary>
        public static string Format()
        {
            var parts = new List<string>();
            foreach (var a in InputManager.Actions)
                if (IsRebindable(a) && !IsDefault(a))
                    parts.Add(a.Key + "=" + Join(a.Bindings.Select(b => (b.Type, b.Serialize()))));
            foreach (var kv in _unknown) parts.Add(kv.Key + "=" + kv.Value);
            return string.Join(";", parts);
        }

        private static void Save()
        {
            try { Persist?.Invoke(Format()); }
            catch (Exception e) { CoreLog.Warning("KeyBindings: saving failed: " + e.Message); }
        }

        private static string Join(IEnumerable<(string Type, string Data)> bindings)
            => string.Join(" ", bindings.Select(b => b.Type + ":" + b.Data));

        /// <summary>The entries of a saved form, in order: action key, then its (type, data) pairs.</summary>
        public static List<KeyValuePair<string, List<(string Type, string Data)>>> Parse(string saved)
        {
            var entries = new List<KeyValuePair<string, List<(string, string)>>>();
            if (string.IsNullOrWhiteSpace(saved)) return entries;
            foreach (var part in saved.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string key = part.Substring(0, eq).Trim();
                var bindings = new List<(string, string)>();
                foreach (var token in part.Substring(eq + 1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int colon = token.IndexOf(':');
                    if (colon > 0) bindings.Add((token.Substring(0, colon), token.Substring(colon + 1)));
                }
                entries.Add(new KeyValuePair<string, List<(string, string)>>(key, bindings));
            }
            return entries;
        }
    }
}
