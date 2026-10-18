using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using UnityEngine;
using FightState = StarVoders.Module.Fight.Fight;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The game's card viewer (<c>DeckViewSceneAdditive</c>, over whatever opened it): a fight's draw
    /// or discard pile, the run's deck, the shop's card removal. It draws no title, so the screen
    /// names it: in a fight, the pile its cards are in ("Draw Pile", the game's own word), else the
    /// deck. Its cards in the order shown (the draw pile comes shuffled, as the game shows it), each
    /// "name, cost, description" with the keyword explanations in the ui buffer and on Backspace;
    /// then its artifacts, then Back. Enter is the game's click on a card (nothing in a pile; the
    /// removal in the shop's mode). Escape is Back.
    ///
    /// The game creates the artifacts first and the cards over the next frames, so the screen holds
    /// its arrival (declaring nothing) until the card count has held for a moment, else focus lands on
    /// the artifact with the cards appearing above it.
    /// </summary>
    public sealed class DeckViewScreen : Screen
    {
        public override string Key => "deckview";
        public override int Layer => 36;
        public override bool Exclusive => true;

        private static DeckViewSceneController Controller => GameScenes.Controller<DeckViewSceneController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        // Settled once the count has not changed for StableFor; an empty viewer (a pile with nothing
        // in it) after EmptyWait; anything after MaxWait.
        private const float StableFor = 0.15f, EmptyWait = 0.5f, MaxWait = 1.5f;
        private float _openedAt, _stableSince;
        private int _lastCount = -1;
        private bool _settled;

        public override void OnPush()
        {
            _openedAt = _stableSince = Time.unscaledTime;
            _lastCount = -1;
            _settled = false;
        }

        public override bool HoldsArrival => !_settled;

        public override void OnUpdate()
        {
            if (_settled) return;
            var c = Controller;
            if (c == null) return;
            float now = Time.unscaledTime;
            int count = Cards(c).Count;
            if (count != _lastCount) { _lastCount = count; _stableSince = now; }
            bool steady = now - _stableSince >= StableFor && (count > 0 || now - _openedAt >= EmptyWait);
            _settled = steady || now - _openedAt >= MaxWait;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || !_settled) return;
            var cards = Cards(c);
            b.BeginStop("deckview:cards");
            b.PushContext(Title(cards));
            var artifactsByGrid = new List<List<DeckArtifactView>>();
            foreach (var grid in new[] { c._artifactGrid, c._artifactPilotUniqueGrid })
            {
                if (grid == null || !grid.gameObject.activeInHierarchy) continue;
                var artifacts = new List<DeckArtifactView>();
                foreach (var artifact in grid.GetComponentsInChildren<DeckArtifactView>(false))
                    if (artifact != null) artifacts.Add(artifact);
                if (artifacts.Count > 0) artifactsByGrid.Add(artifacts);
            }
            bool both = cards.Count > 0 && artifactsByGrid.Count > 0;
            // Each grid's cards as strips, a row each as drawn (Up and Down keep the column), then the
            // artifacts' strips; labelled "Cards" and "Artifacts" when both show.
            b.PushContext(CardNodes.CardsLabel(both));
            foreach (var grid in new[] { c._cardGrid, c._cardPilotUniqueGrid })
                if (grid != null)
                    CardNodes.Strips(b, cards.FindAll(card => card.transform.IsChildOf(grid.transform)), "deckview:rows",
                        card => ControlId.Structural("deckview:card:" + card.GetInstanceID()),
                        card => CardNodes.Card(card.CardModel, card));
            b.PopContext();
            b.PushContext(CardNodes.ArtifactsLabel(both));
            foreach (var artifacts in artifactsByGrid)
                CardNodes.Strips(b, artifacts, "deckview:rows",
                    artifact => ControlId.Structural("deckview:artifact:" + artifact.GetInstanceID()),
                    artifact => CardNodes.Artifact(artifact.ArtifactModel, artifact));
            b.PopContext();
            b.PopContext();
            // Back after the cards in the same stop, outside their context, so the list's positions
            // count the cards alone.
            var back = GameScenes.Item(c, "BackButton");
            if (ViewNodes.IsShown(back))
            {
                b.PushContext(null, positions: false);
                b.AddItem(ControlId.Structural("deckview:back"), ViewNodes.Button(back, () => Strings.Get("bind.ui.back")));
                b.PopContext();
            }
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Exit());
        }

        private static void Exit()
        {
            var c = Controller;
            if (c == null || c.IsEndingScene) return;
            try { c.ExitScene(); }
            catch (Exception e) { CoreLog.Warning("DeckView: leaving failed: " + e.Message); }
        }

        // The cards in the order the grids show them: the main grid, then the pilot's unique cards.
        private static List<DeckCardView> Cards(DeckViewSceneController c)
        {
            var cards = new List<DeckCardView>();
            foreach (var grid in new[] { c._cardGrid, c._cardPilotUniqueGrid })
            {
                if (grid == null || !grid.gameObject.activeInHierarchy) continue;
                foreach (var card in grid.GetComponentsInChildren<DeckCardView>(false))
                    if (card != null && card.CardModel != null) cards.Add(card);
            }
            return cards;
        }

        // In a fight, the pile holding the cards shown ("Draw Pile", "Discard Pile"); else the deck.
        private static string Title(List<DeckCardView> cards)
        {
            var model = FightState.Model;
            if (DeckViewSceneController.Type == DeckSceneType.PileView && model != null && cards.Count > 0)
            {
                try
                {
                    var pile = model.CardPlayModel.GetPileContainingCard(cards[0].CardModel.CardID);
                    if (pile == Pile.Draw) return GameLoc.Get("DrawPileTitle_Misc");
                    if (pile == Pile.Discard) return GameLoc.Get("DiscardPileTitle_Misc");
                }
                catch (Exception e) { CoreLog.Warning("DeckView: finding the pile failed: " + e.Message); }
            }
            return GameLoc.Get("Deck_Misc");
        }
    }
}
