using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.UI;
using TMPro;
using UnityEngine;
using Screen = StarVadersAccess.Core.Screens.Screen;

namespace StarVadersAccess.Module.Screens
{
    /// <summary>
    /// The game's yes/no prompt (an additive scene over whatever asked: skipping a cutscene...). Its
    /// title and message ("SKIP CUTSCENE? Proceed?") name a dialog context holding the buttons of the
    /// setup showing: YES and NO, or the proceed-only OK. Focus lands where the game's own cursor
    /// starts (NO). Escape is NO, or OK when it is the only choice. The prompt fades out before its scene unloads and nothing marks it as
    /// closing (its clicks guard only against a scene transition), so once a choice is clicked the
    /// screen declares nothing for that prompt until its scene goes: a second Enter would run the
    /// choice's action again, and standing down instead would hand it to the screen beneath.
    /// </summary>
    public sealed class ConfirmPromptScreen : Screen
    {
        public override string Key => "confirm";
        public override int Layer => 20;
        public override bool Exclusive => true;
        public override bool AllowsTypeahead => false;
        public override object InitialFocusStop => "buttons";

        // The prompt answered last, by native pointer (a proxy is a fresh object per cast).
        private IntPtr _answered;

        private static ConfirmPromptController Controller => GameScenes.Controller<ConfirmPromptController>();

        public override bool IsActive()
        {
            var c = Controller;
            if (c == null) { _answered = IntPtr.Zero; return false; }
            return c.Pointer == _answered || Panel(c) != null;
        }

        // The setup showing: the yes/no pair or the lone OK.
        private static GameObject Panel(ConfirmPromptController c)
        {
            if (c.NormalSetup != null && c.NormalSetup.activeInHierarchy) return c.NormalSetup;
            if (c.ProceedOnlySetup != null && c.ProceedOnlySetup.activeInHierarchy) return c.ProceedOnlySetup;
            return null;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.Pointer == _answered) return;
            var panel = Panel(c);
            if (panel == null) return;
            b.BeginStop("buttons");
            b.PushContext(Name(c, panel), Strings.RoleDialog, positions: false);
            var start = c.ViewItemToStartAt;
            foreach (var item in panel.GetComponentsInChildren<AViewItem>(false))
            {
                if (item == null) continue;
                var vt = ViewNodes.Button(item);
                var click = vt.OnActivate;
                vt.OnActivate = () => Answer(c, click);
                var self = item;
                vt.IsSelected = () => start != null && start.Pointer == self.Pointer;
                b.AddItem(ControlId.Structural("confirm:" + item.gameObject.name), vt);
            }
            b.PopContext();
        }

        // The title, then the setup's texts that are not a button's (the message).
        private static string Name(ConfirmPromptController c, GameObject panel)
        {
            var parts = new List<string>();
            var title = c.TitleText;
            if (title != null && !string.IsNullOrWhiteSpace(title.text)) parts.Add(title.text.Trim());
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
                if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text) && tmp.GetComponentInParent<AViewItem>() == null)
                    parts.Add(tmp.text.Trim());
            return parts.Count > 0 ? string.Join(" ", parts) : Strings.ScreenConfirm;
        }

        private void Answer(ConfirmPromptController c, Action click)
        {
            if (c.Pointer == _answered) return;
            _answered = c.Pointer;
            click();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var c = Controller;
                if (c == null || c.Pointer == _answered) return;
                var cancel = GameScenes.Item(c, c.NormalSetup != null && c.NormalSetup.activeInHierarchy ? "NoButton" : "OKButton");
                if (!ViewNodes.IsShown(cancel)) return;
                Answer(c, () => ViewNodes.Click(cancel));
            });
        }
    }
}
