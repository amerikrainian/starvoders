using System;
using System.Collections.Generic;
using System.Linq;
using StarVoders.Core;
using StarVoders.Core.Audio;
using StarVoders.Module.Fight;
using StarVoders.Module.Screens;
using FightState = StarVoders.Module.Fight.Fight;

namespace StarVoders.Module.Audio
{
    /// <summary>
    /// Grid occupancy: each tile of a column as a sound, empty (a soft puff), an invader (a buzzy blip)
    /// or one of the player's own (a sine blip: the mech, a bomb, a Summon, any entity not an invader; a
    /// card pickup when the setting says so), with the target bell where the running selection takes the
    /// tile, the strike crack where the game marks it about to be struck, and the danger siren where an
    /// invader will strike it (alone on an empty tile, with the blip on an occupied one), swept
    /// bottom to top with the sounds overlapping (<see cref="OccupancyPlan"/>). A column plays centred:
    /// on its key, and as the board cursor enters a new column; the full grid sweeps the columns left
    /// to right as one stream (each column's first note a row step after the last note before it), panned left to right: on its key, and when the player's turn starts. With columns by
    /// pitch, everything is centred and each column to the right is higher, a single column included. A new sweep stops
    /// the one before. The board is read when a sweep starts; nothing is kept.
    /// </summary>
    internal static class OccupancySounds
    {
        private static int? _lastColumn;   // the column the board cursor was last in; null off the board
        private static IntPtr _turnFight;   // the fight the turn below belongs to
        private static int _turn = -1;      // the player's turn last seen settled; -1 = none yet this fight

        private static OccupancySettings Settings => ModuleMain.Occupancy;

        // ---- keys ----

        /// <summary>The cursor's column (or, off the board, the one it was last in, else the mech's).</summary>
        public static void PlayCurrentColumn()
        {
            var board = FightBoard.Read();
            var at = EncounterScreen.FocusedTile() ?? EncounterScreen.LastTile ?? board?.PlayerTile;
            if (board == null || at == null) return;
            PlayColumn(board, at.Value.X, at.Value.Y);
        }

        /// <summary>Every column, left to right.</summary>
        public static void PlayGrid()
        {
            var board = FightBoard.Read();
            if (board == null) return;
            var settings = Settings;
            if (settings == null) return;
            var at = EncounterScreen.FocusedTile();
            int from = settings.FromCursor && at.HasValue ? at.Value.Y : 0;
            Play(OccupancyPlan.Grid(Columns(board, settings), from, settings.RowDelay.Value,
                settings.PitchStep.Value, settings.Width.Value, ColumnSemitones(settings), settings.StopAtLastOccupied));
        }

        // ---- the cursor crossing columns ----

        /// <summary>A board cell took focus: entering a new column plays it (when that is on), else the
        /// step plays the tile's sound when the tile differs from the last step's (when that is on): moving
        /// across alike tiles is silent. The column's sweep holds the tile already, so the two never play
        /// together.</summary>
        public static void Focused(int x, int y)
        {
            bool newColumn = _lastColumn != x;
            _lastColumn = x;
            var settings = Settings;
            var board = settings != null ? FightBoard.Read() : null;
            if (board == null) return;
            var mark = Mark(board, settings, x, y);
            string key = OccupancyPlan.StepKey("tile", mark, DangerKey(board, x, y));
            string last = _lastStep;
            _lastStep = key;
            if (newColumn && settings.OnColumnCross) { PlayColumn(board, x, y); return; }
            if (!settings.StepSounds) return;
            var notes = OccupancyPlan.Step(mark, key, last, y, settings.PitchStep.Value, x * ColumnSemitones(settings));
            if (notes.Count > 0) Play(notes);
        }

