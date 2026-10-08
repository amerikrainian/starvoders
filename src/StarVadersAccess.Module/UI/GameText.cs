using System.Text.RegularExpressions;
using StarVadersAccess.Core.Text;

namespace StarVadersAccess.Module.UI
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
        private static readonly Regex Sprite = new Regex(@"<sprite[^>]*?name=""?([^"">]+)""?[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // An icon is usually followed by its own word ("<sprite name=Bullet> <b>Bullet</b>", or
        // "...Bullet</b>s"): the icon's name is dropped then, so "Bullet" is not read twice. Icons are marked while the tags
        // between it and the word are stripped.
        private const char IconStart = '\u0001', IconEnd = '\u0002';
        private static readonly Regex IconThenWord = new Regex(@"\u0001([^\u0002]+)\u0002\s*(?=\1)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Icon = new Regex(@"\u0001([^\u0002]+)\u0002", RegexOptions.Compiled);

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Sprite.Replace(text, m => IconStart + m.Groups[1].Value + IconEnd);
            text = AnimatorTag.Replace(text, "");
            text = TextUtil.StripRichText(text);
            text = IconThenWord.Replace(text, "");
            text = Icon.Replace(text, " $1 ");
            return TextUtil.StripRichText(text);
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
