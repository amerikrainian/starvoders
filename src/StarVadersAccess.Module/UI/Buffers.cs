using StarVadersAccess.Core.Buffers;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using Buffer = StarVadersAccess.Core.Buffers.Buffer;

namespace StarVadersAccess.Module.UI
{
    /// <summary>
    /// The mod's buffer roster and its review keys (Ctrl plus arrows), the Harkest Dungeon pattern:
    /// review lists for the information a focus announcement leaves out: the focused control's own
    /// lines (its head, then one line per description part and detail), and in a fight the combat
    /// log (every event, uncapped, oldest first, opening on the newest).
    /// Every buffer reads live on each keypress; a focus change re-homes review to the control's
    /// buffer and rewinds it, since a new control is new content.
    /// </summary>
    internal static class Buffers
    {
        public static BufferManager Manager { get; private set; }
        public static BufferControls Controls { get; private set; }

        private static Buffer _ui;
        private static ControlId _homed;

        public static void Init()
        {
            Manager = new BufferManager();
            Controls = new BufferControls(Manager, (text, interrupt) => Core.Speech.Say(text, interrupt: interrupt));
            _ui = Manager.Add(new Buffer(BufferKeys.Ui, () => Strings.BufferUi));
            _ui.SetSource(() => NodeLines.Lines(Navigation.FocusedNode));
            // The fight's events, oldest first, opening on the newest; empty outside a fight.
            var combat = Manager.Add(new Buffer(BufferKeys.Combat, () => Strings.BufferCombat) { FollowLatest = true });
            combat.SetSource(() => Fight.Fight.Controller != null ? Fight.FightEvents.Log : null);
        }

        /// <summary>Per frame: a focus change (by key or by the game moving things) re-homes review to
        /// the control's own buffer and rewinds it.</summary>
        public static void Tick()
        {
            if (Manager == null) return;
            var id = Navigation.FocusedNode?.Id;
            if (Equals(id, _homed)) return;
            _homed = id;
            _ui.Reset();
            // Reading the combat log stays put when the game moves focus (a cell's occupant dying,
            // a card leaving the hand): only the control buffer follows focus.
            if (Manager.Current?.Key == BufferKeys.Combat) return;
            Manager.SetCurrent(BufferKeys.Ui);
        }
    }
}
