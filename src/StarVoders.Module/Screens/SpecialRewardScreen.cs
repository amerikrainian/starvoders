using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using UnityEngine;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// A special reward after a fight (<c>SpecialRewardScene</c>, <see cref="SpecialRewardController"/>,
    /// kind in the static <c>CurrentSpecialReward</c>: legendaries, upgrades, pilot cards, artifacts),
    /// named by its title ("Special"). Stops: the act timeline and the run status as strips, then what it
    /// gives (the landing) under its description ("You will gain the
    /// following LEGENDARY cards!"), left to right, cards and artifacts as the reward screen reads them
    /// (display only: the reward is taken whole), then Take All (<c>AcceptRewardViewItem</c>), Skip (the
    /// base <c>ProceedButton</c>, its "+1000" Stars) and Reroll. Taking
    /// or skipping changes the run: never through the driver. Up until <c>IsEndingScene</c>; the box
    /// opens on arrival, so the screen holds (declaring nothing) until its texts and rewards show.
    /// </summary>
    public sealed class SpecialRewardScreen : Screen
    {
        public override string Key => "specialreward";
        public override IReadOnlyList<InputCategory> InputCategories => RunKeys.Categories;

        private const string RewardsStop = "specialreward:rewards";
        private const float MaxWait = 3f;

        private static SpecialRewardController Controller => GameScenes.Controller<SpecialRewardController>();

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
                var texts = c != null ? c.TextesObject : null;
                foreach (var tmp in texts != null ? texts.GetComponentsInChildren<TMP_Text>(false) : new TMP_Text[0])
                    if (tmp != c.SpecialRewardDescription) return GameText.Of(tmp);
                return null;
            }
        }

        private float _pushedAt;

        public override void OnPush() => _pushedAt = Time.unscaledTime;

        public override bool HoldsArrival => !Settled(Controller);

        private bool Settled(SpecialRewardController c)
        {
            if (c == null || Time.unscaledTime - _pushedAt >= MaxWait) return true;
            return c.TextesObject != null && c.TextesObject.activeInHierarchy && Rewards(c).Count > 0;
        }

        // The reward boards shown, left to right.
        private static List<AViewItem> Rewards(SpecialRewardController c)
        {
            var list = new List<AViewItem>();
            if (c._rewardViewItems != null)
                foreach (var item in c._rewardViewItems)
                    if (ViewNodes.IsShown(item)) list.Add(item);
            list.Sort((x, y) => x.transform.position.x.CompareTo(y.transform.position.x));
            return list;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || !Settled(c)) return;

            b.BeginStop(RewardsStop);
            CampaignNodes.Strips(b, c, "specialreward");
            b.PushContext(GameText.Of(c.SpecialRewardDescription));
            bool first = true;
            // One strip, as the box lays them out.
            var rewards = Rewards(c).FindAll(item => item.TryCast<CardRewardBoard>() != null || item.TryCast<ArtifactRewardBoard>() != null);
            if (rewards.Count > 0) b.StartRow("specialreward:rewards");
            foreach (var item in rewards)
            {
                var card = item.TryCast<CardRewardBoard>();
                if (card != null)
                {
                    var vt = CardNodes.Card(card.GetCardModel(), card);
                    vt.OnActivate = null;
                    var id = ControlId.Structural("specialreward:card:" + item.GetInstanceID());
                    b.AddItem(id, vt);
                    if (first) b.SetStart(id); // the landing: the first reward, under the run's strips
                    first = false;
                    continue;
                }
                var artifact = item.TryCast<ArtifactRewardBoard>();
                if (artifact != null)
                {
                    var id = ControlId.Structural("specialreward:artifact:" + item.GetInstanceID());
                    b.AddItem(id, CardNodes.Artifact(artifact.GetArtifactModel(), artifact));
                    if (first) b.SetStart(id);
                    first = false;
                }
            }
            if (rewards.Count > 0) b.EndRow();
            b.PopContext();
            // Take All, Skip and Reroll answer the same offer: they follow it in the stop, unnumbered.
            b.PushContext(null, positions: false);
            var accept = c.AcceptRewardViewItem;
            if (ViewNodes.IsShown(accept))
            {
                var pressed = accept.TryCast<PressedButtonView>();
                b.AddItem(ControlId.Structural("specialreward:accept"),
                    ViewNodes.Button(accept, enabled: pressed != null ? () => pressed.ButtonEnabled : (Func<bool>)null));
            }
            var skip = c.ProceedButton;
            if (ViewNodes.IsShown(skip))
                b.AddItem(ControlId.Structural("specialreward:skip"), ViewNodes.Button(skip, () => SkipLabel(skip), enabled: () => skip.ButtonEnabled));
            CampaignNodes.Reroll(b, c.RerollButton, "specialreward:reroll");
            b.PopContext();
        }

        // "Skip, +1000": the caption and the Stars it pays, the star glyph ("=") dropped.
        private static string SkipLabel(AViewItem skip)
        {
            var parts = new List<string>();
            foreach (var tmp in skip.GetComponentsInChildren<TMP_Text>(false))
            {
                string text = GameText.Of(tmp)?.Replace("=", "").Trim();
                if (!string.IsNullOrEmpty(text) && text.Length > 1 && !parts.Contains(text)) parts.Add(text);
            }
            return string.Join(", ", parts);
        }
    }
}
