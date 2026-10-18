using System;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Navigation = StarVoders.Core.UI.Navigation;

namespace StarVoders.Module.UI
{
    /// <summary>
    /// Editing one of the game's text fields with the game's own editor (ported from guildrun): the
    /// field is selected and activated as a click does, so the caret, the selection, Ctrl+A, the
    /// clipboard, Delete and any character the OS types are TextMesh Pro's, which reads its keys from
    /// Unity's IMGUI event queue (untouched by focus mode, which disables the Input System's keyboard
    /// alone). While it lasts the mod's keys stand down (<see cref="OwnsKeyboard"/>,
    /// <c>InputManager.TextFieldFocused</c>) and each step is echoed from the field's state before and
    /// after it (<see cref="EditEcho"/>). Enter ends it with focus resting on the field; Escape ends it
    /// and is then the screen's Back, as on any control, keeping the text (the field would put the
    /// old one back, and with it undo what the player just typed); Tab and Shift+Tab, and Up and Down
    /// in a one-line field, end it and move on, as they do from any control. Keys are read from the OS (<see cref="OsKeys"/>): legacy input throws here.
    /// <para><see cref="Ended"/> is raised once an edit ends (the game's end-of-edit listeners have
    /// run: the Compendium's search filters then).</para>
    /// </summary>
    /// <summary>How an edit ended: the field's own Enter (or it let go of focus), Escape, Tab or
    /// Shift+Tab, Up or Down, or the mod stopping it (focus left, the field went away).</summary>
    public enum EditExit { Field, Escape, Next, Prev, Up, Down, Stopped }

    public static class TextEdit
    {
        private const int FocusWaitFrames = 60;

        private static TMP_InputField _field;
        private static bool _pending;      // begun, waiting for the key that began it to come up
        private static bool _seenFocus;
        private static int _deadline;
        private static bool _activated;    // the field's Escape setting is ours to give back
        private static bool _restoreOnEscape;
        private static bool _holdKeys;     // ended, waiting for the key that ended it to come up
        // The field as the last step left it: what the next echo is measured against, not a cache.
        private static EditState _last;
        private static string _startText;  // the text as the edit began
        private static string _quiet;      // an end-of-edit listener skipped when the text is unchanged

        /// <summary>Raised after an edit ends (before the key that ended it moves on): the field, whether
        /// its text changed during the edit, and how it ended.</summary>
        public static event Action<TMP_InputField, bool, EditExit> Ended;

        /// <summary>Set by an <see cref="Ended"/> handler that moves the focus itself (later): the key
        /// that ended the edit then does not move it (the Compendium, after its list rebuilds).</summary>
        public static bool MoveTaken { get; set; }

        public static bool Active => _field != null;

        /// <summary>Whether the keyboard is the field's: while editing, and until the key that ended it
        /// is released (the Escape that leaves the field must not also close the screen).</summary>
        public static bool OwnsKeyboard => Active || _holdKeys;

        /// <summary>Edit the field. Nothing is said: the field taking focus selects its text, and the
        /// echo says that ("123 selected"), or a blank field was just read as one.</summary>
        /// <param name="quietWhenUnchanged">The name of one of the field's end-of-edit listeners not run
        /// when an edit the mod ends leaves the text as it was (the Compendium's <c>OnSearch</c>: it
        /// rebuilds every item, and the focus with them, for nothing).</param>
        public static void Begin(TMP_InputField field, string quietWhenUnchanged = null)
        {
            if (field == null || Active) return;
            _field = field;
            _quiet = quietWhenUnchanged;
            _pending = true;
            _seenFocus = false;
            _activated = false;
        }

