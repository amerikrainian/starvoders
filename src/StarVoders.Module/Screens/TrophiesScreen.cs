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
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// Trophies (<c>AchievementGlossarySceneAdditive</c>, <see cref="AchievementGlossaryController"/>).
    /// Stops: the completion ("3/100, 3% Completed") and the Global Stats panel, each label with its
    /// figure ("Bosses Defeated: 1"; the favourite pilot by its portrait's pilot), the favourite card
    /// and artifact as the card viewer reads them; the achievements in order, each as its block shows
    /// it: an unlocked one's title, "Unlocked" and description; a locked one's task and LOCKED; a
    /// counted one's task and progress ("1/5"); a hidden one's "HIDDEN ACHIEVEMENT"; then the pages'
    /// buttons and Back. Escape is <see cref="AchievementGlossaryController.ExitScene"/>.
    /// </summary>
    public sealed class TrophiesScreen : Screen
    {
        public override string Key => "trophies";
        public override int Layer => 35;
        public override bool Exclusive => true;
        public override string ScreenName => GameLoc.Get("Trophies_Misc");

        private static AchievementGlossaryController Controller => GameScenes.Controller<AchievementGlossaryController>();

        // Up until the scene is gone, empty once leaving (see GlossaryNodes): dropping it at
        // the press would land on the screen beneath, then land there again after the scene change.
        public override bool IsActive() => Controller != null;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var c = Controller;
                if (c == null || c.IsEndingScene) return;
                try { c.ExitScene(); }
                catch (Exception e) { CoreLog.Warning("Trophies: leaving failed: " + e.Message); }
            });
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.IsEndingScene) return;

            b.BeginStop("trophies:stats");
            b.PushContext(null, positions: false);
            b.AddItem(ControlId.Structural("trophies:completion"), GameNodes.Text(() => Completion(c)));
            var panel = StatsPanel(c);
            string title = panel != null ? GameText.Of(panel.Find("Title")?.GetComponent<TMP_Text>()) : null;
            b.PushContext(title);
            int i = 0;
            foreach (var line in Stats(c))
            {
                string text = line;
                b.AddItem(ControlId.Structural("trophies:stat" + i++), GameNodes.Text(() => text));
            }
            if (ViewNodes.IsShown(c.FaveCardView) && c.FaveCardView.CardModel != null)
            {
                var card = CardNodes.Card(c.FaveCardView.CardModel, c.FaveCardView);
                card.OnActivate = null; // the card viewer's click: the shop's removal path
                b.AddItem(ControlId.Structural("trophies:favecard"), card);
            }
            if (ViewNodes.IsShown(c.FaveArtifactView) && c.FaveArtifactView.ArtifactModel != null)
                b.AddItem(ControlId.Structural("trophies:faveartifact"), CardNodes.Artifact(c.FaveArtifactView.ArtifactModel, c.FaveArtifactView));
            b.PopContext();
            b.PopContext();

            b.BeginStop("trophies:list");
            b.PushContext(null);
            if (c._achBlocks != null)
                foreach (var block in c._achBlocks)
                    if (ViewNodes.IsShown(block)) b.AddItem(ControlId.Structural("trophies:ach:" + block.GetInstanceID()), Achievement(block));
            b.PopContext();

            GlossaryNodes.AddBottom(b, c, "trophies");
        }

        // "3/100, 3% Completed".
        private static string Completion(AchievementGlossaryController c)
        {
            string fraction = GameText.Of(c.CompletionFractionText);
            string percent = GameText.Of(c.PercentCompletionText);
            return fraction != null && percent != null ? fraction + ", " + percent : fraction ?? percent;
        }

        // The Global Stats panel: the favourite card's panel holds it all.
        private static UnityEngine.Transform StatsPanel(AchievementGlossaryController c)
        {
            var faves = c.FaveCardView != null ? c.FaveCardView.transform.parent : null;
            return faves != null ? faves.parent : null;
        }

        // Each label with its figure, as drawn: the panel's texts are labels whose figures are their
        // children ("Bosses Defeated:" holds "1"); the favourite card's label is left to the card.
        private static List<string> Stats(AchievementGlossaryController c)
        {
            var lines = new List<string>();
            var panel = StatsPanel(c);
            if (panel == null) return lines;
            for (int i = 0; i < panel.childCount; i++)
            {
                var row = panel.GetChild(i);
                if (!row.gameObject.activeInHierarchy || row.name == "Title") continue;
                var label = row.GetComponent<TMP_Text>();
                if (label == null) continue;
                var parts = new List<string>();
                string l = GameText.Of(label)?.Trim();
                if (row.name == "FavePilot")
                {
                    string pilot = PilotOf(c);
                    if (pilot != null) parts.Add(pilot);
                }
                foreach (var tmp in row.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (tmp == label || tmp.GetComponentInParent<DeckCardView>() != null) continue;
                    // Min's Riches draws its star icon as "=" after the figure.
                    string t = GameText.Of(tmp)?.TrimEnd('=', ' ');
                    if (!string.IsNullOrWhiteSpace(t)) parts.Add(t);
                }
                // "Favorite Pilot: Roxy, 0 Victories, 0% winrate"; "Fave Card / Artifact:" alone, the
                // card and artifact following as their own entries.
                string values = parts.Count > 0 ? string.Join(", ", parts) : null;
                string line = string.IsNullOrEmpty(l) ? values : values != null ? l + " " + values : l;
                if (line != null) lines.Add(line);
            }
            return lines;
        }

        // The favourite pilot, by the portrait the panel shows (its sprite is named for the pilot).
        private static string PilotOf(AchievementGlossaryController c)
        {
            var sprite = c.PilotImage != null ? c.PilotImage.sprite : null;
            if (sprite == null || sprite == c.UnknownPilot) return null;
            foreach (PilotName pilot in Enum.GetValues(typeof(PilotName)))
            {
                string name = pilot.ToString();
                if (name.Length > 2 && sprite.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return name;
            }
            return null;
        }

        private static NodeVtable Achievement(AchievementBlock block)
        {
            Func<string> label = () => AchievementText(block);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                SearchText = label,
                OnFocus = () => ViewNodes.Hover(block),
            };
        }

        // As the block shows it, by the side it has up.
        private static string AchievementText(AchievementBlock block)
        {
            if (Shown(block.UnlockedObject))
                return Join(GameText.Of(block.UnlockedAchTitle), GameLoc.Get("ProfileUnlocked_Misc"), GameText.Of(block.UnlockedAchDesc));
            if (Shown(block.LockedCounterObject))
            {
                // The progress as drawn ("1/5"): the right counter; the left one is a hidden template.
                string progress = null;
                foreach (var counter in new[] { block.LeftCounter, block.RightCounter })
                    if (counter != null && counter.gameObject.activeInHierarchy) progress = GameText.Of(counter);
                return Join(GameText.Of(block.LockedCounterAchDesc), progress);
            }
            if (Shown(block.LockedNormalObject))
                return Join(GameText.Of(block.LockedAchDesc), GameLoc.Get("LockedTitle_Misc"));
            if (Shown(block.HiddenObject))
                return Join(GameText.Of(block.HiddenObject.GetComponentInChildren<TMP_Text>(false)));
            return ViewNodes.Label(block);
        }

        private static bool Shown(UnityEngine.GameObject o) => o != null && o.activeInHierarchy;

        private static string Join(params string[] parts)
        {
            var kept = new List<string>();
            foreach (var p in parts) if (!string.IsNullOrWhiteSpace(p)) kept.Add(p.Trim());
            return kept.Count > 0 ? string.Join(", ", kept) : null;
        }
    }
}
