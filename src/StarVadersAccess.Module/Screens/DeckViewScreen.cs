using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.UI;
using FightState = StarVadersAccess.Module.Fight.Fight;
using Screen = StarVadersAccess.Core.Screens.Screen;

namespace StarVadersAccess.Module.Screens
{
    /// <summary>
    /// The game's card viewer (<c>DeckViewSceneAdditive</c>, over whatever opened it): a fight's draw
    /// or discard pile, the run's deck, the shop's card removal. It draws no title, so the screen
    /// names it: in a fight, the pile its cards are in ("Draw Pile", the game's own word), else the
    /// deck. Its cards in the order shown (the draw pile comes shuffled, as the game shows it), each
    /// "name, cost, description" with the keyword explanations in the ui buffer and on Backspace;
    /// then its artifacts; Back is the next Tab stop. Enter is the game's click on a card (nothing in a pile; the
    /// removal in the shop's mode). Escape is Back.
    /// </summary>
    public sealed class DeckViewScreen : Screen
    {
        public override string Key => "deckview";
        public override int Layer => 25;
        public override bool Exclusive => true;

        private static DeckViewSceneController Controller => GameScenes.Controller<DeckViewSceneController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;
            var cards = Cards(c);
            b.BeginStop("deckview:cards");
            b.PushContext(Title(cards));
            foreach (var card in cards)
                b.AddItem(ControlId.Structural("deckview:card:" + card.GetInstanceID()), CardNodes.Card(card.CardModel, card));
            foreach (var grid in new[] { c._artifactGrid, c._artifactPilotUniqueGrid })
            {
                if (grid == null || !grid.gameObject.activeInHierarchy) continue;
                foreach (var artifact in grid.GetComponentsInChildren<DeckArtifactView>(false))
                    if (artifact != null) b.AddItem(ControlId.Structural("deckview:artifact:" + artifact.GetInstanceID()), CardNodes.Artifact(artifact.ArtifactModel, artifact));
            }
            b.PopContext();
            // Back on its own stop (Tab), so the list's positions count the cards alone.
            var back = GameScenes.Item(c, "BackButton");
            if (ViewNodes.IsShown(back))
            {
                b.BeginStop("deckview:back");
                b.AddItem(ControlId.Structural("deckview:back"), ViewNodes.Button(back, () => Strings.Get("bind.ui.back")));
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
