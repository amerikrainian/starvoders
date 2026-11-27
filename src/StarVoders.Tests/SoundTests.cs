using System;
using System.Collections.Generic;
using System.Linq;
using StarVoders.Contracts;
using StarVoders.Core.Audio;
using Xunit;

namespace StarVoders.Tests
{
    /// <summary>
    /// The mod's sounds without an engine: grid occupancy as timed notes (a column bottom to top, the
    /// grid's columns left to right by pan or by pitch), and the volumes and stepped settings over the
    /// settings store.
    /// </summary>
    public class SoundTests
    {
        private sealed class MemoryStore : ISettingsStore
        {
            public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
            public bool GetBool(string key, bool d) => Values.TryGetValue(key, out var v) ? (bool)v : d;
            public void SetBool(string key, bool value) => Values[key] = value;
            public int GetInt(string key, int d) => Values.TryGetValue(key, out var v) ? (int)v : d;
            public void SetInt(string key, int value) => Values[key] = value;
            public string GetString(string key, string d) => Values.TryGetValue(key, out var v) ? (string)v : d;
            public void SetString(string key, string value) => Values[key] = value;
        }

        // Bottom first: E empty, O an ally, I an invader, T target, D danger, S strike, Y an ally and
        // target, X an ally in danger, K an invader struck.
        private static List<TileMark> Col(string pattern) => pattern.Select(c =>
            c == 'O' ? TileMark.Ally : c == 'I' ? TileMark.Invader : c == 'T' ? TileMark.Target
            : c == 'D' ? TileMark.Danger : c == 'S' ? TileMark.Strike
            : c == 'Y' ? TileMark.Ally | TileMark.Target : c == 'X' ? TileMark.Ally | TileMark.Danger
            : c == 'K' ? TileMark.Invader | TileMark.Strike : TileMark.None).ToList();

        // Each note as its sound's letter, in AudioCue order: E O I T D S.
        private static string Cues(IEnumerable<OccupancyNote> notes) =>
            string.Concat(notes.Select(n => "EOITDS"[(int)n.Cue]));

        [Fact]
        public void A_column_plays_bottom_to_top_a_row_delay_apart_centred_at_one_pitch()
        {
            var notes = OccupancyPlan.Column(Col("OEEOEOE"), 0, 80, 0);
            Assert.Equal("OEEOEOE", Cues(notes));
            Assert.Equal(new[] { 0, 0.08, 0.16, 0.24, 0.32, 0.40, 0.48 }, notes.Select(n => Math.Round(n.Delay, 6)));
            Assert.All(notes, n => Assert.Equal(0f, n.Pan));
            Assert.All(notes, n => Assert.Equal(1f, n.Pitch));
        }

        [Fact]
        public void A_sweep_from_the_cursor_starts_at_its_row_and_keeps_each_rows_pitch()
        {
            var notes = OccupancyPlan.Column(Col("OEEOEOE"), 3, 80, 2);
            Assert.Equal("OEOE", Cues(notes));
            Assert.Equal(0, notes[0].Delay);
            // Row 3 (the fourth from the bottom) is six semitones up, wherever the sweep starts.
            Assert.Equal(Math.Pow(2, 6 / 12.0), notes[0].Pitch, 4);
        }

        [Fact]
        public void The_grid_sweeps_columns_left_to_right_panned_across_the_width()
        {
            var cols = new List<IReadOnlyList<TileMark>> { Col("OE"), Col("EE"), Col("EO") };
            var notes = OccupancyPlan.Grid(cols, 0, 80, 0, 100);
            Assert.Equal("OEEEEO", Cues(notes));
            // One stream: each column's first note a row step after the last note before it.
            Assert.Equal(new[] { 0, 0.08, 0.16, 0.24, 0.32, 0.40 }, notes.Select(n => Math.Round(n.Delay, 6)));
            Assert.Equal(new[] { -1f, -1f, 0f, 0f, 1f, 1f }, notes.Select(n => n.Pan));

            var half = OccupancyPlan.Grid(cols, 0, 80, 0, 50);
            Assert.Equal(-0.5f, half[0].Pan);
            Assert.Equal(0.5f, half[5].Pan);
        }

