using System;
using System.Collections.Generic;
using System.Globalization;
using StarVoders.Contracts;

namespace StarVoders.Core.Audio
{
    /// <summary>
    /// The baseline volume of every mod sound, in percent: a cue's effective volume is this plus that
    /// cue's saved offset, so one adjustment moves every sound together while their relative levels
    /// hold. Persisted under "sound_master". Guildrun's.
    /// </summary>
    public sealed class MasterVolume
    {
        public const string Key = "sound_master";
        public const int DefaultVolume = 100;

        private readonly ISettingsStore _store;

        /// <summary>Current volume in percent, 0..<see cref="SoundVolume.MaxVolume"/>.</summary>
        public int Value { get; private set; }

        public MasterVolume(ISettingsStore store)
        {
            _store = store;
            string stored = store.GetString(Key, DefaultVolume.ToString(CultureInfo.InvariantCulture));
            Value = int.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? SoundVolume.ClampVolume(parsed) : DefaultVolume;
        }

        /// <summary>Step the volume up (+1) or down (-1), clamped. True when it moved; a move persists.</summary>
        public bool Adjust(int direction)
        {
            int next = SoundVolume.ClampVolume(Value + direction * SoundVolume.Step);
            if (next == Value) return false;
            Value = next;
            _store.SetString(Key, next.ToString(CultureInfo.InvariantCulture));
            return true;
        }
    }

    /// <summary>
    /// One mod sound's saved volume, stored as a signed offset from the master: the effective (and
    /// spoken) <see cref="Value"/> is master plus the offset, clamped to 0..<see cref="MaxVolume"/>
    /// percent of the cue's natural level. Stepping re-derives the offset against the current master,
    /// so a later master move carries every sound with it. Persisted under "sound_volume_&lt;cue&gt;".
    /// </summary>
    public sealed class SoundVolume
    {
        public const int Step = 10;
        public const int MaxVolume = 200;

        private readonly ISettingsStore _store;
        private readonly MasterVolume _master;
        private int _offset;

        public AudioCue Cue { get; }
        public string Key => "sound_volume_" + AudioCues.FileName(Cue);

        /// <summary>Effective volume in percent, 0..<see cref="MaxVolume"/>.</summary>
        public int Value => ClampVolume(_master.Value + _offset);

        /// <summary>The gain applied to the cue's natural level.</summary>
        public float Gain => Value / 100f;

        public SoundVolume(AudioCue cue, ISettingsStore store, MasterVolume master)
        {
            Cue = cue;
            _store = store;
            _master = master;
            _offset = ParseOffset(store.GetString(Key, FormatOffset(0)));
        }

        /// <summary>Step the effective volume up (+1) or down (-1), clamped. True when it moved; a move persists.</summary>
        public bool Adjust(int direction)
        {
            int next = ClampVolume(Value + direction * Step);
            if (next == Value) return false;
            _offset = next - _master.Value;
            _store.SetString(Key, FormatOffset(_offset));
            return true;
        }

        internal static int ClampVolume(int value) => value < 0 ? 0 : value > MaxVolume ? MaxVolume : value;

        // Offsets persist with an explicit sign ("+10", "-40", "+0").
        private static string FormatOffset(int offset) => offset.ToString("+0;-0", CultureInfo.InvariantCulture);

        private static int ParseOffset(string stored)
        {
            stored = (stored ?? "").Trim();
            if (!int.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return 0;
            if (stored[0] == '+' || stored[0] == '-') return parsed;
            return ClampVolume(parsed) - MasterVolume.DefaultVolume; // a bare number: an absolute percent
        }
    }

    /// <summary>The volumes: the <see cref="Master"/> plus one <see cref="SoundVolume"/> per
    /// <see cref="AudioCue"/>, in declaration order (the sounds screen's).</summary>
    public sealed class SoundVolumes
    {
        private readonly SoundVolume[] _volumes;

        public MasterVolume Master { get; }
        public IReadOnlyList<SoundVolume> All => _volumes;

        public SoundVolumes(ISettingsStore store)
        {
            Master = new MasterVolume(store);
            var cues = (AudioCue[])Enum.GetValues(typeof(AudioCue));
            _volumes = new SoundVolume[cues.Length];
            foreach (var cue in cues) _volumes[(int)cue] = new SoundVolume(cue, store, Master);
        }

        public float Gain(AudioCue cue) => _volumes[(int)cue].Gain;
    }
}
