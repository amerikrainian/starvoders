using StarVaders;
using StarVadersAccess.Module.Interop;

namespace StarVadersAccess.Module.UI
{
    /// <summary>
    /// The game's pause menu, opened as its Menu key does (<see cref="KeyInputController.OnMenu"/>):
    /// a click on the scene's pause button. Escape's fallback on screens with no Back of their own.
    /// </summary>
    internal static class Pause
    {
        private static PauseButtonView Button()
        {
            if (GameScenes.Controller<PauseMenuController>() != null) return null;
            foreach (var view in GameScenes.Components<PauseButtonView>())
                if (view != null && view.gameObject.activeInHierarchy) return view;
            return null;
        }

        public static bool CanOpen() => Button() != null;

        public static bool Open()
        {
            var button = Button();
            if (button == null) return false;
            ViewNodes.Click(button);
            return true;
        }
    }
}
