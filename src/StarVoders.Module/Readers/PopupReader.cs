using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Strings;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using UnityEngine;

namespace StarVoders.Module.Readers
{
    /// <summary>
    /// Speaks the texts the game pops up by itself, each once, queued, as it shows: the tutorial's
    /// tip box (<see cref="Tips"/>; one that waits for a click is the tip modal's), the act and Chrono Token overlays
    /// (<see cref="ActOverlay"/>, faded in through CanvasGroups), and the pilot's speech bubbles
    /// (spawned under a <see cref="SpeechBubbleManager"/>, faded out after a while; "Roxy: line" when
    /// the line's name gives the speaker), and the fight's bomb and puppet counters ("1/3 Bomb": shown
    /// only in previews, until they reset: <see cref="BombLimitView.SetBombAmount"/> runs from
    /// AddToPlayerBombListTask and ShowBombNumberTask only while the task engine IsPreviewMode (or
    /// the task IsSkipView), so placing a bomb shows nothing; a bomb card's target preview shows the
    /// count to be, said once per target choice; hovering a bomb on the board shows the current
    /// count, not said: Ctrl+B reads it), also logged to the combat buffer, and the achievement
    /// popups (a block spawned under <see cref="AchievementPopupManager"/>'s
    /// holder, which lives across scenes, slid in and back out, never destroyed; handed to the
    /// <see cref="Screens.AchievementScreen"/> dialog rather than spoken). Polled from the
    /// module tick over components gathered once per scene change; what was spoken is remembered by
    /// native pointer only while it shows.
    /// </summary>
    internal static class PopupReader
    {
        private const float Shown = 0.5f, Hidden = 0.05f;

        private static IntPtr s_tip;
        private static readonly HashSet<IntPtr> s_overlays = new HashSet<IntPtr>();
        private static readonly HashSet<IntPtr> s_bubbles = new HashSet<IntPtr>();
        private static readonly HashSet<IntPtr> s_present = new HashSet<IntPtr>();
        private static readonly Dictionary<IntPtr, string> s_counters = new Dictionary<IntPtr, string>();
        // A counter shown by a target's preview, and its last alpha: quiet until it fades in again.
        private static readonly Dictionary<IntPtr, float> s_previewed = new Dictionary<IntPtr, float>();
        // What a counter said during the current target choice: once per choice, not once per tile.
        private static readonly Dictionary<IntPtr, string> s_selectionSaid = new Dictionary<IntPtr, string>();
        // The holder's blocks already seen; those there when the module loads are taken as seen.
        private static readonly HashSet<IntPtr> s_achievements = new HashSet<IntPtr>();
        private static bool s_achievementsPrimed;

        public static void Tick()
        {
            Tip();
            foreach (var overlay in GameScenes.Components<ActOverlay>())
            {
                if (overlay == null) continue;
                Overlay(overlay._canvasGroup);
                Overlay(overlay._replenishCanvasGroup);
            }
            foreach (var bombs in GameScenes.Components<BombLimitView>())
                if (bombs != null) Counter(bombs.CanvasGroup, bombs.BombLimitText, "Bombs_Keyword", onlyWhileSelecting: true);
            foreach (var puppets in GameScenes.Components<PuppetLimitView>())
                if (puppets != null) Counter(puppets.CanvasGroup, puppets.PuppetLimitText, "Summon_Keyword");
            if (Time.frameCount % 5 == 0)
            {
                Bubbles();
                Achievements();
            }
        }

        public static void Reset()
        {
            s_tip = IntPtr.Zero;
            s_overlays.Clear();
            s_bubbles.Clear();
            s_counters.Clear();
            s_previewed.Clear();
            s_selectionSaid.Clear();
        }

        // A new tip is spoken once its text is in; one that waits for a click is read by the tip
        // modal instead (TipScreen), which takes focus.
        private static void Tip()
        {
            var tip = Tips.Current;
            if (tip == null) { s_tip = IntPtr.Zero; return; }
            if (tip.Pointer == s_tip) return;
            string text = Tips.Text(tip);
            if (text == null) return;
            s_tip = tip.Pointer;
            if (!Tips.WaitsForClick(tip)) Speech.Say(text);
        }

        // A counter drawn as a bare figure ("1/3", beside its icon) takes the game's word for what it
        // counts: "Summon 1/3", as Ctrl+B says it.
        private static string Labelled(string text, string labelKey)
        {
            if (text == null) return null;
            foreach (char ch in text) if (char.IsLetter(ch)) return text;
            string label = GameLoc.Get(labelKey);
            return label != null ? label + " " + text : text;
        }

        // An overlay fading in speaks its text once it is half shown; fully faded out, it may again.
        private static void Overlay(CanvasGroup group)
        {
            if (group == null) return;
            var ptr = group.Pointer;
            float alpha = group.gameObject.activeInHierarchy ? group.alpha : 0f;
            if (alpha < Hidden) { s_overlays.Remove(ptr); return; }
            if (alpha < Shown || s_overlays.Contains(ptr)) return;
            string text = GameText.Of(group.GetComponentInChildren<TMPro.TMP_Text>());
            if (text == null) return;
            s_overlays.Add(ptr);
            Speech.Say(text);
        }

