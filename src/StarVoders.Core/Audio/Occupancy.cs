using System;
using System.Collections.Generic;
using System.Globalization;
using StarVoders.Contracts;

namespace StarVoders.Core.Audio
{
    /// <summary>
    /// A whole-number setting stepped by a slider: <see cref="Min"/>..<see cref="Max"/> in steps of
    /// <see cref="Step"/>, persisted under its key as a plain integer (a stored value off the range is
    /// clamped on read).
    /// </summary>
    public sealed class SteppedSetting
    {
        private readonly ISettingsStore _store;
        public string Key { get; }
        public int Min { get; }
        public int Max { get; }
        public int Step { get; }
        public int Default { get; }

        public SteppedSetting(ISettingsStore store, string key, int min, int max, int step, int defaultValue)
        {
            _store = store;
            Key = key;
            Min = min;
            Max = max;
            Step = step;
            Default = defaultValue;
        }

        public int Value => Clamp(_store.GetInt(Key, Default));

        /// <summary>Step up (+1) or down (-1), clamped. True when it moved; a move persists.</summary>
        public bool Adjust(int direction)
        {
            int now = Value, next = Clamp(now + direction * Step);
            if (next == now) return false;
            _store.SetInt(Key, next);
            return true;
        }

        private int Clamp(int v) => v < Min ? Min : v > Max ? Max : v;
    }

    /// <summary>What a tile holds for the sweep: anything standing there, the running selection taking
    /// it, an invader about to strike it. None is an empty, unmarked tile (the puff).</summary>
    [Flags]
    public enum TileMark
    {
        None = 0,
        Occupied = 1,
        Target = 2,
        Danger = 4,
    }

    /// <summary>
    /// How grid occupancy plays (the sounds screen's Grid occupancy section), read through the store on
    /// every access so a change applies to the next sweep.
    /// </summary>
    public sealed class OccupancySettings
    {
        private readonly ISettingsStore _store;

        public OccupancySettings(ISettingsStore store)
        {
            _store = store;
            RowDelay = new SteppedSetting(store, "occupancy_row_delay_ms", 20, 300, 5, 80);
            PitchStep = new SteppedSetting(store, "occupancy_pitch_semitones", 0, 4, 1, 0);
            Width = new SteppedSetting(store, "occupancy_stereo_width", 0, 100, 10, 100);
            ColumnPitchStep = new SteppedSetting(store, "occupancy_column_semitones", 1, 6, 1, 2);
        }

        /// <summary>The full grid tells its columns apart by pitch (each column to the right higher, all
        /// centred) instead of by stereo position.</summary>
        public bool ColumnsByPitch
        {
            get => _store.GetBool("occupancy_columns_by_pitch", false);
            set => _store.SetBool("occupancy_columns_by_pitch", value);
        }

        /// <summary>Semitones each column to the right is raised by, when columns go by pitch.</summary>
        public SteppedSetting ColumnPitchStep { get; }

        /// <summary>Play a column's occupancy, centred, whenever the board cursor enters a new column. Off
        /// by default.</summary>
        public bool OnColumnCross
        {
            get => _store.GetBool("occupancy_on_cross", false);
            set => _store.SetBool("occupancy_on_cross", value);
        }

        /// <summary>Sweeps start at the cursor's row and go up (rows below it are left out), instead of
        /// always from the bottom row.</summary>
        public bool FromCursor
        {
            get => _store.GetBool("occupancy_from_cursor", false);
            set => _store.SetBool("occupancy_from_cursor", value);
        }

        /// <summary>Play the full grid when the player's turn starts (after the invaders' turn).</summary>
        public bool OnTurnStart
        {
            get => _store.GetBool("occupancy_turn_start", true);
            set => _store.SetBool("occupancy_turn_start", value);
        }

