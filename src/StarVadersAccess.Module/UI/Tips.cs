using System;
using StarVaders;
using StarVadersAccess.Core;

namespace StarVadersAccess.Module.UI
{
    /// <summary>
    /// The tutorial's tip box (<see cref="TipManager.CurrentTip"/>): one at a time, two lines of text
    /// typed out, and on some a "[key] to continue" line: those wait for a click, which the game's
    /// keyboard gives through its Click action and the mod through <see cref="Continue"/>.
    /// </summary>
    internal static class Tips
    {
        /// <summary>The tip showing now, or null.</summary>
        public static Tip Current
        {
            get
            {
                var tip = TipManager.CurrentTip;
                return tip != null && tip._isShowing ? tip : null;
            }
        }

        private static TipBox Box(Tip tip) => tip != null ? tip.GetComponentInChildren<TipBox>() : null;

        /// <summary>The tip box's text (its two lines, not the continue prompt), or null.</summary>
        public static string Text(Tip tip)
        {
            var box = Box(tip);
            if (box == null) return null;
            string first = GameText.Of(box.textAnimatorFirst?.tmproText);
            string second = GameText.Of(box.textAnimatorSecond?.tmproText);
            if (first == null) return second;
            return second == null ? first : first + " " + second;
        }

        /// <summary>Whether the tip waits for a click: its box carries the key glow that lights when
        /// the text is done (<see cref="TipBox.LineGlow"/>; the first tip also draws a "[key] to
        /// continue" line). A tip that waits for an action instead (the globe's "Press the proceed
        /// button") has neither.</summary>
        public static bool WaitsForClick(Tip tip)
        {
            var box = Box(tip);
            if (box == null) return false;
            if (box.LineGlow != null) return true;
            var line = box.transform.Find("Anchor (Height)/Line2/ClickToContinueLine");
            return line != null && line.gameObject.activeInHierarchy;
        }

        /// <summary>Click the tip as the game's Click does (<see cref="TipManager.ClickTipBox"/>): a
        /// tip still typing out is shown whole first, so one press always dismisses it (the game's
        /// first click only finishes the typing, and the player has heard the text already).</summary>
        public static void Continue(Tip tip)
        {
            var box = Box(tip);
            if (box == null)
            {
                CoreLog.Warning("Tips: no tip box to click");
                return;
            }
            try
            {
                if (!box._isFinished)
                {
                    box.OnClick();
                    box._isFinished = true;
                }
                TipManager.ClickTipBox();
            }
            catch (Exception e) { CoreLog.Warning("Tips: continuing the tip failed: " + e.Message); }
        }
    }
}
