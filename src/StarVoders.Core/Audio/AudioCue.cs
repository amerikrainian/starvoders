namespace StarVoders.Core.Audio
{
    /// <summary>
    /// A named one-shot sound (the engine owns the file behind each name, so Core stays free of paths):
    /// one WAV per cue in the plugin's audio folder, named by <see cref="AudioCues.FileName"/>. A cue's
    /// sound is replaced 1:1 by dropping in a file of the same name. Guildrun's pattern.
    /// </summary>
    public enum AudioCue
    {
        /// <summary>A board tile with nothing on it (grid occupancy): a soft noise puff.</summary>
        Empty,
        /// <summary>A board tile with one of the player's own on it (the mech, a bomb, a Summon; any
        /// entity not an invader): a sine blip.</summary>
        Ally,
        /// <summary>A board tile with an invader on it: a buzzy sawtooth blip.</summary>
        Invader,
        /// <summary>A tile (or hand card) the running selection takes: a bell jumping up a fifth. Played
        /// on focus in place of the word "target", and in a sweep.</summary>
        Target,
        /// <summary>A tile an invader will strike: a falling square that restarts, a short siren. Played
        /// on focus in place of "targeted by", and in a sweep.</summary>
        Danger,
        /// <summary>A tile about to be struck (the game's strike mark: a card's preview while its target
        /// is chosen, a delayed strike): a sharp noise crack, in place of the word "strike".</summary>
        Strike,
    }

    public static class AudioCues
    {
        /// <summary>The file a cue plays, without an extension: the enum name in snake case
        /// ("ally").</summary>
        public static string FileName(AudioCue cue)
        {
            string name = cue.ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
