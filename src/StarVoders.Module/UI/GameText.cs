using System.Text.RegularExpressions;
using StarVoders.Core.Text;

namespace StarVoders.Module.UI
{
    /// <summary>
    /// Game text made speakable. Besides TMP rich text the game uses Text Animator, whose effect tags
    /// come in two forms (<c>&lt;shake a=0.2&gt;</c>, <c>{rot}</c>) and stay in a text until the
    /// animator parses them, and inline sprite icons (<c>&lt;sprite="TextIcons" name="Reroll"&gt;</c>)
    /// that carry meaning ("3 [damage icon]"), so an icon reads as its name, unless its own word
    /// follows it.
    /// </summary>
    internal static class GameText
    {
        private static readonly Regex AnimatorTag = new Regex(@"\{/?[a-z]+[^}]*\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // Space just inside brackets: the game's LocalizationUtils.GetModifiedDescriptions pads them
        // ("( Strike open hands to hurt the Overseer )" from "(Strike ...)").
        private static readonly Regex PaddedOpen = new Regex(@"([(\[])\s+", RegexOptions.Compiled);
        private static readonly Regex PaddedClose = new Regex(@"\s+([)\]])", RegexOptions.Compiled);
        private static readonly Regex Sprite = new Regex(@"<sprite[^>]*?name=""?([^"">]+)""?[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // A keyword is an icon and its word ("<sprite name=Bullet> <b>Bullet</b>s", "<sprite
        // name=Panic> <b>Doom</b>"): an icon followed by a word is the keyword's decoration and its
        // name is dropped; one standing alone ("2 <sprite name=Heat>") reads as its name, or as the
        // game's word for it where the two differ (Aliases). Icons are marked while the tags between
        // it and the word are stripped.
        private const char IconStart = '\u0001', IconEnd = '\u0002';
        private static readonly Regex IconThenWord = new Regex(@"\u0001([^\u0002]+)\u0002\s*(?=\p{L})", RegexOptions.Compiled);
        private static readonly Regex Icon = new Regex(@"\u0001([^\u0002]+)\u0002", RegexOptions.Compiled);

        // The icons whose sprite is named otherwise than their word, by the game's key for the word:
        // across every game string, Panic is followed by "Doom" (Panic_Keyword) and Reroll by "Chrono
        // Token" (RerollToken_Keyword); every other icon's name is its word. Alone ("BONUS: -1
        // [Panic]") they read as that word.
        private static readonly System.Collections.Generic.Dictionary<string, string> Aliases =
            new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "Panic", "DoomTitle_Misc" },
                { "Reroll", "ChronoTokenHeader_Misc" },
            };

        private static string IconWord(string name)
        {
            if (!Aliases.TryGetValue(name, out var key)) return name;
            try
            {
                string word = StarVaders.LocalizationUtils.LocalizeSynchronous(key);
                if (string.IsNullOrWhiteSpace(word) || word.StartsWith("No translation for", System.StringComparison.Ordinal)) return name;
                return TextUtil.StripRichText(word).Trim();
            }
            catch (System.Exception e)
            {
                Core.CoreLog.Warning("GameText: the word for the " + name + " icon failed: " + e.Message);
                return name;
            }
        }

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Sprite.Replace(text, m => IconStart + m.Groups[1].Value + IconEnd);
            text = AnimatorTag.Replace(text, "");
            text = TextUtil.StripRichText(text);
            text = IconThenWord.Replace(text, "");
            text = Icon.Replace(text, m => " " + IconWord(m.Groups[1].Value) + " ");
            text = TextUtil.StripRichText(text);
            return PaddedClose.Replace(PaddedOpen.Replace(text, "$1"), "$1");
        }

        /// <summary>A title the game writes in capitals ("STARS") as a word ("Stars"): spoken capitals
        /// read as an acronym or a shout. Left alone when any letter is lower case already, or there
        /// is no cased letter (CJK).</summary>
        public static string Calm(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            bool upper = false;
            foreach (char ch in text)
            {
                if (char.IsLower(ch)) return text;
                if (char.IsUpper(ch)) upper = true;
            }
            if (!upper) return text;
            return text.Substring(0, 1) + text.Substring(1).ToLowerInvariant();
        }

        /// <summary>A TMP text's clean text, or null when it is missing or blank.</summary>
        public static string Of(TMPro.TMP_Text tmp)
        {
            if (tmp == null) return null;
            string text = Clean(tmp.text);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}
