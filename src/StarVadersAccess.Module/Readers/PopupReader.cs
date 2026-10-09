using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.UI;
using UnityEngine;

namespace StarVadersAccess.Module.Readers
{
    /// <summary>
    /// Speaks the texts the game pops up by itself, each once, queued, as it shows: the tutorial's
    /// tip box (<see cref="Tips"/>; one that waits for a click is the tip modal's), the act and Chrono Token overlays
    /// (<see cref="ActOverlay"/>, faded in through CanvasGroups), and the pilot's speech bubbles
    /// (spawned under a <see cref="SpeechBubbleManager"/>, faded out after a while; "Roxy: line" when
    /// the line's name gives the speaker). Polled from the
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

        public static void Tick()
        {
            Tip();
            foreach (var overlay in GameScenes.Components<ActOverlay>())
            {
                if (overlay == null) continue;
                Overlay(overlay._canvasGroup);
                Overlay(overlay._replenishCanvasGroup);
            }
            if (Time.frameCount % 5 == 0) Bubbles();
        }

        public static void Reset()
        {
            s_tip = IntPtr.Zero;
            s_overlays.Clear();
            s_bubbles.Clear();
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
