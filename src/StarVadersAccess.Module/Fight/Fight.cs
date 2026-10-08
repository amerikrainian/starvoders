using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.UI;

namespace StarVadersAccess.Module.Fight
{
    /// <summary>
    /// The fight (<c>EncounterScene</c>): its controller and model, and the game's own click. The
    /// game's keyboard plays through its input handler: hover an item
    /// (<see cref="InputHandler.OnHoverViewItemByKey"/>), then <see cref="EncounterInputHandler.ClickViewItems"/>,
    /// which plays a card, presses a button, or answers a running target selection with the hovered
    /// tile or arrow. Every node here clicks that way, so a click means what it means to the game.
    /// </summary>
    internal static class Fight
    {
        public static EncounterController Controller
        {
            get
            {
                var c = GameScenes.Controller<EncounterController>();
                return c != null && c.EncounterModel != null ? c : null;
            }
        }

        public static EncounterModel Model => Controller?.EncounterModel;
        public static EncounterView View => Controller?.EncounterView;

        /// <summary>Hover the item as the game's keys do: its highlight and info panel follow.</summary>
        public static void Hover(AViewItem item)
        {
            var handler = Controller?.EncounterInputHandler;
            if (handler == null || item == null) return;
            try { handler.OnHoverViewItemByKey(item); }
            catch (Exception e) { CoreLog.Warning("Fight: hovering " + item.gameObject.name + " failed: " + e.Message); }
        }

        /// <summary>The game's click on the item: hover it, then click what is hovered.</summary>
        public static void Click(AViewItem item)
        {
            var handler = Controller?.EncounterInputHandler;
            if (handler == null || item == null) return;
            try
            {
                handler.OnHoverViewItemByKey(item);
                handler.ClickViewItems();
            }
            catch (Exception e) { CoreLog.Warning("Fight: clicking " + item.gameObject.name + " failed: " + e.Message); }
        }

        /// <summary>The cards in hand, in hand order, those already being played left out.</summary>
        public static List<CardView> Hand()
        {
            var cards = new List<CardView>();
            var model = Model;
            var play = View?.CardPlayView;
            if (model == null || play == null) return cards;
            try
            {
                foreach (var id in model.CardPlayModel.GetCardsInHandButNotBeingPlayed())
                {
                    var view = play.GetCardView(id);
                    if (view != null) cards.Add(view);
                }
            }
            catch (Exception e) { CoreLog.Warning("Fight: reading the hand failed: " + e.Message); }
            return cards;
        }

        public static string CardName(CardModel card) => card != null ? GameLoc.Get(card.CardName + "_CardTitle") ?? card.CardName.ToString() : null;

        /// <summary>The card's description with its live numbers, as its info panel shows it.</summary>
        public static string CardDescription(CardModel card)
        {
            if (card == null) return null;
            try
            {
                string raw = LocalizationUtils.LocalizeSynchronous(card.CardName + "_CardDesc");
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("No translation for", StringComparison.Ordinal)) return null;
                var controller = Controller;
                if (controller != null) raw = LocalizationUtils.GetModifiedDescriptions(raw, card, controller);
                return GameText.Clean(raw);
            }
            catch (Exception e)
            {
                CoreLog.Warning("Fight: describing " + card.CardName + " failed: " + e.Message);
                return null;
            }
        }

        // ---- target selection ----

        /// <summary>Whether the game is asking for a target (a card being played waits on it).</summary>
        public static bool Selecting
        {
            get
            {
                var sc = Controller?.SelectionController;
                return sc != null && sc.IsSelecting;
            }
        }

        /// <summary>The view items the running selection accepts, by the game's own test
        /// (<see cref="SelectionController.IsSatisfied"/>), in the encounter's view order.</summary>
        public static List<AEncounterViewItem> Targets()
        {
            var targets = new List<AEncounterViewItem>();
            var c = Controller;
            var sc = c?.SelectionController;
            if (sc == null || !sc.IsSelecting) return targets;
            try
            {
                foreach (var item in c.EncounterView.ViewsItems)
                {
                    if (item == null || !item.gameObject.activeInHierarchy) continue;
                    bool ok;
                    try { ok = sc.IsSatisfied(item); } catch { ok = false; }
                    if (ok) targets.Add(item);
                }
            }
            catch (Exception e) { CoreLog.Warning("Fight: listing the targets failed: " + e.Message); }
            return targets;
        }

        /// <summary>The selection's prompt as the right-hand screen states it ("Click grid to play").</summary>
        public static string Prompt()
        {
            var screen = View?.ScreenView;
            var prompt = screen != null ? screen.SelectionPrompt : null;
            return prompt != null && prompt.gameObject.activeInHierarchy ? GameText.Of(prompt) : null;
        }

        public static bool Cancellable
        {
            get
            {
                var sc = Controller?.SelectionController;
                try { return sc != null && sc.IsSelecting && sc.IsCancellable(); }
                catch { return false; }
            }
        }

        /// <summary>Cancel the selection as the game's Cancel key does: the selection, then the
        /// previews, info panel and hover it left behind.</summary>
        public static void Cancel()
        {
            var c = Controller;
            if (c == null || !Cancellable) return;
            try
            {
                c.SelectionController.Cancel(false);
                c.PreviewController?.ResetAllPreviews();
                c.EncounterView.DisplayBoard?.Hide();
                c.EncounterInputHandler?.ResetHover(-1);
            }
            catch (Exception e) { CoreLog.Warning("Fight: cancelling the selection failed: " + e.Message); }
        }
    }
}
