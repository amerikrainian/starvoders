namespace StarVoders.Core.Audio
{
    /// <summary>
    /// The playback backend. An interface so Core stays engine-free; the implementation (the game's
    /// own FMOD core system, a channel group of ours under its SFX bus) lives in the module. Callers
    /// hand a finished volume, pan, pitch and start delay.
    /// </summary>
    public interface IAudioEngine
    {
        /// <summary>Whether the output is usable (false once it failed to open).</summary>
        bool Available { get; }

        /// <summary>Play a one-shot cue at <paramref name="volume"/> (1 as authored), stereo
        /// <paramref name="pan"/> (-1 hard left .. 1 hard right), <paramref name="pitch"/> a
        /// playback-rate multiplier, starting <paramref name="delay"/> seconds after the batch began
        /// (see <see cref="BeginBatch"/>). A missing device or file means silence (logged by the
        /// engine), never a throw.</summary>
        void PlayCue(AudioCue cue, float volume, float pan, float pitch = 1f, double delay = 0);

        /// <summary>Start a batch: the delays of the cues played until the next batch count from this
        /// one moment, so their spacing is exact whatever the frame rate.</summary>
        void BeginBatch();

        /// <summary>Stop every cue playing or scheduled.</summary>
        void StopAll();
    }

    /// <summary>
    /// The engine every caller plays through: scales each cue by that cue's saved volume
    /// (<see cref="SoundVolumes"/>), read live on every play, so an adjustment reaches the next cue.
    /// </summary>
    public sealed class VolumeScaledEngine : IAudioEngine
    {
        private readonly IAudioEngine _inner;
        private readonly SoundVolumes _volumes;

        public VolumeScaledEngine(IAudioEngine inner, SoundVolumes volumes)
        {
            _inner = inner;
            _volumes = volumes;
        }

        public bool Available => _inner.Available;

        public void PlayCue(AudioCue cue, float volume, float pan, float pitch = 1f, double delay = 0)
            => _inner.PlayCue(cue, volume * _volumes.Gain(cue), pan, pitch, delay);

        public void BeginBatch() => _inner.BeginBatch();

        public void StopAll() => _inner.StopAll();
    }

    /// <summary>An engine with no output: what plays before the module has one, and in tests.</summary>
    public sealed class SilentEngine : IAudioEngine
    {
        public bool Available => false;
        public void PlayCue(AudioCue cue, float volume, float pan, float pitch = 1f, double delay = 0) { }
        public void BeginBatch() { }
        public void StopAll() { }
    }
}
