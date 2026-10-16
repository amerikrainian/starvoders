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
    /// The challenges (<c>ChallengesSceneAdditive</c>, <see cref="ChallengesSceneController"/>, the
    /// difficulty choice's Challenges), titled by its TitleText. One stop, as the difficulties: the
    /// challenges (<c>ChallengeTextLineList</c>) as a row of radio buttons by title, coming up into it on
    /// the selected one (a locked one LOCKED and disabled); Enter is its click (<c>OnClickChallenge</c>:
    /// selects only). Then the selected challenge: "Custom Loadout, Deck" when it has a custom start (the
    /// deck viewer), the modifier lines under <c>ModVerticalLayoutGroup</c>, the modifier artifacts, its
    /// trophy (the sprite: Completed, with the True or Ultimate Ending); the pilot (drawn as an image: by
    /// its name sprite, "T_ShunName", with the game's subtitle) and Total Completed ("0/5"); Start (the
    /// run begins: never through the driver), Back. Escape is <c>OnClickBack</c>; empty once leaving.
    /// </summary>
    public sealed class ChallengesScreen : Screen
    {
        public override string Key => "challenges";
        public override int Layer => 11;
        public override bool Exclusive => true;

        private static ChallengesSceneController Controller => GameScenes.Controller<ChallengesSceneController>();

        public override bool IsActive() => Controller != null;

        public override string ScreenName
        {
            get
            {
                var c = Controller;
                if (c == null || c.MainPanel == null) return null;
                foreach (var tmp in c.MainPanel.GetComponentsInChildren<TMP_Text>(false))
                    if (tmp.name == "TitleText") return GameText.Of(tmp);
                return null;
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
            catch (Exception e) { CoreLog.Warning("Challenges: leaving failed: " + e.Message); }
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.IsEndingScene) return;
            b.BeginStop("challenges");

            // The challenges side by side, as the difficulties: a row (Left/Right), coming up into it on
            // the selected one.
            var shown = new List<ChallengeTextLine>();
            var lines = c.ChallengeTextLineList;
            if (lines != null)
                foreach (var line in lines)
                    if (ViewNodes.IsShown(line)) shown.Add(line);
            if (shown.Count > 0)
            {
                b.PushContext(null);
                b.StartRow("challenges:row", () => Math.Max(0, shown.FindIndex(Selected)));
                for (int i = 0; i < shown.Count; i++)
                    b.AddItem(ControlId.Structural("challenges:" + i), Challenge(c, shown[i]));
                b.EndRow();
                b.PopContext();
            }

            // The selected challenge: its custom start, modifiers, artifacts and trophy; then the pilot.
            b.PushContext(null, positions: false);
            if (c.CustomStartObject != null && c.CustomStartObject.activeInHierarchy && ViewNodes.IsShown(c._deckButton))
                b.AddItem(ControlId.Structural("challenges:deck"), ViewNodes.Button(c._deckButton, () => CustomStart(c) + ", " + GameLoc.Get("Deck_Misc")));
            int n = 0;
            foreach (var mod in ModLines(c))
            {
                string text = mod;
                b.AddItem(ControlId.Structural("challenges:mod" + n++), GameNodes.Text(() => text));
            }
            if (c.ArtifactModHolderObject != null && c.ArtifactModHolderObject.activeInHierarchy && c.ArtifactModHolder != null)
                foreach (var artifact in c.ArtifactModHolder.GetComponentsInChildren<DeckArtifactView>(false))
                    if (artifact.ArtifactModel != null)
                        b.AddItem(ControlId.Structural("challenges:artifact" + artifact.GetInstanceID()), CardNodes.Artifact(artifact.ArtifactModel, artifact));
            var selected = shown.Find(Selected);
            string trophy = selected != null ? Trophy(selected) : null;
            if (trophy != null) b.AddItem(ControlId.Structural("challenges:trophy"), GameNodes.Text(() => trophy));
            string pilot = Pilot(c);
            if (pilot != null) b.AddItem(ControlId.Structural("challenges:pilot"), GameNodes.Text(() => pilot));
            if (c.CompletionNumber != null && c.CompletionNumber.gameObject.activeInHierarchy)
                b.AddItem(ControlId.Structural("challenges:completed"), GameNodes.Text(() => Completed(c)));
            if (ViewNodes.IsShown(c._startButton))
                b.AddItem(ControlId.Structural("challenges:start"), ViewNodes.Button(c._startButton));
            if (ViewNodes.IsShown(c._backButton))
                b.AddItem(ControlId.Structural("challenges:back"), GameNodes.Button(() => Strings.Get("bind.ui.back"), Leave));
            b.PopContext();
        }

        private static bool Selected(ChallengeTextLine line)
            => line._selectedImage != null && line._selectedImage.enabled && line._selectedImage.gameObject.activeInHierarchy;

        // "Post-Apocalypse, radio button, selected": the title alone, as the difficulties; Enter selects
        // (the game's click, which also shows its modifiers), a locked one reads LOCKED and ignores it.
        private static NodeVtable Challenge(ChallengesSceneController c, ChallengeTextLine line)
        {
            Func<string> title = () => GameText.Of(line.Text);
            Func<string> label = () =>
            {
                string text = title();
                string locked = GameLoc.Get("LockedTitle_Misc");
                return line.IsLocked && text != locked ? locked + ", " + text : text;
            };
            return new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    GameNodes.SelectedPart(() => Selected(line)),
                    GameNodes.DisabledPart(() => !line.IsLocked),
                },
                SearchText = title,
                OnFocus = () => ViewNodes.Hover(line),
                OnActivate = () =>
                {
                    if (line.IsLocked || c.IsEndingScene) return;
                    try { line.OnClick(); }
                    catch (Exception e) { CoreLog.Warning("Challenges: selecting failed: " + e.Message); }
                },
            };
        }

        // The trophy by its sprite ("Completed", with the ending when won on one): nothing while not won.
        private static string Trophy(ChallengeTextLine line)
        {
            var sprite = line.TrophyImage != null ? line.TrophyImage.sprite : null;
            if (sprite == null) return null;
            IntPtr p = sprite.Pointer;
            string completed = GameLoc.Get("ProfileCompleted_Misc");
            if (line.UltimateEndingObtainedTrophy != null && p == line.UltimateEndingObtainedTrophy.Pointer) return completed + ", " + Strings.ChallengesUltimateEnding;
            if (line.TrueEndingObtainedTrophy != null && p == line.TrueEndingObtainedTrophy.Pointer) return completed + ", " + Strings.ChallengesTrueEnding;
            if (line.ObtainedTrophy != null && p == line.ObtainedTrophy.Pointer) return completed;
            return null;
        }

        // A custom start's caption, "Custom Loadout" (drawn with a colon, over its Deck button).
        private static string CustomStart(ChallengesSceneController c)
        {
            var tmp = c.CustomStartObject.GetComponentInChildren<TMP_Text>(false);
            return GameText.Of(tmp)?.Trim().TrimEnd(':').Trim();
        }

        // The modifier lines (the custom start, drawn among them, is not one).
        private static List<string> ModLines(ChallengesSceneController c)
        {
            var lines = new List<string>();
            if (c.ModVerticalLayoutGroup == null) return lines;
            foreach (var tmp in c.ModVerticalLayoutGroup.GetComponentsInChildren<TMP_Text>(false))
            {
                if (c.CustomStartObject != null && tmp.transform.IsChildOf(c.CustomStartObject.transform)) continue;
                string line = GameText.Of(tmp)?.Replace("\n", " ").Trim();
                if (!string.IsNullOrEmpty(line) && line.Length >= 2) lines.Add(line);
            }
            return lines;
        }

        // "Total Completed: 0/5": the pilot's challenges won, as drawn beside the trophy.
        private static string Completed(ChallengesSceneController c)
        {
            string count = GameText.Of(c.CompletionNumber) + "/" + GameText.Of(c.TotalNumber);
            string caption = GameLoc.Get("TotalCompleted_Misc");
            return caption != null ? caption + " " + count : count;
        }

        // "Shun, <subtitle>": the pilot whose name the sprite is named for ("T_ShunName").
        private static string Pilot(ChallengesSceneController c)
        {
            var sprite = c.PilotTitleImage != null ? c.PilotTitleImage.sprite : null;
            if (sprite == null) return null;
            foreach (PilotName pilot in Enum.GetValues(typeof(PilotName)))
            {
                string name = pilot.ToString();
                if (name.Length <= 2 || sprite.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string subtitle = GameLoc.Get(name + "_Subtitle_Misc");
                return subtitle != null ? name + ", " + subtitle : name;
            }
            return null;
        }
    }
}
