using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// An achievement earned, held as a dialog until dismissed: the game's popup is a toast (slid in
    /// for a few seconds and out again, over whatever is up, with no button), easy to miss in speech.
    /// "Achievement unlocked, dialog", then the popup's title and description as the game wrote them,
    /// then OK (the game's own word, <c>OK_Misc</c>); OK or Escape dismisses it, showing the next one
    /// when several came at once. Fed by <see cref="Readers.PopupReader"/>; nothing in the game
    /// changes, it only holds the mod's focus. The game shows queued achievements one after another,
    /// seconds apart, so after a dismissal the screen stays up, empty, while the game still has more
    /// waiting (<see cref="AchievementPopupManager._popupQueue"/>, or a popup it took off the queue whose
    /// texts are not in yet), 8 seconds at most: else the screen beneath would be announced between each.
    /// </summary>
    public sealed class AchievementScreen : Screen
    {
        public override string Key => "achievement";
        public override int Layer => 45;
        public override bool Exclusive => true;
        public override bool KeptThroughTransitions => true;
        public override bool AllowsTypeahead => false;

        private sealed class Entry
        {
            public int Id;
            public string Title;
            public string Description;
        }

        private static readonly Queue<Entry> s_queue = new Queue<Entry>();
        private static int s_next;
        private const float HoldFor = 8f;
        private static float s_holdUntil;
        // The game's queue as last seen, and the popups it has taken off it that have not reached the
        // dialog yet: it dequeues one, then spawns its block, whose texts arrive later still.
        private static int s_lastGameQueue, s_owed;

        public static void Show(string title, string description)
        {
            s_queue.Enqueue(new Entry { Id = ++s_next, Title = title, Description = description });
            if (s_owed > 0) s_owed--;
        }

        public override bool IsActive()
        {
            int waiting = GameQueue();
            if (waiting < s_lastGameQueue) s_owed += s_lastGameQueue - waiting;
            s_lastGameQueue = waiting;
            if (s_queue.Count > 0) return true;
            if (UnityEngine.Time.unscaledTime >= s_holdUntil) { s_owed = 0; return false; }
            return waiting > 0 || s_owed > 0 || Readers.PopupReader.AchievementPending();
        }

        private static int GameQueue()
        {
            try
            {
                var pending = AchievementPopupManager._popupQueue;
                return pending != null ? pending.Count : 0;
            }
            catch (System.Exception e)
            {
                Core.CoreLog.Warning("Achievement: reading the game's popup queue failed: " + e.Message);
                return 0;
            }
        }

        public override void Build(GraphBuilder b)
        {
            if (s_queue.Count == 0) return;
            var e = s_queue.Peek();
            string id = "achievement:" + e.Id + ":";
            b.PushContext(Strings.AchievementUnlocked, Strings.RoleDialog, positions: false);
            b.AddItem(ControlId.Structural(id + "title"), GameNodes.Text(() => e.Title));
            if (!string.IsNullOrWhiteSpace(e.Description))
                b.AddItem(ControlId.Structural(id + "description"), GameNodes.Text(() => e.Description));
            b.AddItem(ControlId.Structural(id + "ok"), GameNodes.Button(() => GameLoc.Get("OK_Misc") ?? "OK", Dismiss));
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Dismiss());
        }

        private static void Dismiss()
        {
            if (s_queue.Count == 0) return;
            s_queue.Dequeue();
            if (s_queue.Count == 0) s_holdUntil = UnityEngine.Time.unscaledTime + HoldFor;
        }
    }
}