        /// <summary>A sweep ends at the column's highest occupied tile instead of running on to the top;
        /// a column with nothing in it plays nothing. On by default.</summary>
        public bool StopAtLastOccupied
        {
            get => _store.GetBool("occupancy_stop_at_last", true);
            set => _store.SetBool("occupancy_stop_at_last", value);
        }

        /// <summary>A move on the board plays the tile's sound as a sweep would (the puff, the blip, the
        /// bell, the siren) when the tile differs from the one before (what it holds, whether it is a
        /// target, who strikes it): moving across alike tiles is silent. Those sounds stand in for the
        /// words "target" and "targeted by" on a cell ("target" on a hand card in a choice among them,
        /// which plays the bell, or the puff on leaving the targets). Off, moves are silent and the words are
        /// said. On by default. Sweeps play every sound either way.</summary>
        public bool StepSounds
        {
            get => _store.GetBool("occupancy_step_sounds", true);
            set => _store.SetBool("occupancy_step_sounds", value);
        }

        /// <summary>A card pickup lying on a tile counts as occupied.</summary>
        public bool Pickups
        {
            get => _store.GetBool("occupancy_pickups", false);
            set => _store.SetBool("occupancy_pickups", value);
        }

        /// <summary>Milliseconds between one row's sound and the next (shorter than the sounds: they overlap).</summary>
        public SteppedSetting RowDelay { get; }

        /// <summary>Semitones each row above the bottom is raised by; 0 = no pitch.</summary>
        public SteppedSetting PitchStep { get; }

        /// <summary>How far apart the full grid's leftmost and rightmost columns are panned, in percent
        /// of hard left to hard right.</summary>
        public SteppedSetting Width { get; }
    }

    /// <summary>One sound of a sweep: what, when (seconds from the sweep's start), where and how high.</summary>
    public struct OccupancyNote
    {
        public AudioCue Cue;
        public double Delay;
        public float Pan;
        public float Pitch;

        public override string ToString() => Cue + "@" + Delay.ToString("0.###", CultureInfo.InvariantCulture)
            + " pan " + Pan.ToString("0.##", CultureInfo.InvariantCulture)
            + " pitch " + Pitch.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Grid occupancy as notes, engine-free. A column is swept bottom to top (or from a given row up),
    /// one slot per tile, each <c>rowDelayMs</c> after the one below: the puff for an empty tile, else a
    /// note for each of its marks at once (occupied, target, danger: an occupied tile in danger plays the
    /// blip and the siren together); the full grid sweeps the columns
    /// left to right as one stream: each column's first note one row step after the last note of the
    /// one before, panned from left to right across
    /// the stereo width, or, with <c>columnSemitones</c> above 0, all centred and each column that many
    /// semitones above the one to its left. Row pitch, when on, follows the tile's own row (its height
    /// on the board), so a sweep from the cursor keeps the same pitch for the same row; the two pitches add.
    /// </summary>
    public static class OccupancyPlan
    {
        /// <summary>A tile's sounds: the puff when it has no mark, else one per mark.</summary>
        public static IEnumerable<AudioCue> Cues(TileMark mark)
        {
            if (mark == TileMark.None) { yield return AudioCue.Empty; yield break; }
            if ((mark & TileMark.Occupied) != 0) yield return AudioCue.Occupied;
            if ((mark & TileMark.Target) != 0) yield return AudioCue.Target;
            if ((mark & TileMark.Danger) != 0) yield return AudioCue.Danger;
        }

        /// <summary>One tile's notes, at once and centred: its sounds (<see cref="Cues"/>) at the pitch a
        /// sweep gives its <paramref name="row"/>, raised by <paramref name="baseSemitones"/>.</summary>
        public static List<OccupancyNote> Tile(TileMark mark, int row, int pitchSemitones, int baseSemitones = 0,
            float pan = 0f, double delay = 0)
        {
            var notes = new List<OccupancyNote>();
            float pitch = (float)Math.Pow(2.0, (baseSemitones + row * pitchSemitones) / 12.0);
            foreach (var cue in Cues(mark))
                notes.Add(new OccupancyNote { Cue = cue, Delay = delay, Pan = pan, Pitch = pitch });
            return notes;
        }

