using System;
using System.Collections.Generic;
using HarmonyLib;
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
    /// An unlock card at the end of a run (the run end's Unlock button: <see cref="UnlockCanvas"/>,
    /// <c>CampaignEndController._currUnlockCanvas</c>, one card per unlock), as a dialog named by the
    /// card's title ("Pilot Unlocked!", "New SET Unlocked!"). Its kinds are sub-canvases: a pilot
    /// (<c>SingleUnlockCanvas</c>, the pilot drawn only as a portrait: read by name and the game's
    /// subtitle, "Noel, The Coolheaded Scholar"), a level's set (<c>SetUnlockCanvas</c>, its card and
    /// artifact views), a mechanic (<c>MiscUnlockCanvas</c>), a pack (<c>PackUnlockCanvas</c>, its
    /// cards and artifacts). Then the description lines, each card or artifact as one item of its
    /// drawn texts, and OK (the game's word), which is the card's click: the next card, or back to the
    /// run end. Escape too.
    ///
    /// The card fades in and its description arrives by callback after it opens, so the screen
    /// declares nothing until the card is settled (3 s at most), else focus lands on an empty line.
    /// The portrait keeps no pilot name, so <see cref="Pilots"/> records it as the game sets it.
    /// </summary>
    public sealed class UnlockCardScreen : Screen
    {
        public override string Key => "unlockcard";
        public override int Layer => 15;
        public override bool Exclusive => true;
        public override bool AllowsTypeahead => false;

        private const float Settled = 0.99f, MaxWait = 3f;

        private static UnlockCanvas Card
        {
            get
            {
                var c = GameScenes.Controller<CampaignEndController>();
                var card = c != null ? c._currUnlockCanvas : null;
                return card != null && card.gameObject.activeInHierarchy ? card : null;
            }
        }

        public override bool IsActive() => Card != null;

        // The card (by pointer and kind) first seen when, for the settle cap.
        private string _seenKey;
        private float _seenAt;

        public override bool HoldsArrival => !IsSettled(Card);

        private bool IsSettled(UnlockCanvas card)
        {
            if (card == null) return true;
            var kind = Kind(card);
            string key = card.Pointer + ":" + (kind != null ? kind.name : "");
            if (key != _seenKey) { _seenKey = key; _seenAt = Time.unscaledTime; }
            if (Time.unscaledTime - _seenAt >= MaxWait) return true;
            if (card.IsTransitioning || kind == null) return false;
            if (card.CanvasGroup != null && card.CanvasGroup.alpha < Settled) return false;
            foreach (var line in Lines(card, kind)) if (!string.IsNullOrWhiteSpace(GameText.Of(line))) return true;
            return false;
        }

        // The sub-canvas shown: one per kind of unlock.
        private static GameObject Kind(UnlockCanvas card)
        {
            foreach (var go in new[] { card.SingleUnlockCanvas, card.SetUnlockCanvas, card.MiscUnlockCanvas, card.PackUnlockCanvas })
                if (go != null && go.activeInHierarchy) return go;
            return null;
        }

        private static TMP_Text Title(UnlockCanvas card, GameObject kind)
        {
            if (kind == card.MiscUnlockCanvas) return card.MiscUnlockCanvasTitle;
            if (kind == card.PackUnlockCanvas) return card.PackUnlockTitle;
            foreach (var tmp in kind.GetComponentsInChildren<TMP_Text>(false))
                if (tmp != null && tmp.name == "UnlockTitle") return tmp;
            return null;
        }

        // The texts under the title, in reading order.
        private static IEnumerable<TMP_Text> Lines(UnlockCanvas card, GameObject kind)
        {
            if (kind == card.SingleUnlockCanvas) yield return card.SingleUnlockDescription;
            else if (kind == card.SetUnlockCanvas) yield return card.SetUnlockDescription;
            else if (kind == card.MiscUnlockCanvas) { yield return card.MiscUnlockCanvasMiddle; yield return card.MiscUnlockCanvasBottom; }
            else if (kind == card.PackUnlockCanvas) yield return card.PackUnlockDesc;
        }

        // The cards and artifacts the card shows.
        private static IEnumerable<AViewItem> Views(UnlockCanvas card, GameObject kind)
        {
            if (kind == card.SetUnlockCanvas)
            {
                foreach (var v in new AViewItem[] { card.CardUnlockView1, card.CardUnlockView2, card.CardUnlockView3 }) yield return v;
                foreach (var v in new AViewItem[] { card.ArtifactUnlockView1, card.ArtifactUnlockView2, card.ArtifactUnlockView3 }) yield return v;
            }
            else if (kind == card.PackUnlockCanvas)
            {
                if (card.PackDeckCardViews != null) foreach (var v in card.PackDeckCardViews) yield return v;
                if (card.PackDeckArtifactViews != null) foreach (var v in card.PackDeckArtifactViews) yield return v;
            }
        }

        public override void Build(GraphBuilder b)
        {
            var card = Card;
            if (card == null || !IsSettled(card)) return;
            var kind = Kind(card);
            if (kind == null) return;
            string id = "unlockcard:" + card.Pointer + ":" + kind.name + ":";

            b.PushContext(GameText.Of(Title(card, kind)), Strings.RoleDialog, positions: false);
            if (kind == card.SingleUnlockCanvas)
            {
                string pilot = Pilots.Of(card.PilotFrontPortraitView);
                if (pilot != null) b.AddItem(ControlId.Structural(id + "pilot"), GameNodes.Text(() => pilot));
            }
            int n = 0;
            foreach (var view in Views(card, kind))
            {
                n++;
                if (!ViewNodes.IsShown(view)) continue;
                var v = view;
                Func<string> text = () => ViewText(v);
                if (string.IsNullOrWhiteSpace(text())) continue;
                var vt = GameNodes.Text(text);
                vt.OnFocus = () => ViewNodes.Hover(v);
                b.AddItem(ControlId.Structural(id + "view:" + n), vt);
            }
            n = 0;
            foreach (var line in Lines(card, kind))
            {
                n++;
                var tmp = line;
                if (tmp == null || !tmp.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(GameText.Of(tmp))) continue;
                b.AddItem(ControlId.Structural(id + "line:" + n), GameNodes.Text(() => GameText.Of(tmp)));
            }
            b.AddItem(ControlId.Structural(id + "ok"), GameNodes.Button(() => GameLoc.Get("OK_Misc") ?? "OK", () => Continue(card)));
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Continue(Card));
        }

        private static void Continue(UnlockCanvas card)
        {
            if (card == null || card.IsTransitioning) return;
            ViewNodes.Click(card);
        }

        // A card or artifact as drawn: its shown texts, in order (one-letter key prompts skipped).
        private static string ViewText(AViewItem view)
        {
            var parts = new List<string>();
            foreach (var tmp in view.GetComponentsInChildren<TMP_Text>(false))
            {
                string text = GameText.Of(tmp);
                if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 2 || parts.Contains(text)) continue;
                parts.Add(text);
            }
            return string.Join(", ", parts);
        }

        /// <summary>The pilot each portrait shows ("Noel, The Coolheaded Scholar": pilots have no
        /// localized name, the game draws it, but a localized subtitle), recorded as the game sets the
        /// portrait, by the view's native pointer.</summary>
        [HarmonyPatch]
        internal static class Pilots
        {
            private static readonly Dictionary<IntPtr, string> s_pilots = new Dictionary<IntPtr, string>();

            public static string Of(PilotFrontPortraitView view)
                => view != null && s_pilots.TryGetValue(view.Pointer, out var pilot) ? pilot : null;

            [HarmonyPatch(typeof(PilotFrontPortraitView), nameof(PilotFrontPortraitView.SetPortraitView))]
            [HarmonyPrefix]
            private static void Record(PilotFrontPortraitView __instance, PilotName pilotName)
            {
                try
                {
                    if (__instance == null) return;
                    string name = pilotName.ToString();
                    string subtitle = GameLoc.Get(name + "_Subtitle_Misc");
                    s_pilots[__instance.Pointer] = subtitle != null ? name + ", " + subtitle : name;
                }
                catch (Exception e) { CoreLog.Warning("Unlock: naming the pilot failed: " + e.Message); }
            }
        }
    }
}
