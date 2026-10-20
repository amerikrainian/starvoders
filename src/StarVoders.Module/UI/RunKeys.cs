using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Input;
using StarVoders.Module.Interop;

namespace StarVoders.Module.UI
{
    /// <summary>
    /// The Run key category: keys that read the run itself (Ctrl+D Doom), live in the fight, on the
    /// run's screens between fights and on the overlays above them (pause menu, deck viewer, more
    /// info, the Compendium from the pause menu, tips) while a run scene is underneath; not on the
    /// main menu's own Compendium.
    /// </summary>
    internal static class RunKeys
    {
        private static readonly InputCategory[] RunUi = { InputCategory.Run, InputCategory.UI };
        private static readonly InputCategory[] UiOnly = { InputCategory.UI };

        /// <summary>A run scene is loaded: the fight, or a campaign scene (the encounter choice, the
        /// rewards, the Workshop, Bud's Factory, the special rewards).</summary>
        public static bool InRun => Fight.Fight.Controller != null || GameScenes.Controller<CampaignSceneController>() != null;

        /// <summary>A screen's categories: the Run keys and UI navigation in a run, else UI alone.</summary>
        public static IReadOnlyList<InputCategory> Categories => InRun ? RunUi : UiOnly;

        /// <summary>"Doom 3/5": in a fight the fight's model (its maximum from the panel), between fights
        /// the run's player data, which the fight writes back when it ends.</summary>
        public static string DoomLine()
        {
            if (Fight.Fight.Controller != null)
                return Fight.Fight.Shown(Fight.Fight.View?.DoomView) ? Fight.Fight.Figure("Doom") : null;
            var player = DataManager.PlayerData;
            if (player == null || player.isEmpty) return null;
            string figure = player.maxDoom > 0
                ? Core.Strings.Strings.Position(player.doomAmount, player.maxDoom)
                : player.doomAmount.ToString();
            string title = GameLoc.Get("DoomTitle_Misc");
            return title != null ? title + " " + figure : figure;
        }
    }
}