        [Fact]
        public void Columns_by_pitch_are_centred_each_higher_than_the_one_to_its_left()
        {
            var cols = new List<IReadOnlyList<TileMark>> { Col("OE"), Col("EE"), Col("EO") };
            var notes = OccupancyPlan.Grid(cols, 0, 80, 1, 100, columnSemitones: 2);
            Assert.All(notes, n => Assert.Equal(0f, n.Pan));
            // Column 3's top row: 2 columns of 2 semitones plus 1 row of 1.
            Assert.Equal(Math.Pow(2, 5 / 12.0), notes[5].Pitch, 4);
            Assert.Equal(1f, notes[0].Pitch);
        }

        [Fact]
        public void Columns_run_on_at_the_row_step_however_many_rows_they_play()
        {
            var cols = new List<IReadOnlyList<TileMark>> { Col("OEEOEOE"), Col("EEEEOOO"), Col("OEOOOEO") };
            foreach (int from in new[] { 0, 4 })
            {
                var notes = OccupancyPlan.Grid(cols, from, 80, 0, 100);
                int perColumn = 7 - from;
                for (int x = 1; x < 3; x++)
                {
                    double lastBefore = notes[x * perColumn - 1].Delay, first = notes[x * perColumn].Delay;
                    Assert.Equal(0.08, Math.Round(first - lastBefore, 6));
                }
            }
        }

        [Fact]
        public void Stopping_at_the_last_occupied_tile_drops_the_empty_tiles_above_it()
        {
            Assert.Equal("OEEOEO", Cues(OccupancyPlan.Column(Col("OEEOEOE"), 0, 80, 0, stopAtLastOccupied: true)));
            // From the cursor: only the rows swept count.
            Assert.Equal("EO", Cues(OccupancyPlan.Column(Col("OEEOEOE"), 4, 80, 0, stopAtLastOccupied: true)));
            Assert.Empty(OccupancyPlan.Column(Col("EEEE"), 0, 80, 0, stopAtLastOccupied: true));
            Assert.Empty(OccupancyPlan.Column(Col("OEEE"), 1, 80, 0, stopAtLastOccupied: true));
        }

        [Fact]
        public void An_empty_column_stopped_at_its_last_occupied_tile_takes_no_time_in_the_grid()
        {
            var cols = new List<IReadOnlyList<TileMark>> { Col("OE"), Col("EE"), Col("EO") };
            var notes = OccupancyPlan.Grid(cols, 0, 80, 0, 100, stopAtLastOccupied: true);
            Assert.Equal("OEO", Cues(notes));
            Assert.Equal(new[] { 0, 0.08, 0.16 }, notes.Select(n => Math.Round(n.Delay, 6)));
            Assert.Equal(new[] { -1f, 1f, 1f }, notes.Select(n => n.Pan)); // the third column keeps its place
        }

        [Fact]
        public void A_marked_tile_plays_its_marks_together_in_place_of_the_puff()
        {
            var notes = OccupancyPlan.Column(Col("XDDTY"), 0, 80, 0);
            Assert.Equal("ODDDTOT", Cues(notes));
            // An occupied tile in danger: the blip and the siren in one slot.
            Assert.Equal(new[] { 0, 0, 0.08, 0.16, 0.24, 0.32, 0.32 }, notes.Select(n => Math.Round(n.Delay, 6)));
        }

        [Fact]
        public void Stopping_at_the_last_occupied_tile_counts_target_and_danger_tiles()
        {
            Assert.Equal("OEDT", Cues(OccupancyPlan.Column(Col("OEDTEE"), 0, 80, 0, stopAtLastOccupied: true)));
        }

        [Fact]
        public void A_step_plays_its_tile_as_the_sweep_slot_would_at_its_rows_pitch()
        {
            var notes = OccupancyPlan.Tile(TileMark.Ally | TileMark.Danger, 3, 2, baseSemitones: 4);
            Assert.Equal("OD", Cues(notes));
            Assert.All(notes, n => Assert.Equal(0, n.Delay));
            Assert.All(notes, n => Assert.Equal(0f, n.Pan));
            Assert.All(notes, n => Assert.Equal(Math.Pow(2, 10 / 12.0), n.Pitch, 4));
            Assert.Equal("E", Cues(OccupancyPlan.Tile(TileMark.None, 0, 0)));
        }

