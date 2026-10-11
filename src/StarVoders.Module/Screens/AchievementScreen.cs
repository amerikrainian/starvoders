using System.Collections.Generic;
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
    /// changes, it only holds the mod's focus.
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

        public static void Show(string title, string description)
            => s_queue.Enqueue(new Entry { Id = ++s_next, Title = title, Description = description });

        public override bool IsActive() => s_queue.Count > 0;

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
            if (s_queue.Count > 0) s_queue.Dequeue();
        }
    }
}
