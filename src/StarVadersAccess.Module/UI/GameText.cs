using System.Text.RegularExpressions;
using StarVadersAccess.Core.Text;

namespace StarVadersAccess.Module.UI
{
    /// <summary>
    /// Game text made speakable. Besides TMP rich text the game uses Text Animator, whose effect tags
    /// come in two forms (<c>&lt;shake a=0.2&gt;</c>, <c>{rot}</c>) and stay in a text until the
    /// animator parses them, and inline sprite icons (<c>&lt;sprite="TextIcons" name="Reroll"&gt;</c>)
    /// that carry meaning ("3 [damage icon]"), so an icon reads as its name.
    /// </summary>
    internal static class GameText
    {
        private static readonly Regex AnimatorTag = new Regex(@"\{/?[a-z]+[^}]*\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Sprite = new Regex(@"<sprite[^>]*?name=""?([^"">]+)""?[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Sprite.Replace(text, " $1 ");
            text = AnimatorTag.Replace(text, "");
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