        [Fact]
        public void A_step_plays_only_when_the_tile_differs_from_the_last()
        {
            // Up a Shooter's column: the first danger tile sirens, the alike ones above are silent.
            string Key(TileMark m, string d) => OccupancyPlan.StepKey("tile", m, d);
            string last = Key(TileMark.None, "");
            var steps = new List<string>();
            foreach (var (mark, danger) in new[] { (TileMark.Danger, "Shooter 2"), (TileMark.Danger, "Shooter 2"),
                (TileMark.Ally | TileMark.Danger, "Shooter 2"), (TileMark.Danger, "Shooter 2"),
                (TileMark.Danger, "Shooter 2|Shooter 3"), (TileMark.None, ""), (TileMark.None, "") })
            {
                string key = Key(mark, danger);
                steps.Add(Cues(OccupancyPlan.Step(mark, key, last, 0, 0)));
                last = key;
            }
            // Entering, alike, an invader in the line, empty again, another attacker too, safe, alike.
            Assert.Equal(new[] { "D", "", "OD", "D", "D", "E", "" }, steps);
        }

        [Fact]
        public void Every_step_plays_its_tile_unless_only_changes_play()
        {
            string key = OccupancyPlan.StepKey("tile", TileMark.Danger, "Shooter 2");
            Assert.Equal("D", Cues(OccupancyPlan.Step(TileMark.Danger, key, key, 0, 0, onChangeOnly: false)));
            Assert.Equal("", Cues(OccupancyPlan.Step(TileMark.Danger, key, key, 0, 0, onChangeOnly: true)));
        }

        [Fact]
        public void A_first_step_always_plays_and_kinds_never_match()
        {
            string key = OccupancyPlan.StepKey("tile", TileMark.None, null);
            Assert.Equal("E", Cues(OccupancyPlan.Step(TileMark.None, key, null, 0, 0)));
            Assert.NotEqual(OccupancyPlan.StepKey("card", TileMark.None, null), key);
        }

        [Fact]
        public void Invaders_and_strikes_play_their_own_sounds()
        {
            Assert.Equal("OEISIS", Cues(OccupancyPlan.Column(Col("OEISK"), 0, 80, 0)));
            Assert.Equal("OEIS", Cues(OccupancyPlan.Column(Col("OEISEE"), 0, 80, 0, stopAtLastOccupied: true)));
        }

        [Fact]
        public void A_one_column_grid_is_centred()
        {
            var notes = OccupancyPlan.Grid(new List<IReadOnlyList<TileMark>> { Col("OE") }, 0, 80, 0, 100);
            Assert.All(notes, n => Assert.Equal(0f, n.Pan));
        }

        [Fact]
        public void A_sound_volume_follows_the_master_and_goes_past_100()
        {
            var store = new MemoryStore();
            var volumes = new SoundVolumes(store);
            var empty = volumes.All[(int)AudioCue.Empty];
            for (int i = 0; i < 15; i++) empty.Adjust(1);
            Assert.Equal(SoundVolume.MaxVolume, empty.Value);
            Assert.Equal(2f, volumes.Gain(AudioCue.Empty));
            for (int i = 0; i < 5; i++) volumes.Master.Adjust(-1);
            Assert.Equal(150, empty.Value);
            Assert.Equal(50, volumes.All[(int)AudioCue.Ally].Value);

            var reloaded = new SoundVolumes(store);
            Assert.Equal(150, reloaded.All[(int)AudioCue.Empty].Value);
        }

        [Fact]
        public void A_stepped_setting_clamps_and_persists()
        {
            var store = new MemoryStore();
            var settings = new OccupancySettings(store);
            Assert.Equal(80, settings.RowDelay.Value);
            Assert.True(settings.RowDelay.Adjust(1));
            Assert.Equal(85, settings.RowDelay.Value);
            Assert.Equal(85, new OccupancySettings(store).RowDelay.Value);
            for (int i = 0; i < 100; i++) settings.RowDelay.Adjust(-1);
            Assert.Equal(20, settings.RowDelay.Value);
            Assert.False(settings.RowDelay.Adjust(-1));
            store.SetInt("occupancy_row_delay_ms", 9999);
            Assert.Equal(300, settings.RowDelay.Value);
        }

        [Fact]
        public void Cue_files_are_snake_case() => Assert.Equal("ally", AudioCues.FileName(AudioCue.Ally));
    }
}