        public static void Tick()
        {
            if (_holdKeys && !EndKeyHeld()) _holdKeys = false;
            if (_field == null) return;
            try
            {
                if (!_field.gameObject.activeInHierarchy) { End(announce: false, EditExit.Stopped); return; }
                if (_pending)
                {
                    // The field would read a key still down as its own: the Enter that began the edit as
                    // its submit, the arrow that landed on it as a caret move (Up puts the caret at the
                    // start, in place of the text's selection). It takes focus once every key is up.
                    if (OsKeys.AnyActive) return;
                    _pending = false;
                    _restoreOnEscape = _field.restoreOriginalTextOnEscape;
                    _activated = true;
                    _field.restoreOriginalTextOnEscape = false;
                    var es = EventSystem.current;
                    if (es != null) es.SetSelectedGameObject(_field.gameObject);
                    _field.ActivateInputField();
                    _deadline = Time.frameCount + FocusWaitFrames;
                    _last = new EditState(_field.text, (_field.text ?? "").Length, (_field.text ?? "").Length);
                    _startText = _field.text ?? "";
                    return;
                }
                // Escape is the screen's, as on any control: the edit ends without a word and Back runs
                // (the field may already have taken the key and let go of its focus this frame).
                if (_seenFocus && OsKeys.Pressed(KeyCode.Escape))
                {
                    if (_field.isFocused) Deactivate(_field);
                    End(announce: false, EditExit.Escape);
                    InputManager.Dispatch(UiActions.Back);
                    return;
                }
                if (!_field.isFocused)
                {
                    if (_seenFocus) End(announce: true);
                    else if (Time.frameCount > _deadline)
                    {
                        CoreLog.Warning("TextEdit: the field never took focus");
                        End(announce: true);
                    }
                    return;
                }
                bool first = !_seenFocus;
                _seenFocus = true;
                // A one-line field has no use for Up and Down: they end the edit and move on, as Tab does.
                bool up = OsKeys.Pressed(KeyCode.UpArrow), down = OsKeys.Pressed(KeyCode.DownArrow);
                if ((up || down) && _field.lineType == TMP_InputField.LineType.SingleLine)
                {
                    Deactivate(_field);
                    End(announce: false, up ? EditExit.Up : EditExit.Down);
                    InputManager.Dispatch(up ? UiActions.Up : UiActions.Down);
                    return;
                }
                if (OsKeys.Pressed(KeyCode.Tab))
                {
                    bool back = OsKeys.ShiftHeld;
                    Deactivate(_field);
                    End(announce: false, back ? EditExit.Prev : EditExit.Next);
                    if (!MoveTaken) InputManager.Dispatch(back ? UiActions.Prev : UiActions.Next);
                    return;
                }
                var now = Read(_field);
                string say = EditEcho.Describe(_last, now, OsKeys.CtrlHeld);
                _last = now;
                // The field selects its text as it takes focus: said after the landing, not over it.
                if (say != null) Speech.Say(say, interrupt: !first);
            }
            catch (Exception e)
            {
                CoreLog.Warning("TextEdit: " + e.Message);
                End(announce: false, EditExit.Stopped);
            }
        }

        /// <summary>End an edit under way without a word: focus has left the field.</summary>
        public static void Stop()
        {
            if (Active) End(announce: false, EditExit.Stopped);
        }

        /// <summary>Module teardown: the field goes back to the game as it was.</summary>
        public static void Shutdown()
        {
            if (Active) End(announce: false, EditExit.Stopped);
            _holdKeys = false;
            Ended = null;
        }

        /// <summary>A text field's node: "Search, edit, blank", its name the field's placeholder
        /// ("Search...", the dots dropped) unless <paramref name="label"/> names it, its value the text.
        /// Enter edits it (after <paramref name="prepare"/>: a field the game hides is shown first).</summary>
        public static NodeVtable Node(TMP_InputField field, Func<string> label = null, Action prepare = null,
            string quietWhenUnchanged = null)
        {
            Func<string> name = label ?? (() => Placeholder(field));
            return new NodeVtable
            {
                ControlType = ControlTypes.Edit,
                Announcements = new System.Collections.Generic.List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(name),
                    // Not live: the edit echoes each step itself.
                    new NodeAnnouncement(() => string.IsNullOrEmpty(field.text) ? Strings.ValueBlank : field.text,
                        kind: AnnouncementKinds.Value),
                },
                SearchText = name,
                OnActivate = () => { prepare?.Invoke(); Begin(field, quietWhenUnchanged); },
            };
        }

