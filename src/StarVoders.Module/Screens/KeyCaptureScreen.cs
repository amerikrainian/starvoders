using System;
using System.Collections.Generic;
using HarmonyLib;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Input;
using StarVoders.Core.Strings;
using StarVoders.Module.Input;
using StarVoders.Module.UI;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// A key binding row's rebind, pushed as a child of the options by Enter on the row. The game's
    /// rebind (<see cref="UIRebindActionBehaviour"/>, Unity's interactive rebinding: arrows and the
    /// mouse excluded, Escape cancels) listens on the Input System keyboard, which focus mode
    /// disables, so for its length the keyboard is lent back (<see cref="FocusMode.LendKeyboard"/>)
    /// and the mod hears nothing (<see cref="CapturesRawInput"/>). In phases, each waiting for every
    /// key to be up first, so the Enter that opened it is never taken as the new key and the new key
    /// never reaches the mod once it has its keyboard back: arming (lend, then the row's click starts
    /// the rebind), listening (while the game shows its waiting text), settling (keyboard taken
    /// back). Closing refocuses the row, which reads its new key; a key the mod also uses on its own
    /// is named after it. The modifiers are kept out of the rebind (<see cref="NoModifiers"/>): the
    /// mod's game keys fire only with none held, and an Alt+Tab back to the game must not bind Alt.
    /// </summary>
    public sealed class KeyCaptureScreen : Screen
    {
        private enum Phase { Arming, Starting, Listening, Settling }

        // Frames the game gets to show its waiting text after the click before the rebind counts as
        // not started.
        private const int StartFrames = 10;

        private readonly OptionKeybind _row;
        private readonly string _label;
        private Phase _phase = Phase.Arming;
        private int _frames;

        public KeyCaptureScreen(OptionKeybind row, string label)
        {
            _row = row;
            _label = label;
        }

        public override string Key => "overlay.keycapture";
        public override bool IsActive() => false; // only ever a child of the options
        public override bool CapturesRawInput => true;
        public override bool AllowsTypeahead => false;
        public override string ScreenName => Strings.RebindPrompt(_label);

        public override void OnUpdate()
        {
            try { Step(); }
            catch (Exception e)
            {
                CoreLog.Warning("KeyCaptureScreen: " + e.Message);
                Close();
            }
        }

        public override void OnPop() => FocusMode.LendKeyboard(false);

        private void Step()
        {
            switch (_phase)
            {
                case Phase.Arming:
                    if (OsKeys.AnyActive) return;
                    FocusMode.LendKeyboard(true);
                    NoModifiers.Armed = true;
                    try { _row.OnClick(); }
                    finally { NoModifiers.Armed = false; }
                    _phase = Phase.Starting;
                    _frames = 0;
                    return;
                case Phase.Starting:
                    if (Waiting()) { _phase = Phase.Listening; return; }
                    if (++_frames <= StartFrames) return;
                    CoreLog.Warning("KeyCaptureScreen: the rebind of " + _label + " did not start");
                    FocusMode.LendKeyboard(false);
                    _phase = Phase.Settling;
                    return;
                case Phase.Listening:
                    if (Waiting()) return;
                    FocusMode.LendKeyboard(false);
                    _phase = Phase.Settling;
                    return;
                case Phase.Settling:
                    if (OsKeys.AnyActive) return;
                    Close();
                    // After the row's readout, which the refocus queues this frame.
                    Later.Frames(1, SayConflicts, "rebind conflicts");
                    return;
            }
        }

        // The rebind runs while the game shows its waiting text (its StartRebindProcess turns it on,
        // UpdateUI off again on completion or cancel).
        private bool Waiting()
        {
            var text = _row._rebindActionBehaviour != null ? _row._rebindActionBehaviour.waitingText : null;
            return text != null && text.gameObject.activeInHierarchy;
        }

        private void Close() => ParentScreen?.RemoveChild(this);

        /// <summary>Excludes the modifier keys from the rebind the row's click starts: a prefix on the
        /// operation's <c>Start</c>, which the game's builder chain ends with, armed only for that
        /// click.</summary>
        [HarmonyPatch]
        internal static class NoModifiers
        {
            internal static bool Armed;

            private static readonly string[] Paths =
            {
                "<Keyboard>/leftShift", "<Keyboard>/rightShift", "<Keyboard>/leftCtrl", "<Keyboard>/rightCtrl",
                "<Keyboard>/leftAlt", "<Keyboard>/rightAlt", "<Keyboard>/leftMeta", "<Keyboard>/rightMeta",
                // The synthetic controls a modifier also presses: either Shift, Ctrl or Alt, and any key.
                "<Keyboard>/shift", "<Keyboard>/ctrl", "<Keyboard>/alt", "<Keyboard>/anyKey",
            };

            [HarmonyPatch(typeof(UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation),
                nameof(UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation.Start))]
            [HarmonyPrefix]
            private static void Start(UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation __instance)
            {
                if (!Armed || __instance == null) return;
                try
                {
                    foreach (var path in Paths) __instance.WithControlsExcluding(path);
                }
                catch (Exception e) { CoreLog.Warning("KeyCaptureScreen: excluding the modifiers failed: " + e.Message); }
            }
        }

        // "C is also Coordinates": the action's keys that one of the mod's own bare keys shares. Both
        // would fire in the fight, so the player is told.
        private void SayConflicts()
        {
            var action = _row._rebindActionBehaviour != null ? _row._rebindActionBehaviour.focusedInputAction : null;
            if (action == null) return;
            var bindings = action.bindings;
            var keys = new List<UnityEngine.KeyCode>();
            for (int i = 0; i < bindings.Count; i++)
            {
                var key = GameKeyBinding.KeyOf(bindings[i].effectivePath);
                if (key.HasValue && !keys.Contains(key.Value)) keys.Add(key.Value);
            }
            foreach (var key in keys)
                foreach (var a in InputManager.Actions)
                    foreach (var b in a.Bindings)
                        if (b is KeyboardBinding kb && kb.Key == key && !kb.Ctrl && !kb.Shift && !kb.Alt)
                            Speech.Say(Strings.RebindConflict(KeyboardBinding.KeyName(key), a.DisplayLabel));
        }
    }
}