        /// <summary>A hand card took focus while a choice among the hand's cards runs: the bell when it
        /// is a target and the last step was not, the puff the other way, nothing between alike ones.</summary>
        public static void CardFocused(bool target)
        {
            var settings = Settings;
            if (settings == null) return;
            var mark = target ? TileMark.Target : TileMark.None;
            string key = OccupancyPlan.StepKey("card", mark, null);
            string last = _lastStep;
            _lastStep = key;
            if (!settings.StepSounds) return;
            var notes = OccupancyPlan.Step(mark, key, last, 0, 0);
            if (notes.Count > 0) Play(notes);
        }

        /// <summary>Focus is off the cells: coming back to any column plays it, and off the hand too,
        /// the next step plays whatever it lands on.</summary>
        public static void LeftBoard()
        {
            _lastColumn = null;
            if (!EncounterScreen.FocusedOnHand()) _lastStep = null;
        }

        // The last step's state (OccupancyPlan.StepKey): a step plays only when its own differs.
        private static string _lastStep;

        // Who strikes the tile, as a stable key: the attackers' names in order, empty when safe.
        private static string DangerKey(FightBoard board, int x, int y)
        {
            var who = board.Attackers(x, y);
            who.Sort(StringComparer.Ordinal);
            return string.Join("|", who);
        }

        // ---- the player's turn starting ----

        /// <summary>From the module tick, after the fight events: the sweep under way fed to the engine,
        /// and the full grid once per new player turn
        /// (not the fight's first, which the fight's arrival already covers), once the fight settles.</summary>
        public static void Tick()
        {
            try { Pump(); }
            catch (Exception e) { CoreLog.Warning("occupancy: pumping failed: " + e.Message); _pending.Clear(); }
            var c = FightState.Controller;
            if (c == null || c.EncounterEnded) { _turnFight = IntPtr.Zero; return; }
            if (c.Pointer != _turnFight) { _turnFight = c.Pointer; _turn = -1; }
            if (!FightEvents.Settled) return;
            var model = FightState.Model;
            if (model == null || !model.IsPlayerTurn) return;
            int turn = model.TurnNumber;
            if (turn == _turn) return;
            bool first = _turn < 0;
            _turn = turn;
            var settings = Settings;
            if (!first && settings != null && settings.OnTurnStart) PlayGrid();
        }

        // ---- the sounds screen's preview ----

        // A made-up board for the preview outside a fight: five columns of seven rows, bottom first
        // (A the mech, I an invader, D danger, X the mech in danger, E empty): a Shooter on the middle
        // column firing down at the mech.
        private static readonly string[] DemoColumns = { "AEEIEIE", "EEEEIII", "XDDDDIE", "EEEIEEE", "EEEEEEI" };

        /// <summary>The full grid as the settings now make it: the live board in a fight, else a made-up one.</summary>
        public static void Preview()
        {
            if (FightBoard.Read() != null) { PlayGrid(); return; }
            var settings = Settings;
            if (settings == null) return;
            var columns = new List<IReadOnlyList<TileMark>>();
            foreach (var pattern in DemoColumns)
            {
                var col = new List<TileMark>();
                foreach (char ch in pattern)
                    col.Add(ch == 'A' ? TileMark.Ally : ch == 'I' ? TileMark.Invader : ch == 'D' ? TileMark.Danger
                        : ch == 'X' ? TileMark.Ally | TileMark.Danger : TileMark.None);
                columns.Add(col);
            }
            Play(OccupancyPlan.Grid(columns, 0, settings.RowDelay.Value,
                settings.PitchStep.Value, settings.Width.Value, ColumnSemitones(settings), settings.StopAtLastOccupied));
        }

        // ---- shared ----

        private static void PlayColumn(FightBoard board, int x, int cursorRow)
        {
            var settings = Settings;
            if (settings == null || x < 0 || x >= board.Columns) return;
            int from = settings.FromCursor ? cursorRow : 0;
            Play(OccupancyPlan.Column(Column(board, settings, x), from, settings.RowDelay.Value, settings.PitchStep.Value,
                baseSemitones: x * ColumnSemitones(settings), stopAtLastOccupied: settings.StopAtLastOccupied));
        }

