using System.Collections.Generic;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.UI;
using Screen = StarVadersAccess.Core.Screens.Screen;

namespace StarVadersAccess.Module.Screens
{
    /// <summary>
    /// A tutorial tip that waits for a click ("[key] to continue"), as a modal over whatever scene it
    /// shows in: its text is the one node, Enter continues as the game's click does. Tips that do not
    /// wait are spoken by the popup reader and read in their scene's own screen.
    /// </summary>
    public sealed class TipScreen : Screen
    {
        public override string Key => "tip";
        public override int Layer => 40;
        public override bool Exclusive => true;
        public override bool AllowsTypeahead => false;

        public override bool IsActive()
        {
            var tip = Tips.Current;
            return tip != null && Tips.WaitsForClick(tip) && Tips.Text(tip) != null;
        }

        public override void Build(GraphBuilder b)
        {
            var tip = Tips.Current;
            if (tip == null) return;
            b.AddItem(ControlId.Structural("tip:" + tip.GetInstanceID()), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => Tips.Text(tip)) },
                SearchText = () => Tips.Text(tip),
                OnActivate = () => Tips.Continue(tip),
            });
        }
    }
}
