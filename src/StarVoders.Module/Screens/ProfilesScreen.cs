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
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The save profiles (<c>SaveProfileSceneAdditive</c>, <see cref="SaveProfileSceneController"/>, the
    /// main menu's "File - 1"), titled by the game's PROFILES. One stop: the page's three slots, each "File
    /// 1, 1% Unlocked, 0% Completed" (selected when it is the profile in use) or "CREATE NEW PROFILE";
    /// Enter is the slot's click (switching to that profile, or creating one); a slot with a save is
    /// followed by its delete button ("Delete File 1": the game asks first); the page arrows; Back.
    /// Escape is <see cref="SaveProfileSceneController.OnClickBack"/>; empty once leaving.
    /// Switching, creating and deleting change the saves: never through the driver.
    /// </summary>
    public sealed class ProfilesScreen : Screen
    {
        public override string Key => "profiles";
        public override int Layer => 10;
        public override bool Exclusive => true;
        public override string ScreenName => GameLoc.Get("Profiles_Misc");

        private static SaveProfileSceneController Controller => GameScenes.Controller<SaveProfileSceneController>();

        public override bool IsActive() => Controller != null;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Leave());
        }

        private static void Leave()
        {
            var c = Controller;
            if (c == null || c._isEndingScene) return;
            try { c.OnClickBack(); }
            catch (Exception e) { CoreLog.Warning("Profiles: leaving failed: " + e.Message); }
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c._isEndingScene) return;
            b.BeginStop("profiles");
            b.PushContext(null, positions: false);
            var slots = new[] { c._saveProfileSlot0, c._saveProfileSlot1, c._saveProfileSlot2 };
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (!ViewNodes.IsShown(slot)) continue;
                b.AddItem(ControlId.Structural("profiles:slot" + i), Slot(slot));
                var delete = slot._deleteButton != null && slot._deleteButton.activeInHierarchy ? slot._deleteButton.GetComponent<AViewItem>() : null;
                if (ViewNodes.IsShown(delete))
                    b.AddItem(ControlId.Structural("profiles:delete" + i), ViewNodes.Button(delete, () => Strings.ProfilesDelete(GameText.Of(slot._fileNumText))));
            }
            if (ViewNodes.IsShown(c._leftArrowView))
                b.AddItem(ControlId.Structural("profiles:prev"), ViewNodes.Button(c._leftArrowView, () => Strings.ProfilesPreviousPage));
            if (ViewNodes.IsShown(c._rightArrowView))
                b.AddItem(ControlId.Structural("profiles:next"), ViewNodes.Button(c._rightArrowView, () => Strings.ProfilesNextPage));
            b.AddItem(ControlId.Structural("profiles:back"), GameNodes.Button(() => Strings.Get("bind.ui.back"), Leave));
            b.PopContext();
        }

        // "File 1, 1% Unlocked, 0% Completed[, selected]", or the empty slot's CREATE NEW PROFILE.
        private static NodeVtable Slot(SaveProfileGroup slot)
        {
            Func<string> label = () =>
            {
                if (slot.IsEmpty())
                {
                    var empty = slot._emptyProfileGroup != null ? slot._emptyProfileGroup.GetComponentInChildren<TMP_Text>(false) : null;
                    return GameText.Of(empty) ?? GameLoc.Get("CreateNewProfile_Misc");
                }
                var parts = new List<string>();
                foreach (var t in new[] { slot._fileNumText, slot._unlockedText, slot._completedText })
                {
                    string s = t != null && t.gameObject.activeInHierarchy ? GameText.Of(t) : null;
                    if (!string.IsNullOrEmpty(s)) parts.Add(s);
                }
                return string.Join(", ", parts);
            };
            var vt = ViewNodes.Button(slot, label);
            vt.Announcements = new List<NodeAnnouncement>(vt.Announcements) { GameNodes.SelectedPart(() => !slot.IsEmpty() && slot.IsSelected()) };
            return vt;
        }
    }
}
