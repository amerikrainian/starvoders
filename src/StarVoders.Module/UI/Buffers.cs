using StarVoders.Core.Buffers;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using Buffer = StarVoders.Core.Buffers.Buffer;

namespace StarVoders.Module.UI
{
    /// <summary>
    /// The mod's buffer roster and its review keys (Ctrl plus arrows), the Harkest Dungeon pattern:
    /// review lists for the information a focus announcement leaves out: the focused control's own
    /// lines (its head, then one line per description part and detail); in a fight the hand (each
    /// card as it is announced), the invaders and the combat log (every event, uncapped, oldest
    /// first, opening on the newest); the player's figures in a fight or a run (<see cref="Fight.FightBuffers"/>).
    /// Every buffer reads live on each keypress; a focus change rewinds every buffer (the combat log
    /// to its newest event), as the control's does, and leaves review on whichever buffer is being read.
    /// </summary>
    internal static class Buffers
    {
        public static BufferManager Manager { get; private set; }
        public static BufferControls Controls { get; private set; }

        private static ControlId _homed;

        public static void Init()
        {
            Manager = new BufferManager();
            Controls = new BufferControls(Manager, (text, interrupt) => Core.Speech.Say(text, interrupt: interrupt));
            Manager.Add(new Buffer(BufferKeys.Ui, () => Strings.BufferUi)).SetSource(() => NodeLines.Lines(Navigation.FocusedNode));
            Manager.Add(new Buffer(BufferKeys.Hand, () => Strings.BufferHand)).SetSource(Fight.FightBuffers.Hand);
            Manager.Add(new Buffer(BufferKeys.Player, () => Strings.BufferPlayer)).SetSource(Fight.FightBuffers.Player);
            Manager.Add(new Buffer(BufferKeys.Enemies, () => Strings.BufferEnemies)).SetSource(Fight.FightBuffers.Enemies);
            // The fight's events, oldest first, opening on the newest; empty outside a fight.
            var combat = Manager.Add(new Buffer(BufferKeys.Combat, () => Strings.BufferCombat) { FollowLatest = true });
            combat.SetSource(() => Fight.Fight.Controller != null ? Fight.FightEvents.Log : null);
        }

        /// <summary>Per frame: a focus change (by key or by the game moving things) rewinds every
        /// buffer, the combat log to its newest event.</summary>
        public static void Tick()
        {
            if (Manager == null) return;
            var id = Navigation.FocusedNode?.Id;
            if (Equals(id, _homed)) return;
            _homed = id;
            foreach (var buffer in Manager.Buffers)
            {
                if (buffer.FollowLatest) buffer.MoveToEnd();
                else buffer.Reset();
            }
            // Review stays on the buffer being read: only the control buffer's content follows focus.
        }
    }
}
