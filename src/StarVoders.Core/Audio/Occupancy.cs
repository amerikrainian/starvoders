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
    /// one note per tile, each <c>rowDelayMs</c> after the one below; the full grid sweeps the columns
    /// left to right as one stream: each column's first note one row step after the last note of the
    /// one before, panned from left to right across
    /// the stereo width, or, with <c>columnSemitones</c> above 0, all centred and each column that many
    /// semitones above the one to its left. Row pitch, when on, follows the tile's own row (its height
    /// on the board), so a sweep from the cursor keeps the same pitch for the same row; the two pitches add.
    /// </summary>
    public static class OccupancyPlan
    {
        /// <summary>The notes of one column. <paramref name="occupied"/> is bottom row first;
        /// <paramref name="fromRow"/> the first row swept (0 = the bottom); <paramref name="baseSemitones"/>
        /// raises the whole column.</summary>
        public static List<OccupancyNote> Column(IReadOnlyList<bool> occupied, int fromRow, int rowDelayMs, int pitchSemitones,
            float pan = 0f, double start = 0, int baseSemitones = 0)
        {
            var notes = new List<OccupancyNote>();
            if (occupied == null) return notes;
            if (fromRow < 0) fromRow = 0;
            for (int row = fromRow; row < occupied.Count; row++)
            {
                notes.Add(new OccupancyNote
                {
                    Cue = occupied[row] ? AudioCue.Occupied : AudioCue.Empty,
                    Delay = start + (row - fromRow) * rowDelayMs / 1000.0,
                    Pan = pan,
                    Pitch = (float)Math.Pow(2.0, (baseSemitones + row * pitchSemitones) / 12.0),
                });
            }
            return notes;
        }

        /// <summary>The notes of the whole grid: <paramref name="columns"/> left to right, each bottom
        /// row first, each column starting one row step after the last note of the one before. <paramref name="columnSemitones"/> above 0 tells the columns apart
        /// by pitch (centred) instead of by pan.</summary>
        public static List<OccupancyNote> Grid(IReadOnlyList<IReadOnlyList<bool>> columns, int fromRow, int rowDelayMs,
            int pitchSemitones, int widthPercent, int columnSemitones = 0)
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
                    byPitch ? x * columnSemitones : 0);
                notes.AddRange(column);
                if (column.Count > 0) start = column[column.Count - 1].Delay + rowDelayMs / 1000.0;
            }
            return notes;
        }
    }
}
