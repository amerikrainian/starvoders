using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.UI;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// A Dialogue System conversation (<c>CutsceneConvManager.PlayConversation</c>: the Chaos God
    /// after a run, the pilots' backstories, Min's scenes), drawn by the one
    /// <see cref="StandardDialogueUI"/> that lives across scenes. The line on screen is one node,
    /// "speaker: line", keyed by its dialogue entry so the next line's arrival reads as a move; Enter
    /// is the click on the panel's continue button (<see cref="DialogueButtonView"/>, whose listener
    /// is <see cref="TextAnimatorContinueButtonFastForward.OnFastForward"/>: it only finishes a line
    /// still typing out, so Enter shows the line in full first and one press always advances). The
    /// player's responses (the menu panel's <see cref="ViewItemUIResponseButton"/>s) are buttons after
    /// it; focus moves to them when they appear.
    ///
    /// The speaker is the name the panel draws, else the subtitle's actor (the Chaos God's panel draws
    /// none: "God"); the "None" actor is narration. The screen stays up for the whole conversation, empty
    /// between lines, so the scene beneath is not announced in the gaps.
    /// </summary>
    public sealed class DialogueScreen : Screen
    {
        public override string Key => "dialogue";
        public override int Layer => 20;
        public override bool Exclusive => true;
        public override bool AllowsTypeahead => false;

        private const string LineStop = "dialogue:line";
        private const string ChoicesStop = "dialogue:choices";

        private static StandardDialogueUI UI
        {
            get
            {
                try
                {
                    if (!DialogueManager.isConversationActive) return null;
                    var ui = DialogueManager.standardDialogueUI;
                    return ui != null && ui.gameObject.activeInHierarchy ? ui : null;
                }
                catch (Exception e)
                {
                    CoreLog.Warning("Dialogue: reading the dialogue UI failed: " + e.Message);
                    return null;
                }
            }
        }

        public override bool IsActive() => UI != null;

        public override void Build(GraphBuilder b)
        {
            var ui = UI;
            if (ui == null) return;

            var panel = LinePanel(ui);
            if (panel != null)
            {
                b.BeginStop(LineStop);
                Func<string> text = () => LineText(panel);
                b.AddItem(LineId(panel), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(text) },
                    SearchText = text,
                    OnActivate = () => Continue(panel),
                });
            }

            var choices = Choices(ui);
            if (choices.Count > 0)
            {
                b.BeginStop(ChoicesStop);
                b.PushContext(null, positions: true);
                for (int i = 0; i < choices.Count; i++)
                {
                    var view = choices[i].dialogueButtonView;
                    var choice = choices[i];
                    b.AddItem(ControlId.Structural("dialogue:choice:" + i + ":" + view.Pointer),
                        ViewNodes.Button(view, () => ChoiceText(choice), enabled: () => view.interactable && !view.forceDisableInteractable));
                }
                b.PopContext();
            }
        }

        // Responses appearing move focus to them; a line that arrived with them (the navigator not
        // yet on it) is spoken first, so the question precedes its answers.
        private bool _hadChoices;

        public override void OnUpdate()
        {
            var ui = UI;
            bool has = ui != null && Choices(ui).Count > 0;
            if (has && !_hadChoices)
            {
                var panel = LinePanel(ui);
                if (panel != null && !Equals(Navigation.FocusedNode?.Id, LineId(panel)))
                {
                    string line = LineText(panel);
                    if (!string.IsNullOrEmpty(line)) Speech.Say(line);
                }
                Navigation.Active?.FocusStop(ChoicesStop);
            }
            _hadChoices = has;
        }

        // ---- the line ----

        // The open panel showing the conversation's current line; else the focused open one; else
        // any open one with text.
        private static StandardUISubtitlePanel LinePanel(StandardDialogueUI ui)
        {
            try
            {
                var panels = ui.conversationUIElements?.subtitlePanels;
                if (panels == null) return null;
                var entry = DialogueManager.currentConversationState?.subtitle?.dialogueEntry;
                StandardUISubtitlePanel focused = null, any = null;
                foreach (var p in panels)
                {
                    if (p == null || !p.isOpen || !p.gameObject.activeInHierarchy) continue;
                    if (string.IsNullOrWhiteSpace(Line(p))) continue;
                    var own = p.currentSubtitle?.dialogueEntry;
                    if (entry != null && own != null && own.id == entry.id && own.conversationID == entry.conversationID) return p;
                    if (p.hasFocus) focused ??= p;
                    any ??= p;
                }
                return focused ?? any;
            }
            catch (Exception e)
            {
                CoreLog.Warning("Dialogue: finding the line's panel failed: " + e.Message);
                return null;
            }
        }

        private static ControlId LineId(StandardUISubtitlePanel panel)
        {
            var entry = panel.currentSubtitle?.dialogueEntry;
            string key = entry != null ? entry.conversationID + ":" + entry.id : "panel:" + panel.Pointer;
            return ControlId.Structural("dialogue:line:" + key);
        }

        private static string Line(StandardUISubtitlePanel panel)
        {
            var field = panel.subtitleText;
            if (field == null) return null;
            var tmp = field.textMeshProUGUI;
            return tmp != null ? GameText.Of(tmp) : GameText.Clean(field.text);
        }

        private static string LineText(StandardUISubtitlePanel panel)
        {
            string line = Line(panel);
            if (string.IsNullOrWhiteSpace(line)) return null;
            string speaker = Speaker(panel);
            return speaker != null ? Strings.CutsceneLine(speaker, line) : line;
        }

        private static string Speaker(StandardUISubtitlePanel panel)
        {
            try
            {
                var drawn = panel.portraitName?.textMeshProUGUI;
                if (drawn != null && drawn.gameObject.activeInHierarchy)
                {
                    string name = GameText.Of(drawn);
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
                string actor = panel.currentSubtitle?.speakerInfo?.Name;
                if (string.IsNullOrWhiteSpace(actor) || string.Equals(actor, "None", StringComparison.OrdinalIgnoreCase)) return null;
                return actor;
            }
            catch (Exception e)
            {
                CoreLog.Warning("Dialogue: reading the speaker failed: " + e.Message);
                return null;
            }
        }

        // The continue button's listener finishes a line still typing out (its animator's visible
        // characters short of the text's) instead of continuing; the player has heard the whole line,
        // so it is shown in full first and the one click continues. A press while the panel's own
        // opening animation plays is ignored by the game.
        private static void Continue(StandardUISubtitlePanel panel)
        {
            var button = panel.continueButton;
            if (!ViewNodes.IsShown(button)) return;
            try
            {
                var player = button.GetComponent<TextAnimatorContinueButtonFastForward>()?.textAnimatorPlayer;
                var animator = player != null ? player.textAnimator : null;
                var tmp = animator != null ? animator.tmproText : null;
                if (tmp != null && tmp.textInfo != null && animator._maxVisibleCharacters < tmp.textInfo.characterCount)
                {
                    player.SkipTypewriter();
                    animator.ShowAllCharacters(false);
                }
            }
            catch (Exception e) { CoreLog.Warning("Dialogue: finishing the typewriter failed: " + e.Message); }
            ViewNodes.Click(button);
        }

        // ---- the responses ----

        private static List<ViewItemUIResponseButton> Choices(StandardDialogueUI ui)
        {
            var list = new List<ViewItemUIResponseButton>();
            try
            {
                var elements = ui.conversationUIElements;
                if (elements == null) return list;
                var seen = new HashSet<IntPtr>();
                void From(StandardUIMenuPanel menu)
                {
                    if (menu == null || !menu.isOpen || !menu.gameObject.activeInHierarchy || !seen.Add(menu.Pointer)) return;
                    foreach (var button in menu.GetComponentsInChildren<ViewItemUIResponseButton>(false))
                        if (button != null && ViewNodes.IsShown(button.dialogueButtonView)) list.Add(button);
                }
                From(elements.defaultMenuPanel);
                if (elements.menuPanels != null) foreach (var menu in elements.menuPanels) From(menu);
            }
            catch (Exception e) { CoreLog.Warning("Dialogue: reading the responses failed: " + e.Message); }
            return list;
        }

        private static string ChoiceText(ViewItemUIResponseButton button)
        {
            string text = null;
            try { text = GameText.Clean(button.text); }
            catch (Exception e) { CoreLog.Warning("Dialogue: reading a response failed: " + e.Message); }
            return string.IsNullOrWhiteSpace(text) ? ViewNodes.Label(button.dialogueButtonView) : text;
        }
    }
}
