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
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// Tippy's Quick Guide (<c>QuickGuideSceneAdditive</c>, <see cref="QuickGuideSceneController"/>, the
    /// tutorial choice's Quick Guide): three pages, then a run. One stop: the page ("1/3", Left and Right
    /// turning it as the arrows), its prose and demo artifact (<see cref="GuidePages"/>), its own buttons:
    /// Next page, Previous page, the first page's Back (out of the guide), the last page's START (the
    /// run begins: never through the driver). Escape is <see cref="QuickGuideSceneController.OnClickBack"/>;
    /// empty once leaving, and held until the tutorial choice it returns to is back.
    /// </summary>
    public sealed class QuickGuideScreen : Screen
    {
        public override string Key => "quickguide";
        public override int Layer => 10;
        public override bool Exclusive => true;
        public override string ScreenName => GameLoc.Get("QuickGuideTitle_Misc");

        private static QuickGuideSceneController Controller => GameScenes.Controller<QuickGuideSceneController>();

        // Leaving unloads the guide before the tutorial choice it returns to is loaded: held up, empty,
        // until that choice is there (2 s at most), so the main menu beneath is not announced between.
        private static float _leftAt = -1f;

        public override bool IsActive()
        {
            if (Controller != null) return true;
            if (_leftAt < 0f) return false;
            if (GameScenes.Controller<TutorialSelectionController>() != null || Time.realtimeSinceStartup - _leftAt > 2f)
            {
                _leftAt = -1f;
                return false;
            }
            return true;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Leave());
        }

        private static void Leave()
        {
            var c = Controller;
            if (c == null || c.IsEndingScene) return;
            _leftAt = Time.realtimeSinceStartup;
            try { c.OnClickBack(); }
            catch (Exception e) { CoreLog.Warning("Quick guide: leaving failed: " + e.Message); }
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.IsEndingScene || _leftAt >= 0f || c.Pages == null) return;
            int index = c._currentPage;
            if (index < 0 || index >= c.Pages.Count || c.Pages[index] == null) return;
            var page = c.Pages[index].transform;
            b.BeginStop("quickguide");
            b.PushContext(null, positions: false);
            b.AddItem(ControlId.Structural("quickguide:page"), Page(c));
            GuidePages.AddPage(b, page, "quickguide:" + index);
            // The page's own buttons, by name: Next, Previous (drawn "Back"), the first page's Back
            // (leaving), the last page's START.
            Add(b, page, "NextButton", () => Strings.ProfilesNextPage);
            Add(b, page, "PreviousButton", () => Strings.ProfilesPreviousPage);
            Add(b, page, "StartButton", null);
            var back = page.Find("BackButton")?.GetComponent<AViewItem>() ?? FindDeep(page, "BackButton");
            if (ViewNodes.IsShown(back))
                b.AddItem(ControlId.Structural("quickguide:back"), GameNodes.Button(() => Strings.Get("bind.ui.back"), Leave));
            b.PopContext();
        }

        private static void Add(GraphBuilder b, Transform page, string name, Func<string> label)
        {
            var item = FindDeep(page, name);
            if (ViewNodes.IsShown(item)) b.AddItem(ControlId.Structural("quickguide:" + name), ViewNodes.Button(item, label));
        }

        private static AViewItem FindDeep(Transform root, string name)
        {
            foreach (var item in root.GetComponentsInChildren<AViewItem>(false))
                if (item.gameObject.name == name) return item;
            return null;
        }

        // "1/3"; Left and Right turn the page, as the arrows.
        private static NodeVtable Page(QuickGuideSceneController c)
        {
            Func<string> label = () => GameText.Of(c.PageNumber);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { new NodeAnnouncement(label, live: true, kind: AnnouncementKinds.Label) },
                SearchText = label,
                OnAdjust = (sign, large) =>
                {
                    if (c.IsEndingScene) return;
                    try { if (sign > 0) c.OnClickNextPage(); else c.OnClickPreviousPage(); }
                    catch (Exception e) { CoreLog.Warning("Quick guide: turning the page failed: " + e.Message); }
                },
            };
        }
    }
}
