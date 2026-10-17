using System;
using System.Collections.Generic;
using StarVoders.Core;
using StarVoders.Core.Input;
using UnityEngine;

namespace StarVoders.Module.Input
{
    /// <summary>
    /// The keys the game binds to one of its own actions ("Deck", "EndTurn", "SelectCard1"...), so a
    /// player who rebinds them in the game's Options gets the same keys from us. Read from the action
    /// asset of the scene's <c>PlayerInput</c> (one asset shared by every scene; the main menu loads
    /// the saved overrides into it, and <c>effectivePath</c> carries them), only while some key is
    /// down or just released, once a frame. Its keyboard bindings ("&lt;Keyboard&gt;/a") become
    /// <see cref="KeyCode"/>s polled from the OS like <see cref="KeyboardBinding"/>'s, with no
    /// modifier held (the game's own are bare keys). With no handler or action to read, the game's
    /// default key stands in.
    /// </summary>
    public sealed class GameKeyBinding : InputBinding
    {
        public string Action { get; }
        private readonly KeyCode _default;
        private readonly Func<StarVaders.InputHandler> _handler;

        private readonly List<KeyCode> _keys = new List<KeyCode>();
        private int _frame = int.MinValue;

        public GameKeyBinding(string action, KeyCode defaultKey, Func<StarVaders.InputHandler> handler)
        {
            Action = action;
            _default = defaultKey;
            _handler = handler;
        }

        private static bool NoModifiers => !OsKeys.CtrlHeld && !OsKeys.ShiftHeld && !OsKeys.AltHeld;

        public override bool JustPressed() => Any(OsKeys.Pressed);
        public override bool Held() => Any(OsKeys.Held);
        public override bool Released() => Any(OsKeys.Released);

        private bool Any(Func<KeyCode, bool> phase)
        {
            if (!OsKeys.AnyActive || !NoModifiers) return false;
            foreach (var key in Keys())
                if (phase(key)) return true;
            return false;
        }

        // A live type-ahead search owns bare letters and space (as KeyboardBinding's).
        public override bool ConflictsWithTypeahead
        {
            get
            {
                foreach (var key in Keys())
                    if ((key >= KeyCode.A && key <= KeyCode.Z) || key == KeyCode.Space) return true;
                return false;
            }
        }

        public override string DisplayName
        {
            get
            {
                var names = new List<string>();
                foreach (var key in Keys()) names.Add(KeyboardBinding.KeyName(key));
                return names.Count > 0 ? string.Join(", ", names) : Action;
            }
        }

        public override string Type => "game";

        // The game's action name: the keys themselves are the game's to say.
        public override string Serialize() => Action;

        // ---- reading the game ----

        private List<KeyCode> Keys()
        {
            int frame = Time.frameCount;
            if (frame == _frame) return _keys;
            _frame = frame;
            _keys.Clear();
            if (!Read(_keys)) _keys.Add(_default);
            return _keys;
        }

        // The action's keyboard keys into keys; false when there is no action to read.
        private bool Read(List<KeyCode> keys)
        {
            try
            {
                var input = _handler()?.PlayerInput;
                var asset = input != null ? input.actions : null;
                var action = asset != null ? asset.FindAction(Action, false) : null;
                if (action == null) return false;
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];
                    if (binding.isComposite || binding.isPartOfComposite) continue;
                    var key = KeyOf(binding.effectivePath);
                    if (key.HasValue && !keys.Contains(key.Value)) keys.Add(key.Value);
                }
                return true;
            }
            catch (Exception e)
            {
                Warn(Action + ": " + e.Message);
                return false;
            }
        }

        private static readonly HashSet<string> s_warned = new HashSet<string>();

        // Once per message: this runs every frame a key is down.
        private static void Warn(string message)
        {
            if (s_warned.Add(message)) CoreLog.Warning("GameKeyBinding: " + message);
        }

        // "<Keyboard>/a" (or "/Keyboard/a") as a KeyCode; null for another device. The control names
        // are the Input System's Key names, camel case.
        internal static KeyCode? KeyOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string name;
            if (path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase)) name = path.Substring(11);
            else if (path.StartsWith("/Keyboard/", StringComparison.OrdinalIgnoreCase)) name = path.Substring(10);
            else return null;
            name = name.ToLowerInvariant();
            if (name.Length == 1 && name[0] >= 'a' && name[0] <= 'z') return KeyCode.A + (name[0] - 'a');
            if (name.Length == 1 && name[0] >= '0' && name[0] <= '9') return KeyCode.Alpha0 + (name[0] - '0');
            if (name.Length > 1 && name[0] == 'f' && int.TryParse(name.Substring(1), out int f) && f >= 1 && f <= 12)
                return KeyCode.F1 + (f - 1);
            if (name.Length == 7 && name.StartsWith("numpad", StringComparison.Ordinal) && char.IsDigit(name[6]))
                return KeyCode.Keypad0 + (name[6] - '0');
            if (s_named.TryGetValue(name, out var key)) return key;
            Warn("no key for " + path);
            return null;
        }

        private static readonly Dictionary<string, KeyCode> s_named = new Dictionary<string, KeyCode>
        {
            ["space"] = KeyCode.Space, ["enter"] = KeyCode.Return, ["tab"] = KeyCode.Tab,
            ["backquote"] = KeyCode.BackQuote, ["quote"] = KeyCode.Quote, ["semicolon"] = KeyCode.Semicolon,
            ["comma"] = KeyCode.Comma, ["period"] = KeyCode.Period, ["slash"] = KeyCode.Slash,
            ["backslash"] = KeyCode.Backslash, ["leftbracket"] = KeyCode.LeftBracket,
            ["rightbracket"] = KeyCode.RightBracket, ["minus"] = KeyCode.Minus, ["equals"] = KeyCode.Equals,
            ["uparrow"] = KeyCode.UpArrow, ["downarrow"] = KeyCode.DownArrow,
            ["leftarrow"] = KeyCode.LeftArrow, ["rightarrow"] = KeyCode.RightArrow,
            ["escape"] = KeyCode.Escape, ["backspace"] = KeyCode.Backspace,
            ["pageup"] = KeyCode.PageUp, ["pagedown"] = KeyCode.PageDown, ["home"] = KeyCode.Home,
            ["end"] = KeyCode.End, ["insert"] = KeyCode.Insert, ["delete"] = KeyCode.Delete,
            ["numpadenter"] = KeyCode.KeypadEnter, ["numpaddivide"] = KeyCode.KeypadDivide,
            ["numpadmultiply"] = KeyCode.KeypadMultiply, ["numpadplus"] = KeyCode.KeypadPlus,
            ["numpadminus"] = KeyCode.KeypadMinus, ["numpadperiod"] = KeyCode.KeypadPeriod,
            ["leftshift"] = KeyCode.LeftShift, ["rightshift"] = KeyCode.RightShift,
            ["leftctrl"] = KeyCode.LeftControl, ["rightctrl"] = KeyCode.RightControl,
            ["leftalt"] = KeyCode.LeftAlt, ["rightalt"] = KeyCode.RightAlt,
        };
    }
}
