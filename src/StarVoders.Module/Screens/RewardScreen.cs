using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// A reward after a fight (<c>CardRewardScene</c>, one <see cref="RewardController"/> for cards,
    /// upgrades and artifacts). Its title ("Pick a Card") names the screen. One stop: the act
    /// timeline and the run status (Doom, Stars, Deck) as strips, then the rewards (the landing), left
    /// to right, cards "name, cost, description" and artifacts "name, description", Enter taking one
    /// as the game's click does, then Skip and Reroll (with the Chrono Tokens left). Up until a reward is taken or skipped
    /// (<see cref="RewardController.IsEndingScene"/>).
    /// </summary>
    public sealed class RewardScreen : Screen
    {
        public override string Key => "reward";
        public override IReadOnlyList<InputCategory> InputCategories => RunKeys.Categories;

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
            CampaignNodes.Strips(b, c, "reward");
            b.PushContext(null);
            var cards = new List<CardRewardBoard>();
            if (c.CardRewardBoards != null)
                foreach (var board in c.CardRewardBoards)
                    if (ViewNodes.IsShown(board)) cards.Add(board);
            cards.Sort((x, y) => x.transform.position.x.CompareTo(y.transform.position.x));
            var artifacts = new List<ArtifactRewardBoard>();
            if (c.ArtifactRewardBoards != null)
                foreach (var board in c.ArtifactRewardBoards)
                    if (ViewNodes.IsShown(board)) artifacts.Add(board);
            artifacts.Sort((x, y) => x.transform.position.x.CompareTo(y.transform.position.x));
            bool both = cards.Count > 0 && artifacts.Count > 0;
            // The cards as one strip, the artifacts as another under them; labelled when both show.
            if (cards.Count > 0)
            {
                b.PushContext(CardNodes.CardsLabel(both));
                b.StartRow("reward:cards");
                foreach (var board in cards)
                    b.AddItem(ControlId.Structural("reward:card:" + board.GetInstanceID()), CardNodes.Card(board.GetCardModel(), board));
                b.EndRow();
                b.PopContext();
            }
            if (artifacts.Count > 0)
            {
                b.PushContext(CardNodes.ArtifactsLabel(both));
                b.StartRow("reward:artifacts");
                foreach (var board in artifacts)
                    b.AddItem(ControlId.Structural("reward:artifact:" + board.GetInstanceID()),
                        CardNodes.Artifact(board.GetArtifactModel(), board, clickable: true));
                b.EndRow();
                b.PopContext();
            }
            b.PopContext();
            // The landing: the first reward, under the run's strips.
            if (cards.Count > 0) b.SetStart(ControlId.Structural("reward:card:" + cards[0].GetInstanceID()));
            else if (artifacts.Count > 0) b.SetStart(ControlId.Structural("reward:artifact:" + artifacts[0].GetInstanceID()));

            // Skip and Reroll answer the same choice: they follow the rewards in the stop, unnumbered.
            b.PushContext(null, positions: false);
            var skip = c.ProceedButton;
            if (ViewNodes.IsShown(skip))
                b.AddItem(ControlId.Structural("reward:skip"), ViewNodes.Button(skip, enabled: () => skip.ButtonEnabled));
            CampaignNodes.Reroll(b, c.RerollButton, "reward:reroll");
            b.PopContext();
        }
    }
}
