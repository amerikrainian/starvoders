using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Graph;
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
    /// The run's modifiers (<c>CheckModifiersSceneAdditive</c>, <see cref="CheckModifiersController"/>,
    /// the pause menu's MODIFIERS), titled by its own TitleText. One stop: the pilot (drawn as an image:
    /// the <see cref="PilotName"/> its name sprite is named for), each modifier line under
    /// <c>ModVerticalLayoutGroup</c> ("Basic Invaders Only"), the modifier artifacts when the run has
    /// them (<c>ArtifactModHolder</c>), Back. Escape is <see cref="CheckModifiersController.OnClickBack"/>;
    /// layer 35 over the pause menu, empty once leaving.
    /// </summary>
    public sealed class ModifiersScreen : Screen
    {
        public override string Key => "modifiers";
        public override int Layer => 35;
        public override bool Exclusive => true;

        private static CheckModifiersController Controller => GameScenes.Controller<CheckModifiersController>();

        public override bool IsActive() => Controller != null;

        public override string ScreenName
        {
            get
            {
                var c = Controller;
                var title = c != null && c.MainPanel != null ? Find(c.MainPanel, "TitleText") : null;
                return GameText.Of(title);
            }
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Leave());
        }

        private static void Leave()
        {
            var c = Controller;
            if (c == null || c.IsEndingScene) return;
            try { c.OnClickBack(); }
            catch (Exception e) { CoreLog.Warning("Modifiers: leaving failed: " + e.Message); }
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.IsEndingScene) return;
            b.BeginStop("modifiers");
            b.PushContext(null, positions: false);
            string pilot = Pilot(c);
            if (pilot != null) b.AddItem(ControlId.Structural("modifiers:pilot"), GameNodes.Text(() => pilot));
            int i = 0;
            if (c.ModVerticalLayoutGroup != null)
                foreach (var tmp in c.ModVerticalLayoutGroup.GetComponentsInChildren<TMP_Text>(false))
                {
                    string line = GameText.Of(tmp)?.Replace("\n", " ").Trim();
                    if (string.IsNullOrEmpty(line) || line.Length < 2) continue;
                    b.AddItem(ControlId.Structural("modifiers:line" + i++), GameNodes.Text(() => line));
                }
            if (c.ArtifactModHolderObject != null && c.ArtifactModHolderObject.activeInHierarchy && c.ArtifactModHolder != null)
                foreach (var artifact in c.ArtifactModHolder.GetComponentsInChildren<DeckArtifactView>(false))
                    if (artifact.ArtifactModel != null)
                        b.AddItem(ControlId.Structural("modifiers:artifact" + artifact.GetInstanceID()), CardNodes.Artifact(artifact.ArtifactModel, artifact));
            b.AddItem(ControlId.Structural("modifiers:back"), GameNodes.Button(() => Strings.Get("bind.ui.back"), Leave));
            b.PopContext();
        }

        // The pilot whose name the sprite is named for ("T_RoxyName").
        private static string Pilot(CheckModifiersController c)
        {
            var sprite = c.PilotNameImage != null ? c.PilotNameImage.sprite : null;
            if (sprite == null) return null;
            foreach (PilotName pilot in Enum.GetValues(typeof(PilotName)))
            {
                string name = pilot.ToString();
                if (name.Length > 2 && sprite.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return name;
            }
            return null;
        }

        private static TMP_Text Find(Transform root, string name)
        {
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(false))
                if (tmp.name == name) return tmp;
            return null;
        }
    }
}
