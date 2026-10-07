using System;
using System.Linq;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using BepInEx.Preloader.Core.Patching;
using HarmonyLib;
using Il2CppInterop.Common.XrefScans;

namespace StarVadersAccess.Patcher
{
    /// <summary>
    /// Corrects where Il2CppInterop hooks GenericMethod::GetMethod on this game. Without it, BepInEx
    /// alone (no plugins) crashes StarVaders about ten seconds in.
    /// <para>The lookup (GenericMethod_GetMethod_Hook.FindTargetMethod, unchanged upstream since 2023):
    /// il2cpp_object_get_virtual_method -> Object::GetVirtualMethod -> its last jump target, the shim
    /// that builds an Il2CppGenericMethod and calls GetMethod(gmethod, copyMethodPtr). On Unity 2020,
    /// when the shim has a single jump target, the scan is repeated past the shim's ret and the
    /// SECOND target taken. In this GameAssembly (Unity 2020.3.49f1) GetMethod is compiled directly
    /// after the shim, so the rescan walks into GetMethod's own body and its second target is the
    /// first call there: a pool allocator, called as Alloc(pool, 0x50). Il2CppInterop hooks that
    /// allocator with a (gmethod, bool) detour, the size goes through the bool and arrives as 1, and
    /// the undersized blocks corrupt the heap: the crash lands later, in memset or in a vtable
    /// filled from garbage (URP's VolumeManager.ReloadBaseTypes initializing every class).</para>
    /// <para>The fix: when the shim's only jump target is the very function that follows it, that
    /// function is GetMethod, and the rescan's pick is a call inside it. Every other layout is left
    /// to Il2CppInterop untouched.</para>
    /// </summary>
    [PatcherPluginInfo(Guid, "StarVaders Access preloader fixes", "1.0.0")]
    public sealed class GetMethodHookFix : BasePatcher
    {
        public const string Guid = "com.amerikranian.starvadersaccess.patcher";

        // How far past the shim's start GetMethod may begin and still count as "the next function":
        // the shim is a few dozen bytes plus int3 padding to the next 16-byte boundary.
        private const long MaxShimSpan = 0x100;

        private static ManualLogSource s_log;

        public override void Initialize()
        {
            s_log = Log;
            try
            {
                var hook = AccessTools.TypeByName("Il2CppInterop.Runtime.Injection.Hooks.GenericMethod_GetMethod_Hook");
                var find = hook != null ? AccessTools.Method(hook, "FindTargetMethod") : null;
                if (find == null)
                {
                    Log.LogWarning("GenericMethod_GetMethod_Hook.FindTargetMethod not found; Il2CppInterop changed, fix not applied");
                    return;
                }
                new Harmony(Guid).Patch(find, postfix: new HarmonyMethod(typeof(GetMethodHookFix), nameof(Postfix)));
                Log.LogInfo("GenericMethod::GetMethod lookup fix installed");
            }
            catch (Exception e)
            {
                Log.LogError("Installing the GenericMethod::GetMethod lookup fix failed: " + e);
            }
        }

        private static void Postfix(ref IntPtr __result)
        {
            try
            {
                IntPtr corrected = Correct(__result);
                if (corrected == __result) return;
                s_log?.LogInfo($"GenericMethod::GetMethod: Il2CppInterop picked 0x{__result.ToInt64():X}, a call inside 0x{corrected.ToInt64():X}; hooking 0x{corrected.ToInt64():X}");
                __result = corrected;
            }
            catch (Exception e)
            {
                s_log?.LogError("GenericMethod::GetMethod lookup fix failed, Il2CppInterop's pick kept: " + e);
            }
        }

        private static IntPtr Correct(IntPtr picked)
        {
            if (picked == IntPtr.Zero) return picked;
            IntPtr module = NativeLibrary.Load("GameAssembly.dll");
            IntPtr api = NativeLibrary.GetExport(module, "il2cpp_object_get_virtual_method");
            IntPtr getVirtualMethod = XrefScannerLowLevel.JumpTargets(api).Single();
            var xrefs = XrefScannerLowLevel.JumpTargets(getVirtualMethod).ToArray();
            if (xrefs.Length == 0) return picked;
            IntPtr shim = xrefs.Last();

            var direct = XrefScannerLowLevel.JumpTargets(shim).ToArray();
            if (direct.Length != 1) return picked;
            IntPtr target = direct[0];
            long span = target.ToInt64() - shim.ToInt64();
            if (span <= 0 || span > MaxShimSpan) return picked;

            var past = XrefScannerLowLevel.JumpTargets(shim, ignoreRetn: true).Take(2).ToArray();
            if (past.Length == 2 && past[0] == target && past[1] == picked) return target;
            return picked;
        }
    }
}
