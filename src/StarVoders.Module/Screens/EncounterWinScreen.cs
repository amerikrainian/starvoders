using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using UnityEngine;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// A won fight (<c>EncounterWinSceneAdditive</c>, <see cref="EncounterWinController"/>): its
    /// title ("Victory") names the screen; then the stars earned as the panel reads them, and the
    /// loot box, whose click (<see cref="EncounterWinController.GoToRewardScene"/>, also the game's
    /// Click and Cancel keys) opens the reward. Up until the box is opened
    /// (<see cref="EncounterWinController.IsEndingScene"/>).
    /// </summary>
    public sealed class EncounterWinScreen : Screen
    {
        public override string Key => "encounterwin";
        public override IReadOnlyList<InputCategory> InputCategories => RunKeys.Categories;
        public override int Layer => 10;

        private static EncounterWinController Controller => GameScenes.Controller<EncounterWinController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        public override string ScreenName => GameText.Of(Text("Title")) ?? GameLoc.Get("VictoryTitle_Misc");

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;
            b.PushContext(null, positions: false);
            var stars = Text("StarTitle");
            if (stars != null && stars.gameObject.activeInHierarchy)
                b.AddItem(ControlId.Structural("encounterwin:stars"), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => GameText.Of(stars)) },
                    SearchText = () => GameText.Of(stars),
                });
            var box = GameScenes.Item(c, "Box (1)");
            if (ViewNodes.IsShown(box))
                b.AddItem(ControlId.Structural("encounterwin:box"), ViewNodes.Button(box, () => Strings.Get("win.open")));
            b.PopContext();
        }

        // The first text under the scene's object of that name.
        private static TMP_Text Text(string name)
        {
            var c = Controller;
            if (c == null) return null;
            foreach (var root in c.gameObject.scene.GetRootGameObjects())
            {
                var t = Find(root.transform, name);
                if (t != null) return t.GetComponentInChildren<TMP_Text>(true);
            }
            return null;
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = Find(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
