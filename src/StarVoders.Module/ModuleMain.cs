using System;
using System.Text;
using StarVoders.Contracts;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Core.Screens;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Input;
using StarVoders.Module.Interop;
using StarVoders.Module.Screens;
using StarVoders.Module.UI;
using HarmonyLib;
using UnityEngine;

namespace StarVoders.Module
{
    /// <summary>
    /// The reloadable feature module: wires the Core seams to the host, registers the mod's input
    /// actions and screens, owns focus mode, and drives the per-frame loop. Everything here is torn down
    /// in <see cref="Dispose"/> so a fresh generation can hot-swap in without a restart. Core statics are
    /// per-generation (Core loads into the same collectible context), so they need no manual reset.
    /// </summary>
    public sealed class ModuleMain : IModModule, IDevDriver
    {
        private IModHost _host;

        /// <summary>The host of the live generation (settings, logging), for screens that need it.</summary>
        public static IModHost Host { get; private set; }
        private Harmony _harmony;

        public void Load(IModHost host)
        {
            _host = host;
            Host = host;

            // Core seams: logging, speech, engine inputs, focus ownership, announcement wording.
            CoreLog.Info = host.LogInfo;
            CoreLog.Warning = host.LogWarning;
            CoreLog.Error = host.LogError;
            Speech.Speak = (text, interrupt) => host.Speech.Speak(text, interrupt);
            NavInput.Current = new UnityNavInput();
            Navigation.FocusActive = () => FocusMode.Active;
            // A scene change in progress: no game screen until the next scene's (no overlay closing on
            // its way out uncovers the screen beneath for a moment).
            ScreenManager.Transitioning = () => StarVaders.SceneLoader.IsTransitioning;
            // Escape where a screen has no Back of its own: the game's pause menu, as its Menu key.
            Navigation.BackFallbackAvailable = Pause.CanOpen;
            Navigation.BackFallback = Pause.Open;
            InputManager.FocusActive = () => FocusMode.Active;
            GraphAnnouncer.PositionText = (i, n) => host.Settings.SpeakPositions ? Strings.Position(i, n) : null;
            GraphAnnouncer.ExpandedStateText = Strings.ExpandedState;
            InputBinding.RegisterType("keyboard", KeyboardBinding.Deserialize);
            // The strings follow the game's language once Unity Localization has picked one (Tick
            // retries until it has); until then nothing is spoken that the table words.
            LanguageSync.Start(host.PluginDir, AnnounceLaunch);

            // A per-load UNIQUE id so a reload's Dispose unpatches exactly this load's patches: the host
            // loads the new module (which patches) before disposing the old one, and UnpatchSelf removes
            // by owner id, so a fixed id would let the old teardown strip the fresh load's patches.
            _harmony = new Harmony("com.amerikranian.starvoders.module." + Guid.NewGuid().ToString("N"));

            GameScenes.Start();
            RegisterInput();
            RegisterScreens();
            try { _harmony.PatchAll(typeof(ModuleMain).Assembly); }
            catch (Exception e) { host.LogError("Harmony patching failed: " + e); }

            FocusMode.Set(host.Settings.FocusModeOnLaunch);
            host.LogInfo("Module loaded: " + ScreenManager.Registered.Count + " screens, "
                + InputManager.Actions.Count + " input actions, focus " + (FocusMode.Active ? "on" : "off"));
        }

        // "StarVoders 0.1.0 loaded", once per launch, in the language the game came up in: the
        // host leaves the line to the module, which has the strings, and LanguageSync calls this when
        // the first language is in place. The host remembers it was said, so a reload stays quiet.
        private void AnnounceLaunch()
        {
            if (_host.LaunchAnnounced) return;
            _host.LaunchAnnounced = true;
            Speech.Say(Strings.ModLoaded(_host.ModVersion));
        }

