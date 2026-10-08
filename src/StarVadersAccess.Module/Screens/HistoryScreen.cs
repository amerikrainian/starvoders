using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.UI;
using TMPro;
using FightState = StarVadersAccess.Module.Fight.Fight;
using Screen = StarVadersAccess.Core.Screens.Screen;

namespace StarVadersAccess.Module.Screens
{
    /// <summary>
    /// Run history (<c>HistoryScene</c>, <see cref="HistorySceneController"/>), one past run at a
    /// time, one stop top to bottom: the run (its result as the heading, then the difficulty, time,
    /// score, Doom, invaders defeated, highest combo, date and seed, as the panel words them; then
    /// which run of how many with the previous and next arrows); its final deck as a row ("name,
    /// cost, description", Left and Right); its artifacts as a row; Back. Escape is Back. Paging rebuilds the screen from the panel the game
    /// redraws.
    /// </summary>
    public sealed class HistoryScreen : Screen
    {
        public override string Key => "history";

        private static HistorySceneController Controller => GameScenes.Controller<HistorySceneController>();

        public override bool IsActive() => Controller != null;

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;

            // One stop, read top to bottom: the run's lines, the deck as a row (Left and Right walk
            // the cards), the artifacts as a row, Back at the bottom.
            b.BeginStop("history");
            b.PushContext(Result(c), null, positions: false);
            int i = 0;
            foreach (var line in Lines(c))
            {
                string text = line;
                b.AddItem(ControlId.Structural("history:line" + i++), GameNodes.Text(() => text));
            }
            if (ViewNodes.IsShown(c._leftArrow))
                b.AddItem(ControlId.Structural("history:prev"), ViewNodes.Button(c._leftArrow, () => Strings.HistoryPrevious));
            b.AddItem(ControlId.Structural("history:number"), GameNodes.Text(() => RunNumber(c)));
            if (ViewNodes.IsShown(c._rightArrow))
                b.AddItem(ControlId.Structural("history:next"), ViewNodes.Button(c._rightArrow, () => Strings.HistoryNext));
            b.PopContext();

            var cards = c._allCards;
            if (cards != null && cards.Count > 0)
            {
                b.PushContext(GameLoc.Get("Deck_Misc"));
                b.StartRow("history:deck");
                foreach (var card in cards)
                    if (card != null && card.CardModel != null)
                        b.AddItem(ControlId.Structural("history:card:" + card.GetInstanceID()), Card(card));
                b.EndRow();
                b.PopContext();
            }

            var artifacts = c._allArtifacts;
            if (artifacts != null && artifacts.Count > 0)
            {
                b.PushContext(GameLoc.Get("FilterArtifacts_Misc"));
                b.StartRow("history:artifacts");
                foreach (var artifact in artifacts)
                    if (artifact != null)
                        b.AddItem(ControlId.Structural("history:artifact:" + artifact.GetInstanceID()), Artifact(artifact));
                b.EndRow();
                b.PopContext();
            }

            var back = GameScenes.Item(c, "BackButton");
            if (ViewNodes.IsShown(back))
                b.AddItem(ControlId.Structural("history:back"), ViewNodes.Button(back, () => Strings.Get("bind.ui.back")));
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                try { Controller?.OnClickBackButton(); }
                catch (Exception e) { CoreLog.Warning("History: leaving failed: " + e.Message); }
            });
        }

        // "Victory" or "Defeat", whichever the panel shows (its icon after the word is decoration).
        private static string Result(HistorySceneController c)
        {
            foreach (var text in new[] { c.VictoryText, c.DefeatText, c.CorruptedText })
                if (text != null && text.gameObject.activeInHierarchy) return Word(text);
            return null;
        }

        // The panel's words, lone icons dropped ("Defeat <sprite Panic>" is "Defeat").
        private static string Word(TMP_Text text) => GameText.Of(text)?.Replace("Panic", "").Trim();

        // The panel's lines as it words them: "Time: 2h 2m 43s", "Score: 500".
        private static IEnumerable<string> Lines(HistorySceneController c)
        {
            var lines = new List<string>();
            Add(lines, GameText.Of(c.DifficultyTitle));
            Add(lines, Labelled(c.TimeNumber));
            Add(lines, Labelled(c.ScoreNumber)?.TrimEnd('=', ' '));
            Add(lines, GameText.Of(c.DoomText));
            Add(lines, GameText.Of(c.InvadersDefeatedText));
            Add(lines, GameText.Of(c.HighestComboText));
            Add(lines, GameText.Of(c.DateTimeSeedText));
            return lines;
        }

        // A figure with the label it sits under ("Time:" holds "2h 2m 43s").
        private static string Labelled(TMP_Text figure)
        {
            if (figure == null || !figure.gameObject.activeInHierarchy) return null;
            var parent = figure.transform.parent != null ? figure.transform.parent.GetComponent<TMP_Text>() : null;
            string label = parent != null ? GameText.Of(parent) : null;
            string value = GameText.Of(figure);
            return label != null && value != null ? label + " " + value : value;
        }

        // "Run 1/3" from the panel's "1 / 3".
        private static string RunNumber(HistorySceneController c)
        {
            string text = GameText.Of(c.RunText);
            if (text == null) return null;
            var parts = text.Split('/');
            return parts.Length == 2 && int.TryParse(parts[0].Trim(), out int run) && int.TryParse(parts[1].Trim(), out int runs)
                ? Strings.HistoryRun(run, runs) : text;
        }

        private static NodeVtable Card(BaseCardView view)
        {
            var card = view.CardModel;
            Func<string> name = () => FightState.CardName(card);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(name),
                    new NodeAnnouncement(() => EncounterScreen.Cost(card), kind: AnnouncementKinds.Value),
                    GameNodes.TooltipPart(() => FightState.CardDescription(card)),
                },
                SearchText = name,
                Details = () => FightState.InfoLines(card.MoreInfoWordNames),
            };
        }

        private static NodeVtable Artifact(DeckArtifactView view)
        {
            var model = view.ArtifactModel;
            Func<string> name = () => model != null ? GameLoc.ArtifactName(model.ArtifactName) : null;
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(name),
                    GameNodes.TooltipPart(() => model != null ? GameLoc.ArtifactDescription(model.ArtifactName) : null),
                },
                SearchText = name,
            };
        }

        private static void Add(List<string> lines, string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) lines.Add(text);
        }
    }
}
