using System;
using System.Runtime.InteropServices;
using StarVadersAccess.Dev;
using StarVadersAccess.Modularity;
using UnityEngine;

namespace StarVadersAccess.Host
{
    /// <summary>
    /// The one injected MonoBehaviour, registered once for the process (IL2CPP type registration is
    /// permanent, which is why this lives in the host and never reloads). Each frame it pumps the dev
    /// server's job queue, drives the current feature module's per-frame work, and watches for the F6
    /// reload hotkey. The module is read fresh from the loader each frame so a reload swaps it in with
    /// no further wiring.
    /// </summary>
    public sealed class HostPump : MonoBehaviour
    {
        // Required for an IL2CPP-injected MonoBehaviour.
        public HostPump(IntPtr ptr) : base(ptr) { }

#if DEBUG
        internal static DevServer DevServer;
        private static bool s_f6Down;
#endif
        internal static ModuleLoader Loader;

        private void Update()
        {
#if DEBUG
            try
            {
                DevServer?.Pump();
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning("DevServer.Pump: " + ex);
            }
#endif

            try
            {
                Loader?.Module?.Tick();
            }
            catch (Exception ex)
            {
                // Full exception (with stack); a per-frame Tick failure is otherwise unlocatable.
                Plugin.Logger?.LogWarning("Module.Tick: " + ex);
            }

#if DEBUG
            if (F6Pressed())
            {
                Plugin.Logger?.LogInfo("F6: reloading module");
                // Through the dev server so the eval REPL is reset to the new module's types too,
                // matching POST /reload.
                DevServer?.ReloadFromHost();
            }
#endif
        }

#if DEBUG
        // The game runs the new Input System alone (activeInputHandler = 1), so legacy Input throws,
        // and the module disables the Input System keyboard while it owns the keys: F6 is read from
        // the OS, while the game's window is in front.
        private const int VK_F6 = 0x75;

        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private static readonly uint s_pid = (uint)Environment.ProcessId;

        private static bool F6Pressed()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
            bool down = pid == s_pid && (GetAsyncKeyState(VK_F6) & 0x8000) != 0;
            bool pressed = down && !s_f6Down;
            s_f6Down = down;
            return pressed;
        }
#endif
    }
}
