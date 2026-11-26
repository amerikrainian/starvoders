using System;
using System.Collections.Generic;
using System.IO;
using FMOD;
using FMODUnity;
using StarVoders.Core;
using StarVoders.Core.Audio;

namespace StarVoders.Module.Audio
{
    /// <summary>
    /// The cue engine over the game's own FMOD core system (guildrun's): one channel group of ours,
    /// parented under the studio SFX bus's group so the game's SFX and Master sliders govern the cues
    /// (the master group until that bus can be reached), one <see cref="Sound"/> per cue loaded from the
    /// plugin's audio folder on first play and kept. Unity's own audio is off in this game (its output
    /// sample rate reads 0), so FMOD is the only way to a speaker.
    /// Cues are scheduled on the group's DSP clock (<see cref="BeginBatch"/> reads it once, each cue
    /// starts at that clock plus its delay in output samples), so the notes of a sweep keep their
    /// spacing to the sample whatever the frame rate. A missing file logs once and stays silent; a
    /// failed system disables the engine for the session. Its sounds and group are released on module teardown: FMOD handles are
    /// native and survive nothing of a reload.
    /// </summary>
    internal sealed class FmodCueEngine : IAudioEngine, IDisposable
    {
        private const string SfxBus = "bus:/Master/SFX";
        // How far ahead of the clock read a batch starts. None: a start already past when the mixer next
        // runs plays at once, so the first cue is at most a mix block (about 21 ms) late against the rest,
        // and nothing is added to the game's own output latency (its 4 blocks of 1024 samples, about 85 ms
        // at 48 kHz, fixed when it opened FMOD), which already has sounds trailing speech a little.
        private const double Lead = 0;

        private readonly string _assetRoot;
        private readonly Dictionary<AudioCue, Sound> _sounds = new Dictionary<AudioCue, Sound>();
        private readonly HashSet<AudioCue> _warned = new HashSet<AudioCue>();
        private ChannelGroup _group;
        private bool _started, _failed, _underBus, _busWarned;
        private double _nextBusTry;
        private int _rate;
        private ulong _batchClock;
        private bool _batchOpen;

        public FmodCueEngine(string assetRoot) => _assetRoot = assetRoot;

        public bool Available => !_failed;

        private bool EnsureStarted()
        {
            if (_started) return true;
            if (_failed) return false;
            try
            {
                if (!RuntimeManager.IsInitialized) return false; // not failed: the studio system comes up later
                var core = RuntimeManager.CoreSystem;
                var result = core.createChannelGroup("StarVoders", out _group);
                if (result != RESULT.OK) throw new InvalidOperationException("createChannelGroup: " + result);
                if (core.getMasterChannelGroup(out ChannelGroup master) == RESULT.OK) master.addGroup(_group);
                result = core.getSoftwareFormat(out _rate, out _, out _);
                if (result != RESULT.OK || _rate <= 0) { CoreLog.Warning("audio: getSoftwareFormat " + result + "; assuming 48000"); _rate = 48000; }
                _started = true;
                CoreLog.Info("audio: cue channel group opened, " + _rate + " Hz");
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                CoreLog.Warning("audio: FMOD unavailable; cues disabled: " + e.Message);
                return false;
            }
        }

