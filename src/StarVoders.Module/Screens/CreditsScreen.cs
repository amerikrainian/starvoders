using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using UnityEngine;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The credits (<c>CreditsScene</c>, <see cref="CreditsController"/>: from the main menu, and after
    /// the ending). The game scrolls them past; here they are a list, read whenever, in the panel's
    /// hierarchy order (which is the scroll's order). A role with its name below reads as one line
    /// ("Design &amp; Code: Eddie Cai"); a section of several ("Sound By") is its title, then its
    /// entries; a text of several names on separate lines reads them comma-separated.
    /// Then Toggle Speed (the scroll's own button) and Back. Escape is
    /// <see cref="CreditsController.OnBackButtonPressed"/>; the screen stands down while the game
    /// transitions away (the scroll's end does that by itself).
    /// </summary>
    public sealed class CreditsScreen : Screen
    {
        public override string Key => "credits";
        public override string ScreenName => GameLoc.Get("Credits_Misc");

        private static CreditsController Controller => GameScenes.Controller<CreditsController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsTransitioning;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Leave());
        }

        private static void Leave()
        {
            var c = Controller;
            if (c == null || c.IsTransitioning) return;
            try { c.OnBackButtonPressed(); }
            catch (Exception e) { CoreLog.Warning("Credits: leaving failed: " + e.Message); }
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.ScrollingPanel == null) return;
            b.BeginStop("credits");
            b.PushContext(null, positions: false);
            var lines = new List<string>();
            Walk(c.ScrollingPanel.transform, lines);
            for (int i = 0; i < lines.Count; i++)
            {
                string text = lines[i];
                b.AddItem(ControlId.Structural("credits:line" + i), GameNodes.Text(() => text));
            }
            var speed = GameScenes.Item(c, "Text (TMP)");
            if (speed != null && speed.TryCast<ButtonView>() != null && ViewNodes.IsShown(speed))
                b.AddItem(ControlId.Structural("credits:speed"), ViewNodes.Button(speed));
            if (c.BackButton != null && c.BackButton.activeInHierarchy)
                b.AddItem(ControlId.Structural("credits:back"), GameNodes.Button(() => Strings.Get("bind.ui.back"), Leave));
            b.PopContext();
        }

        // Depth first in hierarchy order. A title is paired with its name: of its text children with
        // none of their own, the one drawn nearest below it (the game nests unevenly: Fund Manager holds
        // its name and the next three roles; Special Thanks its list and the Design Consultation title).
        // A title with no such child stands alone, its children following.
        private static void Walk(Transform t, List<string> lines)
        {
            if (!t.gameObject.activeInHierarchy) return;
            var own = t.GetComponent<TMP_Text>();
            var children = new List<Transform>();
            for (int i = 0; i < t.childCount; i++)
                if (t.GetChild(i).gameObject.activeInHierarchy) children.Add(t.GetChild(i));
            Transform name = null;
            if (own != null)
            {
                float y = t.position.y, best = float.MaxValue;
                foreach (var ch in children)
                {
                    if (ch.GetComponent<TMP_Text>() == null || HasText(ch)) continue;
                    float below = y - ch.position.y;
                    if (below >= -0.01f && below < best) { best = below; name = ch; }
                }
                string title = Text(own);
                string named = name != null ? Text(name.GetComponent<TMP_Text>()) : null;
                string line = title != null && named != null ? title + ": " + named : title ?? named;
                if (line != null) lines.Add(line);
            }
            foreach (var ch in children) if (ch != name) Walk(ch, lines);
        }

        private static bool HasText(Transform t)
        {
            for (int i = 0; i < t.childCount; i++)
                if (t.GetChild(i).gameObject.activeInHierarchy && t.GetChild(i).GetComponent<TMP_Text>() != null) return true;
            return false;
        }

        // The text's lines cleaned and joined by commas: a list of names, one per line, reads as one.
        private static string Text(TMP_Text tmp)
        {
            if (tmp == null || string.IsNullOrEmpty(tmp.text)) return null;
            var parts = new List<string>();
            foreach (var raw in tmp.text.Split('\n'))
            {
                string line = GameText.Clean(raw)?.Trim().TrimEnd(',', ' ');
                if (!string.IsNullOrEmpty(line)) parts.Add(line);
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }
    }
}
