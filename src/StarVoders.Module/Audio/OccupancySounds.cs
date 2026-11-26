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
    /// Grid occupancy: each tile of a column as a sound, empty (a soft puff) or occupied (a blip: any
    /// entity, the mech, an invader, a bomb, a Summon; a card pickup when the setting says so), swept
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
                settings.PitchStep.Value, settings.Width.Value, ColumnSemitones(settings)));
        }

        // ---- the cursor crossing columns ----

        /// <summary>A board cell took focus: entering a new column plays it.</summary>
        public static void Focused(int x, int y)
        {
            if (_lastColumn == x) return;
            _lastColumn = x;
            var settings = Settings;
            if (settings == null || !settings.OnColumnCross) return;
            var board = FightBoard.Read();
            if (board != null) PlayColumn(board, x, y);
        }

        /// <summary>Focus is off the cells: coming back to any column plays it.</summary>
        public static void LeftBoard() => _lastColumn = null;

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

        // A made-up board for the preview outside a fight: five columns of seven rows, bottom first.
        private static readonly string[] DemoColumns = { "OEEOEOE", "EEEEOOO", "OEOOOEO", "EEEOEEE", "OEEEEEO" };

        /// <summary>The full grid as the settings now make it: the live board in a fight, else a made-up one.</summary>
        public static void Preview()
        {
            if (FightBoard.Read() != null) { PlayGrid(); return; }
            var settings = Settings;
            if (settings == null) return;
            var columns = new List<IReadOnlyList<bool>>();
            foreach (var pattern in DemoColumns)
            {
                var col = new List<bool>();
                foreach (char ch in pattern) col.Add(ch == 'O');
                columns.Add(col);
            }
            Play(OccupancyPlan.Grid(columns, 0, settings.RowDelay.Value,
                settings.PitchStep.Value, settings.Width.Value, ColumnSemitones(settings)));
        }

        // ---- shared ----

        private static void PlayColumn(FightBoard board, int x, int cursorRow)
        {
            var settings = Settings;
            if (settings == null || x < 0 || x >= board.Columns) return;
            int from = settings.FromCursor ? cursorRow : 0;
            Play(OccupancyPlan.Column(Column(board, settings, x), from, settings.RowDelay.Value, settings.PitchStep.Value,
                baseSemitones: x * ColumnSemitones(settings)));
        }

        // Semitones between neighbouring columns when columns go by pitch (each to the right higher,
        // everything centred), else 0 (columns go by pan in the full grid).
        private static int ColumnSemitones(OccupancySettings settings) => settings.ColumnsByPitch ? settings.ColumnPitchStep.Value : 0;

        private static List<IReadOnlyList<bool>> Columns(FightBoard board, OccupancySettings settings)
        {
            var columns = new List<IReadOnlyList<bool>>();
            for (int x = 0; x < board.Columns; x++) columns.Add(Column(board, settings, x));
            return columns;
        }

        // The column's tiles, bottom row first: occupied when something stands there (or a pickup lies
        // there, when pickups count).
        private static List<bool> Column(FightBoard board, OccupancySettings settings, int x)
        {
            bool pickups = settings.Pickups;
            var col = new List<bool>(board.Rows);
            for (int y = 0; y < board.Rows; y++)
                col.Add(board.At(x, y) != null || (pickups && board.HasPickup(x, y)));
            return col;
        }

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
