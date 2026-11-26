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
        /// <summary>A board tile with something on it (grid occupancy): a sine blip.</summary>
        Occupied,
    }

    public static class AudioCues
    {
        /// <summary>The file a cue plays, without an extension: the enum name in snake case
        /// ("occupied").</summary>
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
