using System;
using System.Collections.Generic;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Core.Screens;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Input;
using StarVoders.Module.UI;
using UnityEngine;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// One action's keys, a child of the key list: each key (Enter: Replace or Remove), Add key, and
    /// Reset to default while the keys differ from it. Changes save at once (<see cref="KeyBindings"/>).
    /// A protected action's last key cannot be removed (Replace still changes it).
    /// </summary>
    public sealed class ActionKeysScreen : Screen
    {
        private readonly InputAction _action;

        public ActionKeysScreen(InputAction action) => _action = action;

        public override string Key => "mod.actionkeys";
        public override bool KeptThroughTransitions => true;
        public override string ScreenName => _action.DisplayLabel;
        public override bool IsActive() => false; // only ever a child of the key list

        public override void Build(GraphBuilder b)
        {
            b.PushContext(null, null); // the screen name says the action
            if (_action.Bindings.Count == 0)
                b.AddItem(ControlId.Structural("actionkeys:none"), GameNodes.Text(() => Strings.KeysNone));
            foreach (var binding in _action.Bindings)
            {
                var bound = binding;
                b.AddItem(ControlId.Structural("actionkeys:" + bound.Type + ":" + bound.Serialize()), GameNodes.Button(() => bound.DisplayName, () => Choose(bound)));
            }
            b.AddItem(ControlId.Structural("actionkeys:add"), GameNodes.Button(() => Strings.KeysAdd, () => PushChild(new BindKeyScreen(_action, null))));
            b.AddItem(ControlId.Structural("actionkeys:reset"), GameNodes.Button(
                () => Strings.HelpRow(Strings.KeysReset, KeyBindings.DefaultsDisplay(_action)), Reset, () => !KeyBindings.IsDefault(_action)));
            b.PopContext();
        }

        // Replace or Remove, for one key.
        private void Choose(InputBinding binding)
        {
            var self = this;
            ChoiceSubmenuScreen.Open(binding.DisplayName, new[]
            {
                new ChoiceOption(Strings.KeysReplace, () => self.PushChild(new BindKeyScreen(_action, binding))),
                new ChoiceOption(Strings.KeysRemove, () =>
                {
                    if (KeyBindings.Remove(_action, binding)) Speech.Say(Strings.KeysRemoved(binding.DisplayName), interrupt: true);
                }, enabled: KeyBindings.CanRemove(_action), detail: KeyBindings.CanRemove(_action) ? null : Strings.KeysNeeded),
            });
        }

        private void Reset()
        {
            KeyBindings.Reset(_action);
            Speech.Say(Strings.KeysResetDone(_action.BindingsDisplay), interrupt: true);
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ParentScreen?.RemoveChild(this));
        }
    }

    /// <summary>
    /// Waits for the new key of an action: added, or in place of <c>replacing</c>. The mod stands
    /// down meanwhile (<see cref="CapturesRawInput"/>), and keys are read from the OS directly. In
    /// phases, each starting once every key is up, so the Enter that opened it is never taken and the
    /// new key never reaches the mod afterwards: waiting, listening (the first key that is not a
    /// modifier goes down: it, with the Ctrl, Shift and Alt held then, is the key; Escape alone
    /// cancels), settling. A key another action of the same category (or Global) has is refused,
    /// named, and the capture listens on; one only another category has is taken and that action
    /// named ("C is also Coordinates"): the deeper category wins wherever both are live.
    /// </summary>
    public sealed class BindKeyScreen : Screen
    {
        private enum Phase { Waiting, Listening, Settling }

        private readonly InputAction _action;
        private readonly InputBinding _replacing;
        private Phase _phase = Phase.Waiting;

        public BindKeyScreen(InputAction action, InputBinding replacing)
        {
            _action = action;
            _replacing = replacing;
        }

        public override string Key => "mod.bindkey";
        public override bool KeptThroughTransitions => true;
        public override bool IsActive() => false; // only ever a child of an action's keys
        public override bool CapturesRawInput => true;
        public override bool AllowsTypeahead => false;
        public override string ScreenName => Strings.RebindPrompt(_action.DisplayLabel);

        public override void OnUpdate()
        {
            try { Step(); }
            catch (Exception e)
            {
                CoreLog.Warning("BindKeyScreen: " + e.Message);
                Close();
            }
        }

        private void Step()
        {
            switch (_phase)
            {
                case Phase.Waiting:
                    if (!OsKeys.AnyActive) _phase = Phase.Listening;
                    return;
                case Phase.Listening:
                    var key = OsKeys.FirstPressed(k => !OsKeys.IsModifier(k));
                    if (key == null) return;
                    bool ctrl = OsKeys.CtrlHeld, shift = OsKeys.ShiftHeld, alt = OsKeys.AltHeld;
                    if (key == KeyCode.Escape && !ctrl && !shift && !alt)
                    {
                        Speech.Say(Strings.KeysCancelled, interrupt: true);
                        _phase = Phase.Settling;
                        return;
                    }
                    if (Take(new KeyboardBinding(key.Value, ctrl, shift, alt))) _phase = Phase.Settling;
                    return;
                case Phase.Settling:
                    if (!OsKeys.AnyActive) Close();
                    return;
            }
        }

        // Applies the key; false (listening on) when another action of its category has it.
        private bool Take(InputBinding binding)
        {
            string name = binding.DisplayName;
            var own = KeyBindings.Existing(_action, binding);
            if (own != null)
            {
                // The key being replaced, pressed again: nothing changes.
                if (own == _replacing) { Speech.Say(Strings.KeysKept(name), interrupt: true); return true; }
                Speech.Say(Strings.KeysTaken(name, _action.DisplayLabel), interrupt: true);
                return false;
            }
            var conflict = KeyBindings.Conflict(_action, binding);
            if (conflict != null)
            {
                Speech.Say(Strings.KeysTaken(name, conflict.DisplayLabel), interrupt: true);
                return false;
            }
            if (_replacing != null) KeyBindings.Replace(_action, _replacing, binding);
            else KeyBindings.Add(_action, binding);
            var said = new List<string> { Strings.KeysBound(name) };
            foreach (var other in KeyBindings.Overlaps(_action, binding))
                said.Add(Strings.RebindConflict(name, other.DisplayLabel));
            Speech.Say(string.Join(". ", said), interrupt: true);
            return true;
        }

        private void Close() => ParentScreen?.RemoveChild(this);
    }
}
