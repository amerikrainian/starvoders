using System;
using System.Collections.Generic;
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
    /// The guide (<c>GuideSceneAdditive</c>, <see cref="GuideSceneController"/>, the pause menu's Show
    /// Guide). Stops: the tabs down its left side (Combat, Gunner, Stinger, Keeper: a locked one is drawn
    /// "? ? ?" and reads LOCKED, never clicked: its click only shakes the panel); landing on a tab shows
    /// its page, as its click (<see cref="GuideSceneController.OnPressButton"/>); the shown page
    /// (<c>GuidePages[currentPageIndex]</c>), its texts in hierarchy order (the speech bubbles top to
    /// bottom: the texts named Line1..Line4, the rest being the pictures' labels), each line its own
    /// entry, a demo artifact as the card viewer reads it; then Back. Escape is
    /// <see cref="GuideSceneController.LeaveScene"/>; once leaving, the screen stays up empty until the
    /// scene unloads, as the Compendium pages do, so the pause menu arrives once.
    /// </summary>
    public sealed class GuideScreen : Screen
    {
        public override string Key => "guide";
        public override int Layer => 35;
        public override bool Exclusive => true;
        public override object InitialFocusStop => PageStop;
        public override string ScreenName => GameLoc.Get("GuideTitle_Misc");

        private const string PageStop = "guide:page";

        // The left side's buttons, each the page its click shows; the locked twins show none.
        private static readonly string[] Tabs = { "General", "Gunner", "Stinger", "StingerLocked", "Keeper", "KeeperLocked", "Advanced" };

        private static GuideSceneController Controller => GameScenes.Controller<GuideSceneController>();

        public override bool IsActive() => Controller != null;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Leave());
        }

        private static void Leave()
        {
            var c = Controller;
            if (c == null || c.IsEndingScene) return;
            try { c.LeaveScene(); }
            catch (Exception e) { CoreLog.Warning("Guide: leaving failed: " + e.Message); }
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.IsEndingScene) return;

            b.BeginStop("guide:tabs");
            b.PushContext(null);
            var side = c.MainPanel != null ? c.MainPanel.Find("LeftSide") : null;
            if (side != null)
                foreach (var name in Tabs)
                {
                    var tab = side.Find(name)?.GetComponent<AViewItem>();
                    if (ViewNodes.IsShown(tab)) b.AddItem(ControlId.Structural("guide:tab:" + name), Tab(c, tab, name));
                }
            b.PopContext();

            b.BeginStop(PageStop);
            b.PushContext(null, positions: false);
            var pages = c.GuidePages;
            int index = c.currentPageIndex;
            if (pages != null && index >= 0 && index < pages.Count && pages[index] != null)
            {
                GuidePages.AddPage(b, pages[index].transform, "guide");
            }
            var back = c.MainPanel != null ? c.MainPanel.Find("BackButton (5)")?.GetComponent<AViewItem>() : null;
            if (ViewNodes.IsShown(back))
                b.AddItem(ControlId.Structural("guide:back"), GameNodes.Button(() => Strings.Get("bind.ui.back"), Leave));
            b.PopContext();
        }

        // A tab: its page is the one its name stands for; a locked one has none.
        private static NodeVtable Tab(GuideSceneController c, AViewItem tab, string name)
        {
            bool locked = name.EndsWith("Locked", StringComparison.Ordinal) || ViewNodes.Label(tab).Trim('?', ' ').Length == 0;
            int page = name == "General" ? 0 : name == "Gunner" ? 1 : name == "Stinger" ? 2 : name == "Keeper" ? 3 : -1;
            Func<string> label = () => locked ? GameLoc.Get("LockedTitle_Misc") : ViewNodes.Label(tab);
            Action pick = () => { if (!locked && page >= 0 && c.currentPageIndex != page) ViewNodes.Click(tab); };
            return new NodeVtable
            {
                ControlType = ControlTypes.Tab,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                IsSelected = () => page >= 0 && c.currentPageIndex == page,
                SearchText = label,
                OnActivate = pick,
                OnFocus = () => { ViewNodes.Hover(tab); pick(); },
            };
        }
    }
}
