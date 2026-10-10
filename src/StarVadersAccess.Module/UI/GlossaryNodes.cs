using StarVaders;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.Interop;

namespace StarVadersAccess.Module.UI
{
    /// <summary>
    /// What the four Compendium pages share (the Codex, Bestiary, Album and Trophies, each its own
    /// additive scene over the main menu): the row of buttons between them along the bottom, each the
    /// game's click (it loads that page's scene in place of this one), and Back.
    /// </summary>
    internal static class GlossaryNodes
    {
        private static readonly string[] PageButtons = { "CodexButton", "InvadersButton", "PilotsButton", "TrophiesButton" };

        /// <summary>The bottom stop: the pages' buttons shown, then Back.</summary>
        public static void AddBottom(GraphBuilder b, SceneController c, string prefix)
        {
            b.BeginStop(prefix + ":bottom");
            b.PushContext(null, positions: false);
            foreach (var name in PageButtons)
            {
                var item = GameScenes.Item(c, name);
                if (ViewNodes.IsShown(item)) b.AddItem(ControlId.Structural(prefix + ":" + name), ViewNodes.Button(item));
            }
            var back = GameScenes.Item(c, "BackButton");
            if (ViewNodes.IsShown(back)) b.AddItem(ControlId.Structural(prefix + ":back"), ViewNodes.Button(back, () => Strings.Get("bind.ui.back")));
            b.PopContext();
        }
    }
}
