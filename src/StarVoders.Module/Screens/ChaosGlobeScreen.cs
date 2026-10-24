using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Module.Fight;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The way to the final fight (<c>ChaosGlobeScene</c>, <see cref="ChaosGlobeController"/>), loaded
    /// when the Chaos God's last conversation ends (<c>ChaosGodController.TransitionToChaosGlobe</c>):
    /// the act timeline (the pause menu's <c>MapPanel</c> reading), then the scene's one button
    /// (<c>Button</c>), read by the title it draws, "The Overseer" (<c>RegularTitle</c>, or
    /// <c>UltimateTitle</c>'s "The Overseer...?" on the ultimate path); its click is
    /// <see cref="ChaosGlobeController.OnChaosGodPressed"/>, which starts the fight. No way back: the game
    /// offers none. Up until the button is pressed (<see cref="ChaosGlobeController.IsEndingScene"/>).
    /// </summary>
    public sealed class ChaosGlobeScreen : Screen
    {
        public override string Key => "chaosglobe";
        public override IReadOnlyList<InputCategory> InputCategories => RunKeys.Categories;

        private static ChaosGlobeController Controller => GameScenes.Controller<ChaosGlobeController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;
            b.BeginStop("chaosglobe:run");
            var map = GameScenes.Item(c, "MapPanel");
            if (map != null) CampaignNodes.Map(b, map, "chaosglobe", row: true);
            var button = GameScenes.Item(c, "Button");
            if (ViewNodes.IsShown(button))
            {
                b.PushContext(null, positions: false);
                b.AddItem(ControlId.Structural("chaosglobe:fight"), ViewNodes.Button(button, () => Title(c)));
                b.PopContext();
                b.SetStart(ControlId.Structural("chaosglobe:fight"));
            }
        }

        // The title the button shows, the ultimate one when it is drawn.
        private static string Title(ChaosGlobeController c)
        {
            foreach (var title in new[] { c.RoomPanelTitleUltimate, c.RoomPanelTitleRegular })
                if (title != null && title.activeInHierarchy)
                {
                    string text = GameText.Of(title.GetComponent<TMP_Text>());
                    if (text != null) return Overseer.Proper(text);
                }
            return Overseer.Name();
        }
    }
}
