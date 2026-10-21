using System;
using System.Collections.Generic;
using System.Linq;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Core.Screens;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.UI;
using Navigation = StarVoders.Core.UI.Navigation;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The mod's own menu (Ctrl+Shift+M anywhere): Settings, Key help, Close. An overlay above every
    /// game screen, opened and closed by a static flag the poll picks up; its sub-screens are child
    /// screens, so Escape walks back through them to the game.
    /// </summary>
    public sealed class ModMenuScreen : Screen
    {
        private static bool s_open;

        public static void Toggle()
        {
            s_open = !s_open;
            if (!s_open) return;
            Core.Speech.Say(Strings.ScreenModMenu, interrupt: true);
        }

        public static void Close() => s_open = false;

        public override string Key => "mod.menu";
        public override bool KeptThroughTransitions => true;
        public override int Layer => 50;
        public override bool Exclusive => true;
        public override bool IsActive() => s_open;

        public override void Build(GraphBuilder b)
        {
            // A context with no words of its own, there for the positions: opening says the menu's
            // name itself (Toggle), so a label would say it a second time.
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("modmenu:settings"), GameNodes.Button(() => Strings.ModSettings, () => PushChild(new ModSettingsScreen())));
            b.AddItem(ControlId.Structural("modmenu:keys"), GameNodes.Button(() => Strings.ModKeyHelp, () => PushChild(new KeyHelpScreen())));
            b.AddItem(ControlId.Structural("modmenu:close"), GameNodes.Button(() => Strings.ModClose, Close));
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Close());
        }
    }

    /// <summary>The mod's settings: each a toggle; Enter flips it and speaks the new state. Values live
    /// in the host's settings store (the BepInEx config), so they survive reloads and restarts.</summary>
    public sealed class ModSettingsScreen : Screen
    {
        public override string Key => "mod.settings";
        public override bool KeptThroughTransitions => true;
        public override string ScreenName => Strings.ModSettings;
        public override bool IsActive() => false; // only ever a child of the mod menu

        public override void Build(GraphBuilder b)
        {
            var settings = ModuleMain.Host != null ? ModuleMain.Host.Settings : null;
            b.PushContext(null, null); // the screen name says "Settings"
            if (settings == null)
            {
                b.AddItem(ControlId.Structural("modsettings:none"), GameNodes.Text(() => Strings.NoTooltip));
                b.PopContext();
                return;
            }
            b.AddItem(ControlId.Structural("modsettings:positions"), Toggle(() => Strings.ModSpeakPositions,
                () => settings.SpeakPositions, v => settings.SpeakPositions = v));
            b.AddItem(ControlId.Structural("modsettings:focus"), Toggle(() => Strings.ModFocusOnLaunch,
                () => settings.FocusModeOnLaunch, v => settings.FocusModeOnLaunch = v));
            b.PopContext();
        }

        private static NodeVtable Toggle(Func<string> label, Func<bool> get, Action<bool> set)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Toggle,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    new NodeAnnouncement(() => get() ? Strings.StateOn : Strings.StateOff, live: true, kind: AnnouncementKinds.Value),
                },
                SearchText = label,
                OnActivate = () =>
                {
                    bool next = !get();
                    set(next);
                    Core.Speech.Say(next ? Strings.StateOn : Strings.StateOff, interrupt: true);
                },
            };
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ParentScreen?.RemoveChild(this));
        }
    }

    /// <summary>Every key the mod binds, whatever the screen, as its categories ("Global keys",
    /// "Fight keys"...), each opening its actions (<see cref="CategoryKeysScreen"/>); Reset all keys
    /// ends the list. The keys that do something HERE are <see cref="HelpScreen"/>'s (F1).</summary>
    public sealed class KeyHelpScreen : Screen
    {
        public override string Key => "mod.keys";
        public override bool KeptThroughTransitions => true;
        public override string ScreenName => Strings.ModKeyHelp;
        public override bool IsActive() => false; // only ever a child of the mod menu

        public override void Build(GraphBuilder b)
        {
            b.PushContext(null, null); // the screen name says "All keys"
            foreach (InputCategory category in Enum.GetValues(typeof(InputCategory)))
            {
                var c = category;
                if (!InputManager.Actions.Any(a => a.Category == c)) continue;
                b.AddItem(ControlId.Structural("keys:" + c), GameNodes.Button(() => Strings.ModKeyCategory(c.ToString()),
                    () => PushChild(new CategoryKeysScreen(c))));
            }
            b.AddItem(ControlId.Structural("keys:resetall"), GameNodes.Button(() => Strings.KeysResetAll, AskResetAll, AnyChanged));
            b.PopContext();
        }

        private static bool AnyChanged()
        {
            foreach (var a in InputManager.Actions)
                if (KeyBindings.IsRebindable(a) && !KeyBindings.IsDefault(a)) return true;
            return false;
        }

        private static void AskResetAll()
        {
            // No first: focus lands on it.
            ChoiceSubmenuScreen.Open(Strings.KeysResetAllAsk, new[]
            {
                new ChoiceOption(Strings.KeysNo, null),
                new ChoiceOption(Strings.KeysYes, () =>
                {
                    KeyBindings.ResetAll();
                    Core.Speech.Say(Strings.KeysResetAllDone, interrupt: true);
                }),
            });
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ParentScreen?.RemoveChild(this));
        }
    }

    /// <summary>One category's actions, "what it does: its keys". Enter on an action opens its keys
    /// to change (<see cref="ActionKeysScreen"/>); the fight's keys that follow the game's own
    /// bindings are rebound in the game's Options instead, and say so.</summary>
    public sealed class CategoryKeysScreen : Screen
    {
        private readonly InputCategory _category;

        public CategoryKeysScreen(InputCategory category) => _category = category;

        public override string Key => "mod.categorykeys";
        public override bool KeptThroughTransitions => true;
        public override string ScreenName => Strings.ModKeyCategory(_category.ToString());
        public override bool IsActive() => false; // only ever a child of the key list

        public override void Build(GraphBuilder b)
        {
            b.PushContext(null, null, positions: false); // the screen name says the category
            foreach (var action in InputManager.Actions)
            {
                if (action.Category != _category) continue;
                var a = action;
                Func<string> row = () => Strings.HelpRow(a.DisplayLabel, a.BindingsDisplay);
                if (KeyBindings.IsRebindable(a))
                    b.AddItem(ControlId.Structural("keys:" + a.Key), GameNodes.Button(row, () => PushChild(new ActionKeysScreen(a))));
                else
                    b.AddItem(ControlId.Structural("keys:" + a.Key), GameNodes.Text(() => row() + ", " + Strings.KeysGame));
            }
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ParentScreen?.RemoveChild(this));
        }
    }
}
