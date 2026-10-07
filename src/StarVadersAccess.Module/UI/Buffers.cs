using StarVadersAccess.Core.Buffers;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using Buffer = StarVadersAccess.Core.Buffers.Buffer;

namespace StarVadersAccess.Module.UI
{
    /// <summary>
    /// The mod's buffer roster and its review keys (Ctrl plus arrows), the Harkest Dungeon pattern:
    /// review lists for the information a focus announcement leaves out. For now the one buffer is
    /// the focused control's own lines (its head, then one line per description part and detail);
    /// the game's buffers (the board, the hand, the invaders) join here as their screens arrive.
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
            Manager.SetCurrent(BufferKeys.Ui);
        }
    }
}
