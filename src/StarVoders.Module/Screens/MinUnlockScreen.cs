using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using UnityEngine;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// What a full fundraiser in Min's Workshop unlocks (the tip jar's click once it reads "Click Me!":
    /// <see cref="MinUnlockCanvas"/>, <c>ShopController.currentUnlockCanvas</c>, a child of the
    /// workshop), as a dialog named by its drawn title ("Card Unlocked!"). The unlocked card
    /// (<c>CardUnlockDisplayBoard</c>) or artifact (<c>ArtifactUnlockDisplayBoard</c>) as the rewards
    /// read them, Enter = its click (the more info page); a mechanic's <c>MiscUnlockText</c>. Then Take
    /// (<c>OnClickGetUnlockCard</c>: into this run's deck) and Skip, or Continue, the game's buttons.
    /// Taking and skipping: never through the driver. Escape is Continue when that is the only choice,
    /// else the pause fallback (Take and Skip both decide something).
    ///
    /// The canvas fades in, so the screen declares nothing until it is opaque with its unlock and a
    /// button shown (3 s at most). It stands down once the canvas <c>IsEndingScene</c> (it slides out
    /// before it is destroyed) and the workshop is read again.
    /// </summary>
    public sealed class MinUnlockScreen : Screen
    {
        public override string Key => "minunlock";
        public override int Layer => 15;
        public override bool Exclusive => true;
        public override bool AllowsTypeahead => false;

        private const float Settled = 0.99f, MaxWait = 3f;

        private static MinUnlockCanvas Canvas
        {
            get
            {
                var c = GameScenes.Controller<ShopController>();
                if (c == null || c.IsEndingScene || !c.IsShowingUnlockCanvas) return null;
                var canvas = c.currentUnlockCanvas;
                return canvas != null && canvas.gameObject.activeInHierarchy && !canvas.IsEndingScene ? canvas : null;
            }
        }

        public override bool IsActive() => Canvas != null;

        // The canvas first seen when, by pointer, for the settle cap.
        private System.IntPtr _seen;
        private float _seenAt;

        public override bool HoldsArrival => !IsSettled(Canvas);

        private bool IsSettled(MinUnlockCanvas canvas)
        {
            if (canvas == null) return true;
            if (canvas.Pointer != _seen) { _seen = canvas.Pointer; _seenAt = Time.unscaledTime; }
            if (Time.unscaledTime - _seenAt >= MaxWait) return true;
            if (canvas.Canvas != null && canvas.Canvas.alpha < Settled) return false;
            if (CardModel(canvas) == null && ArtifactModel(canvas) == null && Misc(canvas) == null) return false;
            return ViewNodes.IsShown(canvas.TakeButton) || ViewNodes.IsShown(canvas.SkipButton) || ViewNodes.IsShown(canvas.ContinueButton);
        }

        private static CardModel CardModel(MinUnlockCanvas canvas)
        {
            var board = canvas.CardUnlockDisplayBoard;
            return board != null && board.gameObject.activeInHierarchy ? board.CardModel : null;
        }

        private static ArtifactModel ArtifactModel(MinUnlockCanvas canvas)
        {
            var board = canvas.ArtifactUnlockDisplayBoard;
            return board != null && board.gameObject.activeInHierarchy ? board.ArtifactModel : null;
        }

        private static string Misc(MinUnlockCanvas canvas)
            => canvas.MiscUnlockObject != null && canvas.MiscUnlockObject.activeInHierarchy ? GameText.Of(canvas.MiscUnlockText) : null;

        public override void Build(GraphBuilder b)
        {
            var canvas = Canvas;
            if (canvas == null || !IsSettled(canvas)) return;
            string id = "minunlock:" + canvas.Pointer + ":";

            b.PushContext(GameText.Of(canvas.UnlockTitle), Strings.RoleDialog, positions: false);
            var card = CardModel(canvas);
            if (card != null)
                b.AddItem(ControlId.Structural(id + "card"), CardNodes.Card(card, canvas.CardUnlockDisplayBoard.GetComponent<CardUnlockView>()));
            var artifact = ArtifactModel(canvas);
            if (artifact != null)
                b.AddItem(ControlId.Structural(id + "artifact"),
                    CardNodes.Artifact(artifact, canvas.ArtifactUnlockDisplayBoard.GetComponent<ArtifactUnlockView>(), clickable: true));
            if (Misc(canvas) != null)
                b.AddItem(ControlId.Structural(id + "misc"), GameNodes.Text(() => Misc(canvas)));
            foreach (var (name, button) in new[] { ("take", canvas.TakeButton), ("skip", canvas.SkipButton), ("continue", canvas.ContinueButton) })
                if (ViewNodes.IsShown(button))
                    b.AddItem(ControlId.Structural(id + name), ViewNodes.Button(button));
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            var canvas = Canvas;
            if (canvas == null || !ViewNodes.IsShown(canvas.ContinueButton) || ViewNodes.IsShown(canvas.TakeButton)) yield break;
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ViewNodes.Click(Canvas != null ? Canvas.ContinueButton : null));
        }
    }
}
