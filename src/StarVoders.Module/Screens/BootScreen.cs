using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Input;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using UnityEngine;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The boot scene's title (the arcade machine, "PRESS ANY BUTTON TO PLAY"). The game takes any
    /// key through its own input, which focus mode silences, so the prompt is one button whose Enter
    /// is the game's own any-key handler (<c>BootupController.OnPressAnything</c>). Up once the game
    /// has finished loading its save (<c>IsFinishedLoadingGame</c>: before that the handler ignores a
    /// press) and until the press starts the transition out (<c>IsEndingScene</c>).
    /// <para>Any key is the press, as in the game: the screen declares no input categories (only the
    /// Global keys, F1 and the mod's Ctrl+Shift chords, stay live) and no type-ahead, and any plain key
    /// going down calls the handler. Not counted: the modifiers alone and anything with Ctrl, Alt or
    /// Insert held (silencing or commanding the screen reader), and the function keys (key help, the
    /// host's reload).</para>
    /// </summary>
    public sealed class BootScreen : Screen
    {
        public override string Key => "boot";
        public override string ScreenName => Strings.ScreenTitle;

        public override IReadOnlyList<InputCategory> InputCategories => NoCategories;
        public override bool AllowsTypeahead => false;

        private static readonly InputCategory[] NoCategories = { };

        private static BootupController Controller => GameScenes.Controller<BootupController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && c.IsFinishedLoadingGame && !c.IsEndingScene;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;
            b.AddItem(ControlId.Structural("boot:start"), GameNodes.Button(() => PromptText(c), () => c.OnPressAnything()));
        }

        private int _lastUpdateFrame = -1;

        // Counted only when this screen was focused the frame before too: the Escape that closes the key
        // help over it pops the help in this frame's input tick and must not be the any-key as well.
        public override void OnUpdate()
        {
            int frame = Time.frameCount;
            bool focusedBefore = _lastUpdateFrame == frame - 1;
            _lastUpdateFrame = frame;
            if (!focusedBefore || !FocusMode.Active || OsKeys.CtrlHeld || OsKeys.AltHeld || OsKeys.Held(KeyCode.Insert)) return;
            if (!OsKeys.AnyPressed(CountsAsAnyKey)) return;
            var c = Controller;
            if (c != null && c.IsFinishedLoadingGame && !c.IsEndingScene) c.OnPressAnything();
        }

        private static bool CountsAsAnyKey(KeyCode k) =>
            !(k >= KeyCode.F1 && k <= KeyCode.F15) && k != KeyCode.Insert
            && k != KeyCode.LeftShift && k != KeyCode.RightShift
            && k != KeyCode.LeftControl && k != KeyCode.RightControl
            && k != KeyCode.LeftAlt && k != KeyCode.RightAlt;

        // The prompt as the game draws it (localized), under the click-text group.
        private static string PromptText(BootupController c)
        {
            var group = c.ClickTextGroup;
            var tmp = group != null ? group.GetComponentInChildren<TMP_Text>(true) : null;
            return tmp != null && !string.IsNullOrWhiteSpace(tmp.text) ? tmp.text : Strings.ScreenTitle;
        }

        public override IEnumerable<ElementAction> GetActions() { yield break; }
    }
}