        // A counter fading in speaks its text once half shown, and again when its number changes while
        // it stays up (two bombs placed in a row); faded out, it forgets. The bomb counter speaks only
        // while a target is chosen: on the board it shows when a bomb is hovered, and the count is
        // Ctrl+B's (FightKeys).
        private static void Counter(CanvasGroup group, TMPro.TMP_Text tmp, string labelKey, bool onlyWhileSelecting = false)
        {
            if (group == null) return;
            var ptr = group.Pointer;
            float alpha = group.gameObject.activeInHierarchy ? group.alpha : 0f;
            bool selecting = Fight.Fight.Selecting;
            if (!selecting) s_selectionSaid.Remove(ptr);
            if (alpha < Hidden) { s_counters.Remove(ptr); s_previewed.Remove(ptr); return; }
            // Choosing a target previews the card on each tile the cursor is on, and a bomb card's
            // preview shows the counter with the count to be, the same on every tile, fading out and
            // in between them: spoken once per choice (again only if its text changes). Once the
            // choice ends it speaks again only on a fresh fade-in, not while the last preview fades
            // out.
            if (selecting)
            {
                s_previewed[ptr] = alpha;
                if (alpha < Shown) return;
                string shown = Labelled(GameText.Of(tmp), labelKey);
                s_counters[ptr] = shown;
                if (shown == null || (s_selectionSaid.TryGetValue(ptr, out var once) && once == shown)) return;
                s_selectionSaid[ptr] = shown;
                Speech.Say(shown);
                Fight.FightEvents.Log.Add(shown);
                return;
            }
            if (onlyWhileSelecting) return;
            if (s_previewed.TryGetValue(ptr, out var last))
            {
                if (alpha <= last) { s_previewed[ptr] = alpha; return; }
                s_previewed.Remove(ptr);
                s_counters.Remove(ptr);
            }
            if (alpha < Shown) return;
            string text = Labelled(GameText.Of(tmp), labelKey);
            if (text == null || (s_counters.TryGetValue(ptr, out var said) && said == text)) return;
            s_counters[ptr] = text;
            Speech.Say(text);
            Fight.FightEvents.Log.Add(text);
        }

        // Speech bubbles are spawned per line and destroyed after their fade: each is spoken once its
        // localized text is in (it arrives by callback after the spawn).
        private static void Bubbles()
        {
            s_present.Clear();
            foreach (var manager in GameScenes.Components<SpeechBubbleManager>())
            {
                if (manager == null || !manager.gameObject.activeInHierarchy) continue;
                // The bubbles spawn under the manager's anchor transforms (one per speaker position:
                // the fight's) or under the manager itself, the anchor only placing them (the
                // workshop's, Min at MinSpeech). A bubble seen twice is spoken once.
                Scan(manager.transform);
                var anchors = manager.Transforms;
                if (anchors != null) foreach (var anchor in anchors) if (anchor != null) Scan(anchor);
            }
            s_bubbles.IntersectWith(s_present);
        }

        // A new block under the holder goes to the achievement dialog once its texts are in: the
        // title, then the other shown texts (the description). The blocks stay after sliding out (and lose their
        // AchievementBlock component), so a block is new by pointer, not by being shown.
        /// <summary>Whether a popup block the dialog has not taken yet is under the holder: the game
        /// spawned it and its texts are not in (they arrive a frame or more later).</summary>
        public static bool AchievementPending()
        {
            var manager = AchievementPopupManager.Instance;
            var holder = manager != null ? manager._popupHolder : null;
            if (holder == null || !s_achievementsPrimed) return false;
            for (int i = 0; i < holder.childCount; i++)
            {
                var block = holder.GetChild(i);
                if (block != null && !s_achievements.Contains(block.Pointer)) return true;
            }
            return false;
        }

        private static void Achievements()
        {
            var manager = AchievementPopupManager.Instance;
            var holder = manager != null ? manager._popupHolder : null;
            if (holder == null) return;
            for (int i = 0; i < holder.childCount; i++)
            {
                var block = holder.GetChild(i);
                if (block == null || s_achievements.Contains(block.Pointer)) continue;
                if (!s_achievementsPrimed) { s_achievements.Add(block.Pointer); continue; }
                var parts = new List<string>();
                foreach (var tmp in block.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    string text = GameText.Of(tmp);
                    if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text)) parts.Add(text);
                }
                if (parts.Count == 0) continue;
                s_achievements.Add(block.Pointer);
                Screens.AchievementScreen.Show(parts[0], parts.Count > 1 ? string.Join(" ", parts.GetRange(1, parts.Count - 1)) : null);
            }
            s_achievementsPrimed = true;
        }

        // A character's line is named for them ("Roxy_Excited", "Min_..."); the generic ones
        // ("EnterGlobeScene", "MinWelcome") name no one.
        private static string Speaker(SpeechName name)
        {
            string key = name.ToString();
            int cut = key.IndexOf('_');
            return cut > 0 ? key.Substring(0, cut) : null;
        }

        private static void Scan(Transform anchor)
        {
            foreach (var bubble in anchor.GetComponentsInChildren<SpeechBubble>(false))
            {
                if (bubble == null) continue;
                var ptr = bubble.Pointer;
                s_present.Add(ptr);
                if (s_bubbles.Contains(ptr)) continue;
                var group = bubble.canvasGroup;
                if (group != null && group.alpha < Hidden) continue;
                string text = GameText.Of(bubble.Text);
                if (text == null) continue;
                s_bubbles.Add(ptr);
                string speaker = Speaker(bubble.SpeechName);
                Speech.Say(speaker != null ? Strings.CutsceneLine(speaker, text) : text);
            }
        }
    }
}
