using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.UI;
using Screen = StarVadersAccess.Core.Screens.Screen;

namespace StarVadersAccess.Module.Screens
{
    /// <summary>
    /// A reward after a fight (<c>CardRewardScene</c>, one <see cref="RewardController"/> for cards,
    /// upgrades and artifacts). Its title ("Pick a Card") names the screen. Stops: the rewards, left
    /// to right, cards "name, cost, description" and artifacts "name, description", Enter taking one
    /// as the game's click does; Skip and Reroll (with the Chrono Tokens left); the run status (Doom,
    /// Stars, Deck); the act timeline. Up until a reward is taken or skipped
    /// (<see cref="RewardController.IsEndingScene"/>).
    /// </summary>
    public sealed class RewardScreen : Screen
    {
        public override string Key => "reward";
        public override object InitialFocusStop => "reward:choices";

        private static RewardController Controller => GameScenes.Controller<RewardController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        public override string ScreenName => GameText.Of(Controller?.TitleText);

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;

            b.BeginStop("reward:choices");
            b.PushContext(null);
            var cards = new List<CardRewardBoard>();
            if (c.CardRewardBoards != null)
                foreach (var board in c.CardRewardBoards)
                    if (ViewNodes.IsShown(board)) cards.Add(board);
            cards.Sort((x, y) => x.transform.position.x.CompareTo(y.transform.position.x));
            foreach (var board in cards)
                b.AddItem(ControlId.Structural("reward:card:" + board.GetInstanceID()), CardNodes.Card(board.GetCardModel(), board));
            var artifacts = new List<ArtifactRewardBoard>();
            if (c.ArtifactRewardBoards != null)
                foreach (var board in c.ArtifactRewardBoards)
                    if (ViewNodes.IsShown(board)) artifacts.Add(board);
            artifacts.Sort((x, y) => x.transform.position.x.CompareTo(y.transform.position.x));
            foreach (var board in artifacts)
                b.AddItem(ControlId.Structural("reward:artifact:" + board.GetInstanceID()),
                    CardNodes.Artifact(board.GetArtifactModel(), board, clickable: true));
            b.PopContext();

            b.BeginStop("reward:actions");
            b.PushContext(null, positions: false);
            var skip = c.ProceedButton;
            if (ViewNodes.IsShown(skip))
                b.AddItem(ControlId.Structural("reward:skip"), ViewNodes.Button(skip, enabled: () => skip.ButtonEnabled));
            CampaignNodes.Reroll(b, c.RerollButton, "reward:reroll");
            b.PopContext();

            CampaignNodes.Status(b, c, "reward");
            CampaignNodes.Map(b, c, "reward");
        }
    }
}
