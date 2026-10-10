using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace StarVoders.Module.Input
{
    /// <summary>
    /// The mod's keyboard, read from the OS. StarVaders runs the new Input System alone
    /// (PlayerSettings activeInputHandler = 1), so Unity's legacy Input throws, and focus mode disables
    /// the Input System keyboard so the game hears none of our keys: the mod therefore reads keys
    /// below Unity, with user32's GetAsyncKeyState, and only while the game's window is in front (the
    /// call is system-wide). One snapshot per frame, taken on the first query of the frame, so every
    /// reader of a frame sees the same presses; pressed and released are the edges between this
    /// frame's snapshot and the last one. A press and release inside one frame is not seen, which a
    /// human key press never is.
    /// <para>Bindings keep naming keys by Unity's <see cref="KeyCode"/> (their saved form, the key
    /// help's names); <see cref="VirtualKey"/> maps each to its Windows virtual-key code. Keypad Enter
    /// shares VK_RETURN with Enter: Windows tells them apart only by an extended-key flag the async
    /// state does not carry.</para>
    /// </summary>
    internal static class OsKeys
    {
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private static readonly uint s_pid = (uint)Environment.ProcessId;
        private static readonly bool[] s_down = new bool[256];
        private static readonly bool[] s_prev = new bool[256];
        private static int s_frame = int.MinValue;
        private static bool s_foreground;

        // Only the keys some binding or reader can name are polled.
        private static readonly Dictionary<KeyCode, int> s_vk = BuildMap();
        private static readonly int[] s_polled = BuildPolled();

        /// <summary>Whether the game's window is the foreground window this frame.</summary>
        public static bool Foreground { get { Refresh(); return s_foreground; } }

        public static bool Held(KeyCode key) { Refresh(); int vk = VirtualKey(key); return vk > 0 && s_down[vk]; }
        public static bool Pressed(KeyCode key) { Refresh(); int vk = VirtualKey(key); return vk > 0 && s_down[vk] && !s_prev[vk]; }
        public static bool Released(KeyCode key) { Refresh(); int vk = VirtualKey(key); return vk > 0 && !s_down[vk] && s_prev[vk]; }

        public static bool CtrlHeld => Held(KeyCode.LeftControl) || Held(KeyCode.RightControl);
        public static bool ShiftHeld => Held(KeyCode.LeftShift) || Held(KeyCode.RightShift);
        public static bool AltHeld => Held(KeyCode.LeftAlt) || Held(KeyCode.RightAlt);

        /// <summary>The Windows virtual-key code of a Unity key, or 0 when it has none here.</summary>
        public static int VirtualKey(KeyCode key) => s_vk.TryGetValue(key, out int vk) ? vk : 0;

        private static void Refresh()
        {
            int frame = Time.frameCount;
            if (frame == s_frame) return;
            s_frame = frame;
            Array.Copy(s_down, s_prev, s_down.Length);
            GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
            s_foreground = pid == s_pid;
            foreach (int vk in s_polled)
                s_down[vk] = s_foreground && (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private static int[] BuildPolled()
        {
            var set = new SortedSet<int>(s_vk.Values);
            var arr = new int[set.Count];
            set.CopyTo(arr);
            return arr;
        }

        private static Dictionary<KeyCode, int> BuildMap()
        {
            var m = new Dictionary<KeyCode, int>();
            for (int i = 0; i < 26; i++) m[KeyCode.A + i] = 0x41 + i;
            for (int i = 0; i < 10; i++) m[KeyCode.Alpha0 + i] = 0x30 + i;
            for (int i = 0; i < 10; i++) m[KeyCode.Keypad0 + i] = 0x60 + i;
            for (int i = 0; i < 12; i++) m[KeyCode.F1 + i] = 0x70 + i;
            m[KeyCode.Backspace] = 0x08;
            m[KeyCode.Tab] = 0x09;
            m[KeyCode.Return] = 0x0D;
            m[KeyCode.KeypadEnter] = 0x0D;
            m[KeyCode.Escape] = 0x1B;
            m[KeyCode.Space] = 0x20;
            m[KeyCode.PageUp] = 0x21;
            m[KeyCode.PageDown] = 0x22;
            m[KeyCode.End] = 0x23;
            m[KeyCode.Home] = 0x24;
            m[KeyCode.LeftArrow] = 0x25;
            m[KeyCode.UpArrow] = 0x26;
            m[KeyCode.RightArrow] = 0x27;
            m[KeyCode.DownArrow] = 0x28;
            m[KeyCode.Insert] = 0x2D;
            m[KeyCode.Delete] = 0x2E;
            m[KeyCode.KeypadMultiply] = 0x6A;
            m[KeyCode.KeypadPlus] = 0x6B;
            m[KeyCode.KeypadMinus] = 0x6D;
            m[KeyCode.KeypadPeriod] = 0x6E;
            m[KeyCode.KeypadDivide] = 0x6F;
            m[KeyCode.LeftShift] = 0xA0;
            m[KeyCode.RightShift] = 0xA1;
            m[KeyCode.LeftControl] = 0xA2;
            m[KeyCode.RightControl] = 0xA3;
            m[KeyCode.LeftAlt] = 0xA4;
            m[KeyCode.RightAlt] = 0xA5;
            m[KeyCode.Semicolon] = 0xBA;
            m[KeyCode.Equals] = 0xBB;
            m[KeyCode.Comma] = 0xBC;
            m[KeyCode.Minus] = 0xBD;
            m[KeyCode.Period] = 0xBE;
            m[KeyCode.Slash] = 0xBF;
            m[KeyCode.BackQuote] = 0xC0;
            m[KeyCode.LeftBracket] = 0xDB;
            m[KeyCode.Backslash] = 0xDC;
            m[KeyCode.RightBracket] = 0xDD;
            m[KeyCode.Quote] = 0xDE;
            return m;
        }
    }
}
