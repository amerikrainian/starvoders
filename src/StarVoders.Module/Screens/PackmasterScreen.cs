using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using UnityEngine;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// Bud's Factory (<c>PackmasterScene</c>, <see cref="PackmasterController"/>), named by the game's
    /// <c>PackSceneTitle_Misc</c>. Stops: the cards on the belt under the pick panel's line ("Pick 2
    /// Cards"), left to right, each "name, cost, description[, selected]", Enter its click
    /// (<c>OnPressSelectCardButton</c>: picks or unpicks it), then Proceed (enabled once enough are
    /// picked; it adds them to the deck and leaves: never through the driver) and Reroll; the run's packs
    /// under the panel's "Packs" (its "?" tooltip as the description), each "name, description" (the
    /// game's <c>&lt;Pack&gt;_Misc</c>, <c>&lt;Pack&gt;_Desc_Misc</c>; drawn only as a picture), Enter its
    /// click (the Compendium, on that pack); the run status; the act timeline.
    ///
    /// The cards ride in on the belt, unclickable until they stop, so the screen holds its arrival
    /// (declaring nothing) until every shown card is interactable, 3 s at most.
    /// </summary>
    public sealed class PackmasterScreen : Screen
    {
        public override string Key => "packmaster";
        public override object InitialFocusStop => CardsStop;

        private const string CardsStop = "packmaster:cards";
        private const float MaxWait = 3f;

        private static PackmasterController Controller => GameScenes.Controller<PackmasterController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        public override string ScreenName => GameLoc.Get("PackSceneTitle_Misc");

        private float _pushedAt;

        public override void OnPush() => _pushedAt = Time.unscaledTime;

        public override bool HoldsArrival => !Settled(Controller);

        private bool Settled(PackmasterController c)
        {
            if (c == null || Time.unscaledTime - _pushedAt >= MaxWait) return true;
            var cards = Cards(c);
            if (cards.Count == 0) return false;
            foreach (var card in cards) if (!card._interactable) return false;
            return true;
        }

        // The cards shown on the belt, left to right.
        private static List<PackCardView> Cards(PackmasterController c)
        {
            var cards = new List<PackCardView>();
            if (c.CardViews != null)
                foreach (var card in c.CardViews)
                    if (ViewNodes.IsShown(card) && card.CardModel != null) cards.Add(card);
            cards.Sort((x, y) => x.transform.position.x.CompareTo(y.transform.position.x));
            return cards;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || !Settled(c)) return;

            b.BeginStop(CardsStop);
            b.PushContext(PickLine(c));
            foreach (var card in Cards(c))
            {
                var view = card;
                var vt = CardNodes.Card(view.CardModel, view);
                vt.Announcements = new List<NodeAnnouncement>(vt.Announcements) { GameNodes.SelectedPart(() => view._isSelected) };
                b.AddItem(ControlId.Structural("packmaster:card:" + view.GetInstanceID()), vt);
            }
            b.PopContext();
            // Proceed and Reroll answer the same choice: they follow the cards in the stop, unnumbered.
            b.PushContext(null, positions: false);
            var proceed = c.ProceedButton;
            if (ViewNodes.IsShown(proceed))
                b.AddItem(ControlId.Structural("packmaster:proceed"), ViewNodes.Button(proceed, enabled: () => proceed.ButtonEnabled));
            CampaignNodes.Reroll(b, GameScenes.Item(c, "RerollButton")?.TryCast<RerollButton>(), "packmaster:reroll",
                () => GameLoc.Get("RerollButtonPack_Desc_Misc"));
            b.PopContext();

            var packs = new List<PackView>();
            if (c.PackViews != null)
                foreach (var pack in c.PackViews)
                    if (ViewNodes.IsShown(pack)) packs.Add(pack);
            if (packs.Count > 0)
            {
                packs.Sort((x, y) => x.transform.position.x.CompareTo(y.transform.position.x));
                var frame = PacksFrame(c);
                b.BeginStop("packmaster:packs");
                b.PushContext(GameText.Of(frame?.Find("Title")?.GetComponent<TMP_Text>()) ?? GameLoc.Get("PackTitle_Misc"));
                foreach (var pack in packs)
                {
                    var view = pack;
                    Func<string> name = () => GameLoc.Get(view.ItemPackName + "_Misc") ?? view.ItemPackName.ToString();
                    var vt = ViewNodes.Button(view, name);
                    vt.Announcements = new List<NodeAnnouncement>(vt.Announcements)
                    {
                        GameNodes.TooltipPart(() => GameLoc.Get(view.ItemPackName + "_Desc_Misc")),
                    };
                    b.AddItem(ControlId.Structural("packmaster:pack:" + view.GetInstanceID()), vt);
                }
                var tip = frame?.GetComponentInChildren<StarVaders.TooltipTrigger>(true);
                string about = tip != null ? CampaignNodes.TooltipLine(tip) : null;
                if (!string.IsNullOrWhiteSpace(about))
                    b.AddItem(ControlId.Structural("packmaster:packsabout"), GameNodes.Text(() => about));
                b.PopContext();
            }

            CampaignNodes.Status(b, c, "packmaster");
            CampaignNodes.Map(b, c.MapPanel, "packmaster");
        }

        // "Pick 2 Cards", the panel over the belt.
        private static string PickLine(PackmasterController c)
        {
            foreach (var root in c.gameObject.scene.GetRootGameObjects())
            {
                var panel = root.transform.Find("PickPanelGroup/PickPanel");
                if (panel != null) return GameText.Of(panel.GetComponentInChildren<TMP_Text>());
            }
            return GameLoc.Get("Pick2Cards_Misc");
        }

        // The frame of the shown pack set (normal or modified), titled "Packs" with its "?".
        private static Transform PacksFrame(PackmasterController c)
        {
            foreach (var set in new[] { c.NormalPackSet, c.ModifiedPackSet })
                if (set != null && set.activeInHierarchy)
                {
                    var frame = set.transform.Find("Frame");
                    if (frame != null) return frame;
                }
            return null;
        }
    }
}