        private static void RegisterInput()
        {
            // UI navigation keys: live only while a screen declares the UI category and focus mode is
            // on, routed into the navigator by InputManager. Directions and Tab auto-repeat while held.
            InputManager.Register(UiActions.Up, "Navigate up", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.UpArrow)).Repeating();
            InputManager.Register(UiActions.Down, "Navigate down", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.DownArrow)).Repeating();
            InputManager.Register(UiActions.Left, "Navigate left", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.LeftArrow)).Repeating();
            InputManager.Register(UiActions.Right, "Navigate right", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.RightArrow)).Repeating();
            InputManager.Register(UiActions.Next, "Next control group", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Tab)).Repeating();
            InputManager.Register(UiActions.Prev, "Previous control group", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Tab, shift: true)).Repeating();
            InputManager.Register(UiActions.Activate, "Activate", InputCategory.UI)
                .AddBinding(new KeyboardBinding(KeyCode.Return));
            InputManager.Register(UiActions.Secondary, "Secondary action", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Backspace));
            InputManager.Register(UiActions.Back, "Back", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Escape));
            InputManager.Register(UiActions.Home, "Jump to first", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.Home));
            InputManager.Register(UiActions.End, "Jump to last", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.End));
            InputManager.Register(UiActions.RegionPrev, "Previous section", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.UpArrow, alt: true));
            InputManager.Register(UiActions.RegionNext, "Next section", InputCategory.UI).AddBinding(new KeyboardBinding(KeyCode.DownArrow, alt: true));
            // Buffer review, the Harkest Dungeon keys: Ctrl+Left/Right switch buffers, Ctrl+Up/Down step lines.
            Buffers.Init();
            InputManager.Register("buffer.next", "Next buffer", InputCategory.UI, Buffers.Controls.NextBuffer).AddBinding(new KeyboardBinding(KeyCode.RightArrow, ctrl: true));
            InputManager.Register("buffer.prev", "Previous buffer", InputCategory.UI, Buffers.Controls.PreviousBuffer).AddBinding(new KeyboardBinding(KeyCode.LeftArrow, ctrl: true));
            InputManager.Register("buffer.line.next", "Next buffer line", InputCategory.UI, Buffers.Controls.NextLine).AddBinding(new KeyboardBinding(KeyCode.UpArrow, ctrl: true)).Repeating();
            InputManager.Register("buffer.line.prev", "Previous buffer line", InputCategory.UI, Buffers.Controls.PreviousLine).AddBinding(new KeyboardBinding(KeyCode.DownArrow, ctrl: true)).Repeating();

            // The fight's keys: live while the fight screen declares the Game category.
            Fight.FightKeys.Register();

            // Global: always live, so the player can hand the keyboard back to the game and reclaim it.
            InputManager.Register("mod.focus", "Toggle navigation", InputCategory.Global, ToggleFocus)
                .AddBinding(new KeyboardBinding(KeyCode.A, ctrl: true, shift: true));
            InputManager.Register("mod.menu", "Mod menu", InputCategory.Global, ModMenuScreen.Toggle)
                .AddBinding(new KeyboardBinding(KeyCode.M, ctrl: true, shift: true));
            // The keys that do something here, each runnable from the list.
            InputManager.Register(HelpScreen.ActionKey, "Keys here", InputCategory.Global, HelpScreen.Toggle)
                .AddBinding(new KeyboardBinding(KeyCode.F1));
        }

        private static void ToggleFocus()
        {
            FocusMode.Toggle();
            Speech.Say(FocusMode.Active ? Strings.FocusOn : Strings.FocusOff, interrupt: true);
            if (FocusMode.Active) Navigation.AnnounceCurrent();
        }

        private static void RegisterScreens()
        {
            ScreenManager.Register(new BootScreen());
            ScreenManager.Register(new MainMenuScreen());
            ScreenManager.Register(new TutorialSelectionScreen());
            ScreenManager.Register(new CutsceneScreen());
            ScreenManager.Register(new ConfirmPromptScreen());
            ScreenManager.Register(new GlobeScreen());
            ScreenManager.Register(new TipScreen());
            ScreenManager.Register(new EncounterScreen());
            ScreenManager.Register(new DeckViewScreen());
            ScreenManager.Register(new EncounterWinScreen());
            ScreenManager.Register(new RewardScreen());
            ScreenManager.Register(new WorkshopScreen());
            ScreenManager.Register(new PauseScreen());
            ScreenManager.Register(new CampaignEndScreen());
            ScreenManager.Register(new HistoryScreen());
            ScreenManager.Register(new CompendiumScreen());
            ScreenManager.Register(new BestiaryScreen());
            ScreenManager.Register(new AlbumScreen());
            ScreenManager.Register(new TrophiesScreen());
            ScreenManager.Register(new NewsScreen());
            ScreenManager.Register(new CreditsScreen());
            ScreenManager.Register(new SettingsScreen());
            ScreenManager.Register(new ModMenuScreen());
            ScreenManager.Register(new HelpScreen());
        }

        public void Tick()
        {
            Safe(LanguageSync.Tick, "language");
            FocusMode.Tick();
            InputManager.Tick();
            // Before the screens: an event is heard before the focus landing it caused (a target list).
            Safe(Fight.FightEvents.Tick, "fight events");
            ScreenManager.Tick();
            Navigation.TickTypeahead();
            Safe(Later.Tick, "later");
            Safe(Readers.PopupReader.Tick, "popups");
            Safe(Buffers.Tick, "buffers");
        }

        // A reader that throws must not take the whole tick (and every other reader) down with it.
        private void Safe(Action tick, string what)
        {
            try { tick(); }
            catch (Exception e) { _host?.LogWarning("[" + what + "] " + e); }
        }

        /// <summary>Undo every persistent game-side effect Load created. Runs on reload (after the new
        /// generation is live) and on shutdown.</summary>
        public void Dispose()
        {
            // On a reload the successor already owns the keyboard and the EventSystem: drop our hooks
            // only. On a shutdown, give everything back to the game.
            bool restore = _host == null || !_host.SuccessorLoaded;
            try { FocusMode.Shutdown(restore); } catch (Exception e) { _host?.LogError("[dispose] focus: " + e); }
            try { ScreenManager.Shutdown(); } catch (Exception e) { _host?.LogError("[dispose] screens: " + e); }
            try { InputManager.Clear(); } catch (Exception e) { _host?.LogError("[dispose] input: " + e); }
            try { LanguageSync.Stop(); } catch (Exception e) { _host?.LogError("[dispose] language: " + e); }
            try { GameScenes.Shutdown(); } catch (Exception e) { _host?.LogError("[dispose] scenes: " + e); }
            try { _harmony?.UnpatchSelf(); } catch (Exception e) { _host?.LogError("[dispose] harmony: " + e); }
            _harmony = null;
            _host = null;
            if (ReferenceEquals(Host, this)) Host = null;
        }

        // ---- IDevDriver: the dev server drives and inspects our navigation through these ----

        public string DispatchAction(string actionKey)
        {
            if (actionKey != null && actionKey.StartsWith("dev.type:"))
            {
                UnityNavInput.InjectTyped(actionKey.Substring("dev.type:".Length));
                return "typing next frame";
            }
            if (string.IsNullOrEmpty(actionKey) || InputManager.Find(actionKey) == null) return null;
            InputManager.Dispatch(actionKey);
            var nav = Navigation.Active as GraphNavigator;
            var node = nav?.FocusedNode;
            return "fired " + actionKey + (node != null ? " -> " + GraphAnnouncer.ComposeFull(node) : "");
        }

        public string DescribeNav()
        {
            var sb = new StringBuilder();
            sb.Append("focus mode: ").Append(FocusMode.Active ? "on" : "off").Append('\n');
            sb.Append("stack: ");
            foreach (var s in ScreenManager.Stack) sb.Append(s.Key).Append(' ');
            sb.Append('\n');
            var cur = ScreenManager.Current;
            sb.Append("screen: ").Append(cur != null ? cur.Key + " (" + (cur.ScreenName ?? "") + ")" : "(none)").Append('\n');
            var nav = Navigation.Active as GraphNavigator;
            var node = nav?.FocusedNode;
            if (node == null)
            {
                sb.Append("focus: (none)\n");
                return sb.ToString();
            }
            sb.Append("focus: ").Append(node.Id).Append('\n');
            sb.Append("readout: ").Append(GraphAnnouncer.ComposeFull(node)).Append('\n');
            var render = nav.CurrentRender;
            if (render != null)
            {
                sb.Append("nodes (").Append(render.Order.Count).Append("):\n");
                foreach (var n in render.Order)
                    sb.Append(ReferenceEquals(n, node) ? "  > " : "    ").Append(n.Id.StructuralKey)
                      .Append(": ").Append(GraphAnnouncer.LeafText(n)).Append('\n');
            }
            return sb.ToString();
        }

        public string ListActions()
        {
            var sb = new StringBuilder();
            foreach (var a in InputManager.Actions)
                sb.Append(a.Key).Append(" [").Append(a.Category).Append("] ").Append(a.BindingsDisplay).Append('\n');
            return sb.ToString();
        }
    }
}
