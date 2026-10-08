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

        /// <summary>The keyword explanations a card or entity carries (its info panel's lines:
        /// "Danger Zone: Tiles that will be struck..."), from the game's <c>*_InfoLine</c> strings.</summary>
        public static List<string> InfoLines(Il2CppSystem.Collections.Generic.HashSet<MoreInfoWordName> words)
        {
            var lines = new List<string>();
            if (words == null) return lines;
            try
            {
                foreach (var word in words)
                {
                    string line = GameLoc.Get(word + "_InfoLine");
                    if (line != null && !lines.Contains(line)) lines.Add(line);
                }
            }
            catch (Exception e) { CoreLog.Warning("Fight: reading the info lines failed: " + e.Message); }
            return lines;
        }

        // ---- the fight's figures ----

        /// <summary>One of the fight's figures as the model holds it (<see cref="EncounterModel.Values"/>),
        /// never as a panel draws it: hovering a card or End Turn makes the panels preview what it
        /// would do ("Doom 5/5" while the model is at 3).</summary>
        public static int? Value(EncounterValue key)
        {
            var values = Model?.Values;
            try { return values != null && values.ContainsKey(key) ? values[key] : (int?)null; }
            catch { return null; }
        }

        /// <summary>The Doom that loses the fight: the "/5" the Doom panel draws after its figure,
        /// which a preview never changes.</summary>
        public static int? DoomMax()
        {
            string text = GameText.Of(View?.DoomView?.DoomText);
            int slash = text != null ? text.LastIndexOf('/') : -1;
            return slash >= 0 && int.TryParse(text.Substring(slash + 1).Trim(), out int max) ? max : (int?)null;
        }

        /// <summary>"Doom 3/5", "Heat 1/3", "Power 2/4", "Mana 3/5": the game's title and the model's
        /// figure over its maximum; null when the fight has no such figure.</summary>
        public static string Figure(string which)
        {
            int? value, max;
            switch (which)
            {
                case "Doom": value = Value(EncounterValue.Doom); max = DoomMax(); break;
                case "Heat": value = Value(EncounterValue.Heat); max = Value(EncounterValue.MaxHeat); break;
                case "Power": value = Value(EncounterValue.Power); max = Value(EncounterValue.MaxPower); break;
                default: value = Value(EncounterValue.Mana); max = Value(EncounterValue.MaxMana); break;
            }
            if (value == null) return null;
            string figure = max.HasValue && max.Value > 0 ? Core.Strings.Strings.Position(value.Value, max.Value) : value.Value.ToString();
            string title = GameLoc.Get(which + "Title_Misc");
            return title != null ? title + " " + figure : figure;
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
