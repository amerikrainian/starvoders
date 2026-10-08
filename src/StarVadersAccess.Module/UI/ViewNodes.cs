using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Module.Interop;
using TMPro;
using UnityEngine;

namespace StarVadersAccess.Module.UI
{
    /// <summary>
    /// Node factories over StarVaders' own widgets. The game's interactive elements are not uGUI
    /// selectables but <see cref="AViewItem"/> MonoBehaviours (buttons, cards, tiles, option rows),
    /// each with explicit left/right/up/down links for its keyboard and controller navigation, and
    /// those that click implement <see cref="IClickable"/>, whose <c>OnClick</c> is where the game's
    /// own click path ends. A node activates through that, and on focus asks its scene's
    /// <see cref="InputHandler"/> to hover the item as a keyboard move would, so the game's cursor,
    /// highlight and tooltip follow the mod's focus. Labels are read live from the item's TMP text.
    /// </summary>
    internal static class ViewNodes
    {
        /// <summary>A clickable view item: "label, button"; Enter is the game's click. With a
        /// <paramref name="describeFrom"/>, the visible texts under that transform other than the
        /// caption follow as the item's description: a choice that explains itself under its title,
        /// where the game draws the explanation beside the button rather than in it. With
        /// <paramref name="enabled"/>, the button reads "disabled" and ignores Enter while it is false
        /// (a game button greyed out, which still takes the click).</summary>
        public static NodeVtable Button(AViewItem item, Func<string> label = null, Transform describeFrom = null,
            Func<bool> enabled = null)
        {
            Func<string> lbl = label ?? (() => Label(item));
            var vt = GameNodes.Button(lbl, () => Click(item), () => IsShown(item) && (enabled == null || enabled()));
            vt.OnFocus = () => Hover(item);
            if (describeFrom != null)
                vt.Announcements = new List<NodeAnnouncement>(vt.Announcements) { GameNodes.TooltipPart(() => Description(describeFrom, lbl())) };
            return vt;
        }

        /// <summary>The visible texts under <paramref name="scope"/> other than the caption, in
        /// order, joined: an item's description. One-letter key prompts are skipped. Null when none.</summary>
        public static string Description(Transform scope, string label)
        {
            if (scope == null) return null;
            var parts = new List<string>();
            foreach (var tmp in scope.GetComponentsInChildren<TMP_Text>(false))
            {
                if (tmp == null) continue;
                string text = tmp.text != null ? tmp.text.Trim() : null;
                if (string.IsNullOrEmpty(text) || text.Length < 2 || text == label || parts.Contains(text)) continue;
                parts.Add(text);
            }
            return parts.Count > 0 ? string.Join(" ", parts) : null;
        }

        /// <summary>The item's caption: its first visible TMP text, else its first TMP text at all
        /// (an item drawn with its caption hidden), else the object's name. One-letter texts are
        /// skipped: they are the key prompts the game draws beside a button ("R", "Q").</summary>
        public static string Label(AViewItem item)
        {
            if (item == null) return null;
            string hidden = null;
            foreach (var tmp in item.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp == null) continue;
                string text = tmp.text;
                if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 2) continue;
                if (tmp.gameObject.activeInHierarchy) return text;
                hidden ??= text;
            }
            return hidden ?? item.gameObject.name;
        }

        /// <summary>Click the item as the game does (<see cref="IClickable.OnClick"/>).</summary>
        public static void Click(AViewItem item)
        {
            if (!IsShown(item)) return;
            var clickable = item.TryCast<IClickable>();
            if (clickable == null)
            {
                CoreLog.Warning("ViewNodes: " + item.gameObject.name + " (" + item.GetIl2CppType().Name + ") is not clickable");
                return;
            }
            clickable.OnClick();
        }

        /// <summary>Hover the item as a keyboard move does: the game's cursor and the item's own
        /// hover state (highlight, tooltip, sound) follow.</summary>
        public static void Hover(AViewItem item)
        {
            if (!IsShown(item)) return;
            try { GameScenes.InputFor(item.gameObject)?.OnHoverViewItemByKey(item); }
            catch (Exception e) { CoreLog.Warning("ViewNodes: hovering " + item.gameObject.name + " failed: " + e.Message); }
        }

        public static bool IsShown(AViewItem item)
            => item != null && item.gameObject.activeInHierarchy;
    }
}
