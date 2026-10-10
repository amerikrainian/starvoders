using System.Text;
using StarVoders.Core.UI;
using UnityEngine;

namespace StarVoders.Module.Input
{
    /// <summary>
    /// The engine-backed <see cref="INavInput"/>: frame/time from UnityEngine.Time and keys from the
    /// OS key state (<see cref="OsKeys"/>). Typed text is polled per key (A..Z and space), which is
    /// enough for the type-ahead search: it matches letters only.
    /// </summary>
    internal sealed class UnityNavInput : INavInput
    {
        public int FrameCount => Time.frameCount;
        public float UnscaledTime => Time.unscaledTime;
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;

        public bool CtrlHeld => OsKeys.CtrlHeld;
        public bool AltHeld => OsKeys.AltHeld;
        public bool ShiftHeld => OsKeys.ShiftHeld;
        public bool EscapeDown => OsKeys.Pressed(KeyCode.Escape);
        public bool UpHeld => OsKeys.Held(KeyCode.UpArrow);
        public bool DownHeld => OsKeys.Held(KeyCode.DownArrow);

        private readonly StringBuilder _typed = new StringBuilder(4);

        // The dev driver's typing (dev.type): read as typed on the frame after the request, by every
        // reader of that frame, as a key press is.
        private static string _injected;
        private static int _injectedFrame = int.MinValue / 2;

        public static void InjectTyped(string text)
        {
            _injected = text;
            _injectedFrame = Time.frameCount + 1;
        }

        public string TypedText
        {
            get
            {
                _typed.Length = 0;
                for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
                    if (OsKeys.Pressed(k))
                        _typed.Append((char)('a' + (k - KeyCode.A)));
                if (OsKeys.Pressed(KeyCode.Space))
                    _typed.Append(' ');
                if (_injected != null && Time.frameCount == _injectedFrame) _typed.Append(_injected);
                return _typed.Length == 0 ? "" : _typed.ToString();
            }
        }
    }
}
