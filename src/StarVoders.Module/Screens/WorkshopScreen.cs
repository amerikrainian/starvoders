using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// Min's Workshop (<c>WorkshopScene</c>, <see cref="ShopController"/>), named by the game's
    /// <c>ShopTitle_Misc</c> (no title is drawn). Stops: the wares, the cards rack (top row first,
    /// each left to right) then the artifacts, each with its price in Stars ("disabled" while the
    /// price is drawn red: too dear), "Sale!" with the crossed-out price, "Bundle!" (two copies: the
    /// game adds the card twice and draws a second one behind it); Enter buys, the game's click.
    /// Unnumbered after them: Remove Card (opens the card viewer in RemoveCard mode), Restock, the tip
    /// jar (Min's Fundraiser), Min, the Gacha machine and the red button when shown, Exit. Then the
    /// run status and the act timeline. A bought ware stops being interactable and leaves the list.
    /// Min's lines (welcome, thanks, a tip's, a pat's) are speech bubbles: the popup reader's.
    /// The unlock canvas a full fundraiser shows is <see cref="MinUnlockScreen"/>'s.
    /// </summary>
    public sealed class WorkshopScreen : Screen
    {
        public override string Key => "workshop";
        public override object InitialFocusStop => "workshop:wares";

        private static ShopController Controller => GameScenes.Controller<ShopController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        public override string ScreenName => GameLoc.Get("ShopTitle_Misc");

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;

            b.BeginStop("workshop:wares");
            b.PushContext(null);
            var cards = new List<WorkshopCardView>();
            if (c.CardViews != null)
                foreach (var view in c.CardViews)
                    if (ViewNodes.IsShown(view) && view.IsInteractable && view.CardModel != null) cards.Add(view);
            cards.Sort(ByRowThenX);
            foreach (var view in cards)
            {
                var v = view;
                var vt = CardNodes.Card(v.CardModel, v);
                var price = Effective(v);
                Insert(vt, () => CardPrice(v), () => Affordable(price));
                b.AddItem(ControlId.Structural("workshop:card:" + v.GetInstanceID()), vt);
            }
            var artifacts = new List<WorkshopArtifactView>();
            if (c.ArtifactViews != null)
                foreach (var view in c.ArtifactViews)
                    if (ViewNodes.IsShown(view) && view.IsInteractable && view.ArtifactModel != null) artifacts.Add(view);
            artifacts.Sort(ByRowThenX);
            foreach (var view in artifacts)
            {
                var v = view;
                var vt = CardNodes.Artifact(v.ArtifactModel, v, clickable: true);
                Insert(vt, () => Stars(v.BuyButtonText), () => Affordable(v.BuyButtonText));
                b.AddItem(ControlId.Structural("workshop:artifact:" + v.GetInstanceID()), vt);
            }
            b.PopContext();

            // The workshop's own services follow the wares in the stop, unnumbered.
            b.PushContext(null, positions: false);
            var remove = c.RemoveCardButton;
            if (ViewNodes.IsShown(remove))
            {
                var cost = remove._costText ?? c.RemoveCardButtonText;
                var vt = ViewNodes.Button(remove, () => GameLoc.Get("RemoveButtonTitle_Misc") ?? ViewNodes.Label(remove));
                Insert(vt, () => Stars(cost), () => Affordable(cost));
                vt.Announcements = new List<NodeAnnouncement>(vt.Announcements) { GameNodes.TooltipPart(() => GameLoc.Get("RemoveButtonDesc_Misc")) };
                b.AddItem(ControlId.Structural("workshop:remove"), vt);
            }
            CampaignNodes.Reroll(b, c.RerollButton, "workshop:restock", () => GameLoc.Get("RestockButtonDesc_Misc"));
            Fundraiser(b, c.TipJarView);
            Min(b, c.MinView);
            Panel(b, c.GachaView, c.GachaView != null ? c.GachaView.MinFundraiserPanel : null, "workshop:gacha");
            Panel(b, c.RedButtonView, c.RedButtonView != null ? c.RedButtonView.MinFundraiserPanel : null, "workshop:redbutton");
            var exit = c.ProceedButton;
            if (ViewNodes.IsShown(exit))
                b.AddItem(ControlId.Structural("workshop:exit"), ViewNodes.Button(exit, enabled: () => exit.ButtonEnabled));
            b.PopContext();

            CampaignNodes.Status(b, c, "workshop");
            CampaignNodes.Map(b, c.MapPanel, "workshop");
        }

        // The racks are rows of wares: top row first, each left to right (y up; rows ~27 apart).
        private static int ByRowThenX(UnityEngine.Component a, UnityEngine.Component z)
        {
            int rowA = (int)Math.Round(-a.transform.position.y / 10f), rowZ = (int)Math.Round(-z.transform.position.y / 10f);
            return rowA != rowZ ? rowA.CompareTo(rowZ) : a.transform.position.x.CompareTo(z.transform.position.x);
        }

        // The price and the too-dear state right after the label: "Quick Fire!, 8900 Stars, Bundle!...".
        private static void Insert(NodeVtable vt, Func<string> price, Func<bool> affordable)
        {
            var parts = new List<NodeAnnouncement>(vt.Announcements);
            parts.Insert(1, new NodeAnnouncement(price, kind: AnnouncementKinds.Value));
            parts.Insert(2, GameNodes.DisabledPart(affordable));
            vt.Announcements = parts;
        }

        // A sale draws its price on the Sale tag and crosses out the button's.
        private static bool OnSale(WorkshopCardView v) => v.Sale != null && v.Sale.activeInHierarchy;

        private static WorkshopCostText Effective(WorkshopCardView v) => OnSale(v) ? v.SaleText : v.BuyButtonText;

        // "3700 Stars, Sale!, was 5700", "8900 Stars, Bundle!, 2 copies".
        private static string CardPrice(WorkshopCardView v)
        {
            var parts = new List<string>();
            string price = Stars(Effective(v));
            if (price != null) parts.Add(price);
            if (OnSale(v))
            {
                parts.Add(GameLoc.Get("Sale_Misc"));
                string was = GameText.Of(v.BuyButtonText != null ? v.BuyButtonText.Text : null);
                if (was != null) parts.Add(Strings.ShopWas(was));
            }
            if (v.IsBundle)
            {
                parts.Add(GameLoc.Get("Bundle_Misc"));
                parts.Add(Strings.ShopCopies(2));
            }
            parts.RemoveAll(string.IsNullOrEmpty);
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        // "8900 Stars": the price as drawn (the model's is a hundredth of it).
        private static string Stars(WorkshopCostText cost)
        {
            string drawn = GameText.Of(cost != null ? cost.Text : null);
            if (drawn == null) return null;
            string stars = GameLoc.Get("StarPanelTitle_Misc");
            return int.TryParse(drawn, out int n) && stars != null ? Strings.FightCost(n, stars) : drawn;
        }

        // WorkshopCostText.Update draws a price the player cannot pay in red, else its original colour.
        private static bool Affordable(WorkshopCostText cost)
        {
            if (cost == null || cost.Text == null) return true;
            return cost.Text.color == cost.originalColor;
        }

        /// <summary>The tip jar, by the word drawn on it ("Tip"): "Min's Fundraiser 2000/6000", read live
        /// (a tip or a purchase moves it), "Click Me!" when the jar asks for it (full: the click unlocks);
        /// the panel's description and "+1000 each purchase", then the next unlock, in the ui buffer.
        /// The click tips 1000 Stars while the jar is not full (a refusal sound below 1000). The figures
        /// are the game's (<see cref="LoggerDataSO.WorkshopXP"/> over
        /// <see cref="WorkshopLevelManager.XpForCurrentLevel"/>, drawn a hundredfold), never the
        /// jar's own <see cref="TipJarView.MinFundraiserPanel"/>: that one is a template, left at its
        /// first figures; the shown panel is a fresh copy on the display board per hover.</summary>
        private static void Fundraiser(GraphBuilder b, TipJarView jar)
        {
            if (!ViewNodes.IsShown(jar)) return;
            var panel = jar.MinFundraiserPanel;
            Func<string> title = () => PanelText(panel, "Title") ?? GameLoc.Get("MinFundraiserTitle_Misc");
            var vt = ViewNodes.Button(jar, () => GameText.Of(jar._tipText) ?? title());
            var parts = new List<NodeAnnouncement>(vt.Announcements)
            {
                new NodeAnnouncement(() =>
                {
                    string figures = Figures();
                    return figures != null ? title() + " " + figures : title();
                }, live: true, kind: AnnouncementKinds.Value),
                new NodeAnnouncement(() => jar._clickMeText != null && jar._clickMeText.gameObject.activeInHierarchy
                    ? GameText.Of(jar._clickMeText) : null, live: true, kind: AnnouncementKinds.Value),
                GameNodes.TooltipPart(() => PanelText(panel, "Description")),
                GameNodes.TooltipPart(() => PanelText(panel, "TipDescription")),
            };
            vt.Announcements = parts;
            vt.Details = NextUnlock;
            b.AddItem(ControlId.Structural("workshop:tipjar"), vt);
        }

        // "2000/6000", as the fundraiser panel draws it.
        private static string Figures()
        {
            try
            {
                var logger = DataManager.LoggerDataSO;
                if (logger == null) return null;
                return (logger.WorkshopXP * 100) + "/" + (WorkshopLevelManager.XpForCurrentLevel() * 100);
            }
            catch (Exception e)
            {
                Core.CoreLog.Warning("Workshop: reading the fundraiser failed: " + e.Message);
                return null;
            }
        }

        // What the full fundraiser unlocks, as its panel previews it: each card and artifact with its
        // description.
        private static IEnumerable<string> NextUnlock()
        {
            var lines = new List<string>();
            try
            {
                if (!WorkshopLevelManager.TryGetNextUnlock(out UnlockData unlock) || unlock == null) return lines;
                if (unlock.CardNames != null)
                    foreach (var name in unlock.CardNames)
                    {
                        string n = GameLoc.Get(name + "_CardTitle") ?? name.ToString(), d = GameLoc.Get(name + "_CardDesc");
                        lines.Add(d != null ? n + ": " + d : n);
                    }
                if (unlock.ArtifactNames != null)
                    foreach (var name in unlock.ArtifactNames)
                    {
                        string n = GameLoc.ArtifactName(name) ?? name.ToString(), d = GameLoc.Get(name + "_ArtiDesc");
                        lines.Add(d != null ? n + ": " + d : n);
                    }
            }
            catch (Exception e) { Core.CoreLog.Warning("Workshop: reading the next unlock failed: " + e.Message); }
            return lines;
        }

        /// <summary>Min herself: her click is a line in a speech bubble (the popup reader says it) and
        /// a blush. No caption is drawn; she is named as her speech is.</summary>
        private static void Min(GraphBuilder b, MinView min)
        {
            if (!ViewNodes.IsShown(min)) return;
            b.AddItem(ControlId.Structural("workshop:min"), ViewNodes.Button(min, () => "Min"));
        }

        /// <summary>A workshop machine (the Gacha machine, the red button) by its hover panel: the
        /// panel's title as the label (its only text when it has no title), the rest as description.</summary>
        private static void Panel(GraphBuilder b, AViewItem item, UnityEngine.Component panel, string id)
        {
            if (!ViewNodes.IsShown(item)) return;
            var vt = ViewNodes.Button(item, () => PanelText(panel, "Title") ?? PanelText(panel, "Text") ?? ViewNodes.Label(item));
            vt.Announcements = new List<NodeAnnouncement>(vt.Announcements)
            {
                GameNodes.TooltipPart(() => PanelText(panel, "Description")),
            };
            b.AddItem(ControlId.Structural(id), vt);
        }

        // A named text of a hover panel, shown or not (the panel is only shown while hovered).
        private static string PanelText(UnityEngine.Component panel, string name)
        {
            if (panel == null) return null;
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
                if (tmp.gameObject.name == name) return GameText.Of(tmp);
            return null;
        }
    }
}