        /// <summary>
        /// A field edited the moment focus lands on it (guildrun's search): a screen's
        /// <see cref="StarVoders.Core.Screens.Screen.OnUpdate"/> calls <see cref="Tick"/> with whether its
        /// field has the focus. Enter ends the edit with focus still on the field, which then rests
        /// (Enter edits again); focus leaving any other way stops the edit.
        /// </summary>
        public sealed class OnLanding
        {
            private bool _rested; // the player ended the edit and focus has not left the field since

            /// <param name="editOnLanding">False for a field among other controls in a stop: the arrows
            /// pass over it, and Enter edits it.</param>
            public void Tick(bool focused, TMP_InputField field, Action prepare = null, bool editOnLanding = true,
                string quietWhenUnchanged = null)
            {
                if (!focused || !Navigation.FocusActive())
                {
                    if (_rested || TextEdit.Active) TextEdit.Stop();
                    _rested = false;
                    return;
                }
                if (OwnsKeyboard) { _rested = true; return; }
                if (_rested || field == null || !editOnLanding) return;
                prepare?.Invoke();
                if (field.gameObject.activeInHierarchy) Begin(field, quietWhenUnchanged);
            }
        }

        /// <summary>The field's placeholder ("Search..."), its trailing dots dropped, or null.</summary>
        public static string Placeholder(TMP_InputField field)
        {
            var text = field != null && field.placeholder != null ? field.placeholder.GetComponent<TMP_Text>() : null;
            string t = text != null ? GameText.Of(text) : null;
            return string.IsNullOrWhiteSpace(t) ? null : t.TrimEnd('.', '…', ' ');
        }

        // Ends the field's own edit (its end-of-edit listeners run), the quiet listener held off when
        // the text is as the edit found it.
        private static void Deactivate(TMP_InputField field)
        {
            int quiet = -1;
            var events = field.onEndEdit;
            try
            {
                if (_quiet != null && (field.text ?? "") == _startText)
                    for (int i = 0; i < events.GetPersistentEventCount(); i++)
                        if (events.GetPersistentMethodName(i) == _quiet)
                        {
                            quiet = i;
                            events.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                            break;
                        }
                field.DeactivateInputField();
            }
            finally
            {
                // Unity 2020 has no getter for the state: a scene's listener is RuntimeOnly.
                if (quiet >= 0) events.SetPersistentListenerState(quiet, UnityEngine.Events.UnityEventCallState.RuntimeOnly);
            }
        }

        private static EditState Read(TMP_InputField field)
            => new EditState(field.text, field.selectionStringFocusPosition, field.selectionStringAnchorPosition);

        private static bool EndKeyHeld()
            => OsKeys.Held(KeyCode.Return) || OsKeys.Held(KeyCode.Escape) || OsKeys.Held(KeyCode.Tab);

        private static void End(bool announce, EditExit how = EditExit.Field)
        {
            var field = _field;
            _field = null;
            _pending = false;
            _holdKeys = true;
            try
            {
                if (field != null && _activated)
                {
                    field.restoreOriginalTextOnEscape = _restoreOnEscape;
                    if (field.isFocused) Deactivate(field);
                }
                // Focus mode keeps the game's own selection empty.
                var es = EventSystem.current;
                if (es != null && field != null && es.currentSelectedGameObject == field.gameObject) es.SetSelectedGameObject(null);
            }
            catch (Exception e) { CoreLog.Warning("TextEdit: giving the field back failed: " + e.Message); }
            if (announce) Navigation.AnnounceCurrent();
            if (field != null && _activated)
            {
                bool changed = (field.text ?? "") != (_startText ?? "");
                MoveTaken = false;
                try { Ended?.Invoke(field, changed, how); }
                catch (Exception e) { CoreLog.Warning("TextEdit: an end listener failed: " + e.Message); }
            }
        }
    }
}
