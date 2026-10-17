using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Module.Screens;
using StarVoders.Module.UI;

namespace StarVoders.Module.Fight
{
    /// <summary>
    /// The lines of the buffers that read the fight whatever has focus: the hand, each card as
    /// arrowing onto it says it (not its more info); the player, Doom, the energy, bombs and Summons,
    /// the mech and Stars, or between fights the run's Doom and Stars; the invaders, each with its
    /// state and tile, top row first. Read live on every buffer keypress; empty where they do not apply.
    /// </summary>
    internal static class FightBuffers
    {
        public static IEnumerable<string> Hand()
        {
            if (Fight.Controller == null) yield break;
            foreach (var card in Fight.Hand())
                yield return GraphAnnouncer.LeafText(new GraphNode { Vtable = EncounterScreen.Card(card) });
        }

        public static IEnumerable<string> Player()
        {
            if (Fight.Controller == null)
            {
                foreach (var line in CampaignNodes.StatusLines()) yield return line;
                yield break;
            }
            if (Fight.Shown(Fight.View?.DoomView)) yield return Fight.Figure("Doom");
            foreach (var line in Fight.Resources()) yield return line;
            yield return Fight.Bombs();
            yield return Fight.Summons();
            yield return FightKeys.MechLine();
            yield return EncounterScreen.Stars();
        }

        // "Shooter 2, 3 health, 4, 7", as the mech key reads the mech.
        public static IEnumerable<string> Enemies()
        {
            if (Fight.Controller == null) yield break;
            var board = FightBoard.Read();
            if (board == null) yield break;
            for (int y = board.Rows - 1; y >= 0; y--)
                for (int x = 0; x < board.Columns; x++)
                {
                    var e = board.At(x, y);
                    if (e?.TryCast<EnemyEntityModel>() != null)
                        yield return board.Describe(e) + ", " + FightBoard.CoordText(x, y);
                }
        }
    }
}
