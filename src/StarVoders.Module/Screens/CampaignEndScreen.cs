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
    /// The end of a run (<c>CampaignLoseScene</c>, <c>CampaignWinScene</c>; one
    /// <see cref="CampaignEndController"/>). Its heading ("Defeat..?") names the screen; then the
    /// mech's level ("Gunner, lv 0", where focus lands) and its experience ("Exp: 10/500", or the max
    /// line at the top level) as the panel draws them, two items read live: the experience counts up
    /// while the bar fills, so landing on the level keeps the count quiet until it is asked for; then
    /// the buttons that show: Unlock when there is something to unlock,
    /// Proceed, View Stats, Quick Restart, each the game's click. While an unlock card is up
    /// (<see cref="UnlockCanvas"/>) it is <see cref="UnlockCardScreen"/>'s, and this screen is empty.
    /// </summary>
    public sealed class CampaignEndScreen : Screen
    {
        public override string Key => "campaignend";

        private static CampaignEndController Controller => GameScenes.Controller<CampaignEndController>();

        public override bool IsActive() => Controller != null;

        public override string ScreenName
        {
            get
            {
                var c = Controller;
                if (c == null) return null;
                foreach (var root in c.gameObject.scene.GetRootGameObjects())
                    foreach (var name in new[] { "DefeatText", "VictoryText", "WinText" })
                    {
                        var t = Find(root.transform, name);
                        if (t != null && t.gameObject.activeInHierarchy) return GameText.Of(t.GetComponentInChildren<TMP_Text>());
                    }
                return null;
            }
        }

        private static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = Find(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;

            var unlock = c._currUnlockCanvas;
            if (unlock != null && unlock.gameObject.activeInHierarchy) return;

            var level = c.MechLevelView;
            if (level != null && level.gameObject.activeInHierarchy)
            {
                b.AddItem(ControlId.Structural("campaignend:level"), Live(() => LevelText(level)));
                if (!string.IsNullOrEmpty(ExpText(level)))
                    b.AddItem(ControlId.Structural("campaignend:exp"), Live(() => ExpText(level)));
            }

            b.PushContext(null, positions: false);
            foreach (var name in new[] { "UnlockButton", "ProceedButton", "StatsButton", "RestartButton" })
            {
                // Unlock's view item is its child ButtonTemplate, under the controller's UnlockButton.
                var item = name == "UnlockButton"
                    ? (c.UnlockButton != null ? c.UnlockButton.GetComponentInChildren<AViewItem>() : null)
                    : GameScenes.Item(c, name);
                if (!ViewNodes.IsShown(item)) continue;
                var pressed = item.TryCast<PressedButtonView>();
                b.AddItem(ControlId.Structural("campaignend:" + name),
                    ViewNodes.Button(item, enabled: pressed != null ? () => pressed.ButtonEnabled : (Func<bool>)null));
            }
            b.PopContext();
        }

        private static NodeVtable Live(Func<string> text) => new NodeVtable
        {
            ControlType = ControlTypes.Text,
            Announcements = new List<NodeAnnouncement> { new NodeAnnouncement(text, live: true, kind: AnnouncementKinds.Label) },
            SearchText = text,
        };

        // "Gunner, lv 0", in the panel's own words.
        private static string LevelText(MechLevelView level)
        {
            var parts = new List<string>();
            Add(parts, GameText.Of(level.ClassTitleText));
            var lv = level.LevelDiamond != null ? level.LevelDiamond.Find("lvText") : null;
            string lvWord = lv != null ? GameText.Of(lv.GetComponent<TMP_Text>()) : null;
            string number = GameText.Of(level.LevelNumberText);
            Add(parts, lvWord != null && number != null ? lvWord + " " + number : number);
            return string.Join(", ", parts);
        }

        // "Exp: 10/500", in the panel's own words; its "max" line once the mech is at the top level.
        private static string ExpText(MechLevelView level)
        {
            var parts = new List<string>();
            if (level.MaxReachedHolder != null && level.MaxReachedHolder.activeInHierarchy)
            {
                foreach (var tmp in level.MaxReachedHolder.GetComponentsInChildren<TMP_Text>(false)) Add(parts, GameText.Of(tmp));
            }
            else if (level.ExpTextHolder == null || level.ExpTextHolder.activeInHierarchy)
            {
                var label = level.CurrentExpText != null ? level.CurrentExpText.transform.parent : null;
                string word = label != null ? GameText.Of(label.GetComponent<TMP_Text>()) : null;
                string current = GameText.Of(level.CurrentExpText), max = GameText.Of(level.MaxExpText);
                string figure = current != null && max != null ? current + "/" + max : current;
                Add(parts, word != null && figure != null ? word + " " + figure : figure);
            }
            return string.Join(", ", parts);
        }

        private static void Add(List<string> parts, string text)
        {
            if (!string.IsNullOrEmpty(text) && !parts.Contains(text)) parts.Add(text);
        }
    }
}