        /// <summary>A step's state, to compare with the last step's: the tile's marks and who strikes it
        /// (<paramref name="danger"/>, a stable key such as the attackers' names joined; null or empty for
        /// none), so a different attacker is a different state though both are danger.
        /// <paramref name="kind"/> keeps states of different things apart (a cell, a hand card).</summary>
        public static string StepKey(string kind, TileMark mark, string danger) =>
            kind + "|" + (int)mark + "|" + (danger ?? "");

        /// <summary>
        /// A step's sounds: the tile's full sweep sound (<see cref="Tile"/>) when its state
        /// (<see cref="StepKey"/>) differs from the last step's (<paramref name="lastKey"/>; null =
        /// unknown, so it plays), else nothing. Moving across tiles that are alike is silent, and each
        /// change says what the new tile holds.
        /// </summary>
        public static List<OccupancyNote> Step(TileMark mark, string key, string lastKey, int row, int pitchSemitones,
            int baseSemitones = 0)
        {
            if (key == lastKey) return new List<OccupancyNote>();
            return Tile(mark, row, pitchSemitones, baseSemitones);
        }

        /// <summary>The notes of one column. <paramref name="tiles"/> is bottom row first;
        /// <paramref name="fromRow"/> the first row swept (0 = the bottom); <paramref name="baseSemitones"/>
        /// raises the whole column; <paramref name="stopAtLastOccupied"/> ends the sweep at the highest
        /// marked tile swept (occupied, target or danger; no notes when there is none).</summary>
        public static List<OccupancyNote> Column(IReadOnlyList<TileMark> tiles, int fromRow, int rowDelayMs, int pitchSemitones,
            float pan = 0f, double start = 0, int baseSemitones = 0, bool stopAtLastOccupied = false)
        {
            var notes = new List<OccupancyNote>();
            if (tiles == null) return notes;
            if (fromRow < 0) fromRow = 0;
            int end = tiles.Count; // one past the last row swept
            if (stopAtLastOccupied)
                while (end > fromRow && tiles[end - 1] == TileMark.None) end--;
            for (int row = fromRow; row < end; row++)
                notes.AddRange(Tile(tiles[row], row, pitchSemitones, baseSemitones, pan, start + (row - fromRow) * rowDelayMs / 1000.0));
            return notes;
        }

        /// <summary>The notes of the whole grid: <paramref name="columns"/> left to right, each bottom
        /// row first, each column starting one row step after the last note of the one before. <paramref name="columnSemitones"/> above 0 tells the columns apart
        /// by pitch (centred) instead of by pan. A column with no notes (stopping at the last occupied
        /// tile, and none) takes no time: the next follows on.</summary>
        public static List<OccupancyNote> Grid(IReadOnlyList<IReadOnlyList<TileMark>> columns, int fromRow, int rowDelayMs,
            int pitchSemitones, int widthPercent, int columnSemitones = 0, bool stopAtLastOccupied = false)
        {
            var notes = new List<OccupancyNote>();
            if (columns == null) return notes;
            int n = columns.Count;
            float width = Math.Max(0, Math.Min(100, widthPercent)) / 100f;
            double start = 0;
            for (int x = 0; x < n; x++)
            {
                bool byPitch = columnSemitones > 0;
                float pan = !byPitch && n > 1 ? width * (-1f + 2f * x / (n - 1)) : 0f;
                var column = Column(columns[x], fromRow, rowDelayMs, pitchSemitones, pan, start,
                    byPitch ? x * columnSemitones : 0, stopAtLastOccupied);
                notes.AddRange(column);
                if (column.Count > 0) start = column[column.Count - 1].Delay + rowDelayMs / 1000.0;
            }
            return notes;
        }
    }
}
