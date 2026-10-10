using System;
using System.Collections.Generic;
using StarVoders.Core;
using Il2CppInterop.Runtime;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace StarVoders.Module.Interop
{
    /// <summary>
    /// The live scene controllers and input handlers. StarVaders has no DI container: each scene carries one
    /// <c>StarVaders.SceneController</c> subclass (BootupController, MainMenuSceneController,
    /// EncounterController...) that owns the scene's views, and screens find their game state from
    /// it. The list is rebuilt from the loaded scenes' roots when Unity reports a scene loaded or
    /// unloaded (sceneLoaded fires after Awake, so the controller exists), never per frame: a lookup
    /// is a walk of a handful of entries. Destroyed controllers read as null through Unity's own
    /// equality, so a stale entry is skipped until the next rebuild.
    /// </summary>
    internal static class GameScenes
    {
        private static readonly List<StarVaders.SceneController> s_controllers = new List<StarVaders.SceneController>();
        private static readonly List<StarVaders.InputHandler> s_inputs = new List<StarVaders.InputHandler>();
        private static UnityAction<Scene, LoadSceneMode> s_loadedHook;
        private static UnityAction<Scene> s_unloadedHook;
        private static bool s_dirty = true;

        /// <summary>Hook the scene events and take the scenes already loaded (a reload).</summary>
        public static void Start()
        {
            try
            {
                var loaded = DelegateSupport.ConvertDelegate<UnityAction<Scene, LoadSceneMode>>(
                    new Action<Scene, LoadSceneMode>((_, __) => s_dirty = true));
                var unloaded = DelegateSupport.ConvertDelegate<UnityAction<Scene>>(
                    new Action<Scene>(_ => s_dirty = true));
                SceneManager.add_sceneLoaded(loaded);
                SceneManager.add_sceneUnloaded(unloaded);
                s_loadedHook = loaded;
                s_unloadedHook = unloaded;
            }
            catch (Exception e)
            {
                CoreLog.Error("GameScenes: hooking the scene events failed, screens will not see scene changes: " + e);
            }
            s_dirty = true;
        }

        public static void Shutdown()
        {
            try
            {
                if (s_loadedHook != null) SceneManager.remove_sceneLoaded(s_loadedHook);
                if (s_unloadedHook != null) SceneManager.remove_sceneUnloaded(s_unloadedHook);
            }
            catch (Exception e) { CoreLog.Warning("GameScenes: unhooking the scene events failed: " + e.Message); }
            s_loadedHook = null;
            s_unloadedHook = null;
            s_controllers.Clear();
            s_inputs.Clear();
            s_items.Clear();
            s_components.Clear();
        }

        /// <summary>The scene's input handler (each scene has its own; the enabled one is the
        /// scene in front), or null. It owns the game's hover state: what the keyboard and the mouse
        /// point at.</summary>
        public static StarVaders.InputHandler Input
        {
            get
            {
                if (s_dirty) Rebuild();
                StarVaders.InputHandler any = null;
                foreach (var h in s_inputs)
                {
                    if (h == null) continue;
                    if (h.isActiveAndEnabled) return h;
                    any ??= h;
                }
                return any;
            }
        }

        /// <summary>The input handler of the scene <paramref name="go"/> lives in (an additive scene
        /// over the menu has its own), else <see cref="Input"/>.</summary>
        public static StarVaders.InputHandler InputFor(UnityEngine.GameObject go)
        {
            if (go == null) return Input;
            if (s_dirty) Rebuild();
            int scene = go.scene.handle;
            foreach (var h in s_inputs)
                if (h != null && h.gameObject.scene.handle == scene) return h;
            return Input;
        }

        /// <summary>The live controller of that type, or null when its scene is not loaded.</summary>
        public static T Controller<T>() where T : StarVaders.SceneController
        {
            if (s_dirty) Rebuild();
            foreach (var c in s_controllers)
            {
                if (c == null) continue;
                var t = c.TryCast<T>();
                if (t != null) return t;
            }
            return null;
        }

        // The view items of each controller's scene by object name, keyed by the controller's native
        // pointer (a proxy is a fresh object per cast): a scene's widgets do not change while it
        // lives, so they are gathered once per controller and dropped when the scenes change.
        private static readonly Dictionary<IntPtr, Dictionary<string, StarVaders.AViewItem>> s_items =
            new Dictionary<IntPtr, Dictionary<string, StarVaders.AViewItem>>();

        /// <summary>The view item of that object name in the controller's scene (shown or not), or
        /// null. For the widgets a controller holds no field for.</summary>
        public static StarVaders.AViewItem Item(StarVaders.SceneController controller, string name)
        {
            if (controller == null) return null;
            if (s_dirty) Rebuild();
            if (!s_items.TryGetValue(controller.Pointer, out var items))
            {
                items = new Dictionary<string, StarVaders.AViewItem>();
                s_items[controller.Pointer] = items;
                try
                {
                    foreach (var root in controller.gameObject.scene.GetRootGameObjects())
                        foreach (var item in root.GetComponentsInChildren<StarVaders.AViewItem>(true))
                            if (item != null && !items.ContainsKey(item.gameObject.name)) items[item.gameObject.name] = item;
                }
                catch (Exception e) { CoreLog.Warning("GameScenes: gathering view items failed: " + e.Message); }
            }
            return items.TryGetValue(name, out var found) && found != null ? found : null;
        }

        // Components of a type across the loaded scenes, gathered once per type per scene change
        // (inactive ones included): for the readers that watch a scene's popups every frame.
        private static readonly Dictionary<Type, System.Collections.IList> s_components = new Dictionary<Type, System.Collections.IList>();

        /// <summary>The loaded scenes' components of that type (shown or not), gathered when first
        /// asked after a scene change; destroyed ones read as null.</summary>
        public static List<T> Components<T>() where T : UnityEngine.Component
        {
            if (s_dirty) Rebuild();
            if (s_components.TryGetValue(typeof(T), out var cached)) return (List<T>)cached;
            var list = new List<T>();
            s_components[typeof(T)] = list;
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var c in root.GetComponentsInChildren<T>(true))
                            if (c != null) list.Add(c);
                }
            }
            catch (Exception e) { CoreLog.Warning("GameScenes: gathering " + typeof(T).Name + " failed: " + e.Message); }
            return list;
        }

        /// <summary>Every live controller, for the dev driver.</summary>
        public static IEnumerable<StarVaders.SceneController> All
        {
            get
            {
                if (s_dirty) Rebuild();
                foreach (var c in s_controllers) if (c != null) yield return c;
            }
        }

        private static void Rebuild()
        {
            s_dirty = false;
            s_controllers.Clear();
            s_inputs.Clear();
            s_items.Clear();
            s_components.Clear();
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var c in root.GetComponentsInChildren<StarVaders.SceneController>(true))
                            if (c != null) s_controllers.Add(c);
                        foreach (var h in root.GetComponentsInChildren<StarVaders.InputHandler>(true))
                            if (h != null) s_inputs.Add(h);
                    }
                }
            }
            catch (Exception e)
            {
                CoreLog.Warning("GameScenes: rebuilding the controller list failed: " + e.Message);
            }
        }
    }
}