        // Move our group under the SFX bus's, so the game's SFX slider applies. The bus's group exists
        // only once the bus is locked and the studio system has built it: tried again on later plays,
        // a second apart, the group playing under the master meanwhile (a move re-parents it).
        private void TryAttachToSfxBus()
        {
            double now = UnityEngine.Time.unscaledTimeAsDouble;
            if (now < _nextBusTry) return;
            _nextBusTry = now + 1.0;
            string step = "getBus";
            try
            {
                var studio = RuntimeManager.StudioSystem;
                var result = studio.getBus(SfxBus, out FMOD.Studio.Bus bus);
                if (result == RESULT.OK)
                {
                    // Locked by us, or still by the generation before a reload: the group exists either way.
                    // Never unlocked: a reload's old generation unlocking it could free the group the new
                    // generation's cues hang under, and a locked bus costs nothing.
                    step = "lockChannelGroup";
                    result = bus.lockChannelGroup();
                    if (result == RESULT.ERR_ALREADY_LOCKED) result = RESULT.OK;
                }
                if (result == RESULT.OK) { step = "flushCommands"; result = studio.flushCommands(); }
                ChannelGroup parent = default(ChannelGroup);
                if (result == RESULT.OK) { step = "getChannelGroup"; result = bus.getChannelGroup(out parent); }
                if (result == RESULT.OK && !parent.hasHandle()) { step = "bus group not built yet"; result = RESULT.ERR_NOTREADY; }
                if (result == RESULT.OK) { step = "addGroup"; result = parent.addGroup(_group); }
                if (result == RESULT.OK)
                {
                    _underBus = true;
                    CoreLog.Info("audio: cue channel group moved under the SFX bus");
                    return;
                }
                if (!_busWarned) { _busWarned = true; CoreLog.Warning("audio: SFX bus not reached at " + step + " (" + result + "); cues play under the master group until it is"); }
            }
            catch (Exception e)
            {
                if (!_busWarned) { _busWarned = true; CoreLog.Warning("audio: SFX bus attach failed at " + step + ": " + e.Message); }
            }
        }

        public void BeginBatch()
        {
            _batchOpen = false;
            if (!EnsureStarted()) return;
            try
            {
                var result = _group.getDSPClock(out ulong clock, out _);
                if (result != RESULT.OK) { CoreLog.Warning("audio: getDSPClock " + result + "; cues play unscheduled"); return; }
                _batchClock = clock + (ulong)(Lead * _rate);
                _batchOpen = true;
            }
            catch (Exception e) { CoreLog.Warning("audio: getDSPClock failed: " + e.Message); }
        }

        public void PlayCue(AudioCue cue, float volume, float pan, float pitch = 1f, double delay = 0)
        {
            if (volume <= 0f || !EnsureStarted()) return;
            if (!_underBus) TryAttachToSfxBus();
            try
            {
                if (!TryLoad(cue, out var sound)) return;
                // Started paused, so its volume, pan and start are set before a sample of it plays.
                var result = RuntimeManager.CoreSystem.playSound(sound, _group, true, out Channel channel);
                if (result != RESULT.OK)
                {
                    CoreLog.Warning("audio: playSound " + cue + ": " + result);
                    return;
                }
                channel.setVolume(Math.Max(0f, volume));
                channel.setPan(Math.Max(-1f, Math.Min(1f, pan)));
                if (Math.Abs(pitch - 1f) > 0.001f) channel.setPitch(pitch);
                if (_batchOpen)
                {
                    result = channel.setDelay(_batchClock + (ulong)(Math.Max(0, delay) * _rate), 0, false);
                    if (result != RESULT.OK) CoreLog.Warning("audio: setDelay " + cue + ": " + result);
                }
                channel.setPaused(false);
            }
            catch (Exception e)
            {
                CoreLog.Warning("audio: cue " + cue + " failed: " + e.Message);
            }
        }

        public void StopAll()
        {
            if (!_started) return;
            try { _group.stop(); }
            catch (Exception e) { CoreLog.Warning("audio: stop failed: " + e.Message); }
        }

        private bool TryLoad(AudioCue cue, out Sound sound)
        {
            if (_sounds.TryGetValue(cue, out sound)) return true;
            string path = Path.Combine(_assetRoot, AudioCues.FileName(cue) + ".wav");
            if (!File.Exists(path))
            {
                if (_warned.Add(cue)) CoreLog.Warning("audio: no file for " + cue + " at " + path);
                return false;
            }
            var result = RuntimeManager.CoreSystem.createSound(path, MODE.DEFAULT | MODE.LOOP_OFF, out sound);
            if (result != RESULT.OK)
            {
                if (_warned.Add(cue)) CoreLog.Warning("audio: createSound " + cue + ": " + result);
                return false;
            }
            _sounds[cue] = sound;
            return true;
        }

        public void Dispose()
        {
            try
            {
                if (_started) _group.stop();
                foreach (var sound in _sounds.Values) sound.release();
                _sounds.Clear();
                if (_started) _group.release();
            }
            catch (Exception e) { CoreLog.Warning("audio: release failed: " + e.Message); }
            _started = false;
        }
    }
}
