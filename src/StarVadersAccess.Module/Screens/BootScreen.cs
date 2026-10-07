using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.UI;
using TMPro;
using Screen = StarVadersAccess.Core.Screens.Screen;

namespace StarVadersAccess.Module.Screens
{
    /// <summary>
    /// The boot scene's title (the arcade machine, "PRESS ANY BUTTON TO PLAY"). The game takes any
    /// key through its own input, which focus mode silences, so the prompt is one button whose Enter
    /// is the game's own any-key handler (<c>BootupController.OnPressAnything</c>). Up once the game
    /// has finished loading its save (<c>IsFinishedLoadingGame</c>: before that the handler ignores a
    /// press) and until the press starts the transition out (<c>IsEndingScene</c>).
    /// </summary>
    public sealed class BootScreen : Screen
    {
        public override string Key => "boot";
        public override string ScreenName => Strings.ScreenTitle;

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
