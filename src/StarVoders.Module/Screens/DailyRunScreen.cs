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
    /// The Daily Run (<c>DailyRunSceneAdditive</c>, <see cref="DailyRunController"/>, over the main menu
    /// once every pilot is unlocked), named by its title ("Daily"). Stops: the run, its pilot (drawn
    /// only as a portrait: the static <c>PlayerData.PilotName</c> and the game's subtitle), the
    /// modifier lines under <c>ModVerticalLayoutGroup</c>, its artifacts, Start (the run begins: never
    /// through the driver) and Back, then the reset countdown and Total Completed; and the deck, its
    /// cards in the grid's order. Escape is Back. The view is built when the scene starts, so the screen
    /// holds its arrival until the deck is in, 2 s at most.
    /// </summary>
    public sealed class DailyRunScreen : Screen
    {
        public override string Key => "daily";
        public override int Layer => 10;
        public override bool Exclusive => true;

        private const float MaxWait = 2f;

        private static DailyRunController Controller => GameScenes.Controller<DailyRunController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        public override string ScreenName
        {
            get
            {
                var c = Controller;
                if (c == null) return null;
                foreach (var root in c.gameObject.scene.GetRootGameObjects())
                {
                    var title = root.transform.Find("TitleBox/TitleText") ?? FindNamed(root.transform, "TitleText");
                    if (title != null) return GameText.Of(title.GetComponent<TMP_Text>());
                }
                return null;
            }
        }

        private float _pushedAt;

        public override void OnPush() => _pushedAt = Time.unscaledTime;

        public override bool HoldsArrival => !Settled(Controller);

        private bool Settled(DailyRunController c)
            => c == null || Time.unscaledTime - _pushedAt >= MaxWait || Cards(c).Count > 0;

        private static List<DeckCardView> Cards(DailyRunController c)
        {
            var cards = new List<DeckCardView>();
            if (c.CardGrid != null)
                foreach (var card in c.CardGrid.GetComponentsInChildren<DeckCardView>(false))
                    if (card != null && card.CardModel != null) cards.Add(card);
            return cards;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.IsEndingScene || !Settled(c)) return;

            b.BeginStop("daily:run");
            b.PushContext(null, positions: false);
            string pilot = Pilot();
            if (pilot != null) b.AddItem(ControlId.Structural("daily:pilot"), GameNodes.Text(() => pilot));
            int i = 0;
            if (c.ModVerticalLayoutGroup != null)
                foreach (var tmp in c.ModVerticalLayoutGroup.GetComponentsInChildren<TMP_Text>(false))
                {
                    string line = GameText.Of(tmp)?.Replace("\n", " ").Trim();
                    if (string.IsNullOrEmpty(line) || line.Length < 2) continue;
                    b.AddItem(ControlId.Structural("daily:mod" + i++), GameNodes.Text(() => line));
                }
            foreach (var holder in new[] { c.PilotArtifactHolder, c.ArtifactModHolderObject != null && c.ArtifactModHolderObject.activeInHierarchy ? c.ArtifactModHolder : null })
            {
                if (holder == null) continue;
                foreach (var artifact in holder.GetComponentsInChildren<DeckArtifactView>(false))
                    if (artifact.ArtifactModel != null)
                        b.AddItem(ControlId.Structural("daily:artifact" + artifact.GetInstanceID()), CardNodes.Artifact(artifact.ArtifactModel, artifact));
            }
            if (ViewNodes.IsShown(c.StartButton))
                b.AddItem(ControlId.Structural("daily:start"), ViewNodes.Button(c.StartButton));
            if (ViewNodes.IsShown(c.BackButton))
                b.AddItem(ControlId.Structural("daily:back"), ViewNodes.Button(c.BackButton, () => Strings.Get("bind.ui.back")));
            if (c.DailyResetText != null && c.DailyResetText.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(GameText.Of(c.DailyResetText)))
                b.AddItem(ControlId.Structural("daily:reset"), GameNodes.Text(() => Clean(GameText.Of(c.DailyResetText))));
            if (c.StreakNumber != null && c.StreakNumber.gameObject.activeInHierarchy)
                b.AddItem(ControlId.Structural("daily:completed"), GameNodes.Text(() => Completed(c)));
            b.PopContext();

            var cards = Cards(c);
            if (cards.Count > 0)
            {
                b.BeginStop("daily:deck");
                b.PushContext(GameLoc.Get("Deck_Misc"));
                foreach (var card in cards)
                {
                    var vt = CardNodes.Card(card.CardModel, card);
                    vt.OnActivate = null;
                    b.AddItem(ControlId.Structural("daily:card:" + card.GetInstanceID()), vt);
                }
                b.PopContext();
            }
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var c = Controller;
                if (c != null && !c.IsEndingScene) ViewNodes.Click(c.BackButton);
            });
        }

        // "Garu, The ...": the pilot by name (pilots have no localized name) and the game's subtitle.
        private static string Pilot()
        {
            try
            {
                var data = DailyRunController.PlayerData;
                if (data == null) return null;
                string name = data.PilotName.ToString();
                string subtitle = GameLoc.Get(name + "_Subtitle_Misc");
                return subtitle != null ? name + ", " + subtitle : name;
            }
            catch (Exception e)
            {
                CoreLog.Warning("Daily: reading the pilot failed: " + e.Message);
                return null;
            }
        }

        // "Total Completed: 0": the count with the caption beside it (the nearest panel holding both).
        private static string Completed(DailyRunController c)
        {
            var panel = c.StreakNumber.transform;
            while (panel.parent != null && panel.GetComponentsInChildren<TMP_Text>(false).Length < 2) panel = panel.parent;
            var parts = new List<string>();
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
            {
                string text = GameText.Of(tmp);
                if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text)) parts.Add(text);
            }
            return string.Join(" ", parts);
        }

        // The countdown draws a double space between its two lines.
        private static string Clean(string text)
            => text == null ? null : System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

        private static Transform FindNamed(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindNamed(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