        // Semitones between neighbouring columns when columns go by pitch (each to the right higher,
        // everything centred), else 0 (columns go by pan in the full grid).
        private static int ColumnSemitones(OccupancySettings settings) => settings.ColumnsByPitch ? settings.ColumnPitchStep.Value : 0;

        private static List<IReadOnlyList<TileMark>> Columns(FightBoard board, OccupancySettings settings)
        {
            var columns = new List<IReadOnlyList<TileMark>>();
            for (int x = 0; x < board.Columns; x++) columns.Add(Column(board, settings, x));
            return columns;
        }

        // The column's tiles, bottom row first, as their marks.
        private static List<TileMark> Column(FightBoard board, OccupancySettings settings, int x)
        {
            var col = new List<TileMark>(board.Rows);
            for (int y = 0; y < board.Rows; y++) col.Add(Mark(board, settings, x, y));
            return col;
        }

        // Invader or ally by what stands there (anything not an invader is the player's: the mech, a bomb,
        // a Summon; a pickup when pickups count); target when the running selection takes it; strike when
        // the game marks it about to be struck; danger when an invader will strike it.
        private static TileMark Mark(FightBoard board, OccupancySettings settings, int x, int y)
        {
            var mark = TileMark.None;
            var entity = board.At(x, y);
            if (entity != null) mark |= entity.TryCast<StarVaders.EnemyEntityModel>() != null ? TileMark.Invader : TileMark.Ally;
            else if (settings.Pickups && board.HasPickup(x, y)) mark |= TileMark.Ally;
            if (EncounterScreen.IsTarget(x, y)) mark |= TileMark.Target;
            if (FightState.Strike(x, y)) mark |= TileMark.Strike;
            if (board.Attackers(x, y).Count > 0) mark |= TileMark.Danger;
            return mark;
        }

        // ---- a step's sound ----

        /// <summary>Whether moves play the tiles' sounds (which then stand in for the words "target" and
        /// "targeted by").</summary>
        public static bool StepSounds => Settings?.StepSounds ?? false;



        // The sweep under way: its notes not yet handed to the engine, earliest first, and when it began.
        // The game's FMOD has 32 real voices, so a 45-note grid scheduled at once overruns them and
        // FMOD drops or virtualises notes; each is handed over only LookAhead before it is due (on the
        // batch's DSP clock, so its timing stays exact), keeping a handful of voices alive at a time.
        private static readonly List<OccupancyNote> _pending = new List<OccupancyNote>();
        private static double _batchStart;
        private const double LookAhead = 0.25;

        private static double Now => UnityEngine.Time.unscaledTimeAsDouble;

        private static void Play(List<OccupancyNote> notes)
        {
            var engine = ModuleMain.CueEngine;
            _pending.Clear();
            if (engine == null || notes.Count == 0) return;
            try
            {
                engine.StopAll();
                engine.BeginBatch();
                _batchStart = Now;
                _pending.AddRange(notes.OrderBy(n => n.Delay)); // stable: a column's rows keep their order
                Pump();
            }
            catch (Exception e) { CoreLog.Warning("occupancy: playing failed: " + e.Message); _pending.Clear(); }
        }

        // Hand the engine every pending note due within LookAhead.
        private static void Pump()
        {
            if (_pending.Count == 0) return;
            var engine = ModuleMain.CueEngine;
            if (engine == null) { _pending.Clear(); return; }
            double horizon = Now - _batchStart + LookAhead;
            int n = 0;
            while (n < _pending.Count && _pending[n].Delay <= horizon)
            {
                var note = _pending[n++];
                engine.PlayCue(note.Cue, 1f, note.Pan, note.Pitch, note.Delay);
            }
            if (n > 0) _pending.RemoveRange(0, n);
        }

        /// <summary>One sound alone, centred (the sounds screen's volume rows): through the queue, so it
        /// replaces a sweep rather than playing into its batch.</summary>
        public static void PlayOne(AudioCue cue) =>
            Play(new List<OccupancyNote> { new OccupancyNote { Cue = cue, Delay = 0, Pan = 0f, Pitch = 1f } });
    }
}
