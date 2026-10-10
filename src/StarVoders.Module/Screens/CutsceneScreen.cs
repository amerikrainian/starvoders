using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The story cutscenes, the opening (Full Tutorial's start) and the ending: a timeline that pauses
    /// on each speech bubble until it is clicked. Each bubble is one node, "speaker: line", so the
    /// next line's arrival reads as a move; Enter clicks the bubble as the mouse does (a line still
    /// typing out shown in full first, so the click advances rather than finishing it), Escape is
    /// the game's skip (its confirm prompt). Nothing is declared while no bubble shows (the timeline
    /// playing between lines).
    ///
    /// The game never draws a speaker name (the bubble's colour tells them apart), so the name is the
    /// Dialogue System actor's. The controller opens <c>_dialogueBubbles[_currIndex - 1]</c> with
    /// dialogue entry <c>_currIndex - _dialogueDatabaseIndexOffset</c>, then increments
    /// <c>_currIndex</c>; a bubble with a <c>LocKeyOverride</c> (Roxy's run of "wait"s) takes its line
    /// from that key, its actor from the key's last segment, and bumps the offset instead.
    /// </summary>
    public sealed class CutsceneScreen : Screen
    {
        public override string Key => "cutscene";
        public override int Layer => 10;
        public override bool Exclusive => true;
        public override bool AllowsTypeahead => false;

        // The controller fields the screen reads, from whichever cutscene is live.
        private sealed class Scene
        {
            public Il2CppSystem.Collections.Generic.List<CutsceneDialogueBubble> Bubbles;
            public int Index;
            public int Offset;
            public CutsceneDialogueHelper Helper;
            public Action Skip;
        }

        private static Scene Live()
        {
            var opening = GameScenes.Controller<OpeningSceneController>();
            if (opening != null)
            {
                if (opening.IsEndingScene || opening.IsSlowEndingScene) return null;
                return new Scene
                {
                    Bubbles = opening._dialogueBubbles, Index = opening._currIndex,
                    Offset = opening._dialogueDatabaseIndexOffset, Helper = opening._dialogueBubbleHandler,
                    Skip = opening.OnSkipCutscene,
                };
            }
            var ending = GameScenes.Controller<EndingSceneController>();
            if (ending != null)
            {
                if (ending.IsEndingScene || ending.IsSlowEndingScene) return null;
                return new Scene
                {
                    Bubbles = ending._dialogueBubbles, Index = ending._currIndex,
                    Offset = ending._dialogueDatabaseIndexOffset, Helper = ending._dialogueBubbleHandler,
                    Skip = ending.OnSkipCutscene,
                };
            }
            return null;
        }

        public override bool IsActive() => Live() != null;

        public override void Build(GraphBuilder b)
        {
            var scene = Live();
            var bubble = scene?.Helper?._currBubble;
            if (bubble == null || !bubble.gameObject.activeInHierarchy) return;
            if (string.IsNullOrWhiteSpace(LineOf(bubble))) return;
            Func<string> text = () =>
            {
                string speaker = Speaker(Live(), bubble);
                return speaker != null ? Strings.CutsceneLine(speaker, LineOf(bubble)) : LineOf(bubble);
            };
            b.AddItem(ControlId.Structural("cutscene:" + bubble.GetInstanceID()), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(text) },
                SearchText = text,
                OnActivate = () => Advance(bubble),
            });
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Live()?.Skip());
        }

        // The game's click advances only when no shown bubble is still typing out (its animator's
        // visible characters short of the text's); otherwise it just finishes the typewriter. The
        // player has heard the whole line already, so Enter shows every such bubble in full first
        // and the one click advances.
        private static void Advance(CutsceneDialogueBubble bubble)
        {
            var bubbles = Live()?.Bubbles;
            if (bubbles != null)
            {
                foreach (var b in bubbles)
                {
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    try
                    {
                        var player = b.TextAnimatorPlayer;
                        var animator = player != null ? player.textAnimator : null;
                        var tmp = animator != null ? animator.tmproText : null;
                        if (tmp == null || tmp.textInfo == null || animator._maxVisibleCharacters >= tmp.textInfo.characterCount) continue;
                        player.SkipTypewriter();
                        animator.ShowAllCharacters(false);
                    }
                    catch (Exception e) { CoreLog.Warning("Cutscene: finishing " + b.gameObject.name + "'s typewriter failed: " + e.Message); }
                }
            }
            ViewNodes.Click(bubble);
        }

        private static string LineOf(CutsceneDialogueBubble bubble) => GameText.Of(bubble.Text);

        // The actor of the bubble on screen, when it is the one the controller opened last; null
        // otherwise (a bubble the timeline shows by itself) or when the database has no such actor.
        private static string Speaker(Scene scene, CutsceneDialogueBubble bubble)
        {
            if (scene?.Helper == null) return null;
            try
            {
                var helper = scene.Helper;
                var db = helper._dialogueDB;
                int i = scene.Index - 2;
                if (db == null || scene.Bubbles == null || i < 0 || i >= scene.Bubbles.Count) return null;
                var opened = scene.Bubbles[i];
                if (opened == null || opened.Pointer != bubble.Pointer) return null;

                Actor actor;
                string key = bubble.LocKeyOverride;
                if (!string.IsNullOrEmpty(key))
                {
                    int cut = key.LastIndexOf('_');
                    actor = db.GetActor(cut >= 0 ? key.Substring(cut + 1) : key);
                }
                else
                {
                    var entry = db.GetConversation(helper._conversationName)?.GetDialogueEntry(scene.Index - 1 - scene.Offset);
                    actor = entry != null ? db.GetActor(entry.ActorID) : null;
                }
                if (actor == null) return null;
                string display = Field.LookupValue(actor.fields, "Display Name");
                return string.IsNullOrWhiteSpace(display) ? actor.Name : display;
            }
            catch (Exception e)
            {
                CoreLog.Warning("Cutscene: reading the speaker failed: " + e.Message);
                return null;
            }
        }
    }
}
