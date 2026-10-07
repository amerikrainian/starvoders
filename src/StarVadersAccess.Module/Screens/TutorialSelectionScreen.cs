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
    /// The tutorial choice Start opens on a fresh profile (an additive scene over the main menu):
    /// Full Tutorial and Quick Guide, each read with the description the game prints under it (texts
    /// beside the button in its selection panel, the button's parent), then
    /// Back, and the game's hint line (how to replay the tutorial) as text. Escape is Back.
    /// </summary>
    public sealed class TutorialSelectionScreen : Screen
    {
        public override string Key => "tutorialselect";
        public override int Layer => 10;
        public override bool Exclusive => true;

        private static TutorialSelectionController Controller => GameScenes.Controller<TutorialSelectionController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;
            b.PushContext(null, Strings.RoleList);
            Add(b, c, "StartButton", null, describe: true);
            Add(b, c, "QuickStartButton", null, describe: true);
            Add(b, c, "BackButton", () => Strings.Get("bind.ui.back"), describe: false);
            string hint = Hint(c);
            if (hint != null) b.AddItem(ControlId.Structural("tutorialselect:hint"), GameNodes.Text(() => Hint(c)));
            b.PopContext();
        }

        private static void Add(GraphBuilder b, TutorialSelectionController c, string name, System.Func<string> label, bool describe)
        {
            var item = GameScenes.Item(c, name);
            if (ViewNodes.IsShown(item))
                b.AddItem(ControlId.Structural("tutorialselect:" + name),
                    ViewNodes.Button(item, label, describe ? item.transform.parent : null));
        }

        // The line under the panel ("*You can replay the tutorial by creating a new profile").
        private static string Hint(TutorialSelectionController c)
        {
            var panel = c.MainPanel;
            if (panel == null) return null;
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
                if (tmp != null && tmp.gameObject.name == "Hint" && !string.IsNullOrWhiteSpace(tmp.text))
                    return tmp.text.TrimStart('*').Trim();
            return null;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Controller?.OnClickBack());
        }
    }
}
