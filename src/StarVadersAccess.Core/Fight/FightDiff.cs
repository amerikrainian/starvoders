using System.Collections.Generic;
using S = StarVadersAccess.Core.Strings.Strings;

namespace StarVadersAccess.Core.Fight
{
    /// <summary>
    /// What changed between two settled moments of a fight, worded as outcomes (speechchem's way):
    /// "Enemy turn" first when it began, then per entity: arrived, moved (only the coordinate that
    /// changed: "Migo 1 to row 4"), health, shield, shock, charge, destroyed; new Danger Zones per
    /// attacker; then Doom and Heat as their panels read; then the new turn ("Turn 2/8") and the
    /// cards drawn. Terse, one line per fact, in that
    /// order. Engine-free: the module takes the snapshots.
    /// </summary>
    public static class FightDiff
    {
        public static List<string> Lines(FightSnapshot before, FightSnapshot after)
        {
            var lines = new List<string>();
            if (before == null || after == null) return lines;

            // The enemy turn opens what it did (often all of it arrives at once, the game busy from
            // the end of the player's turn to the start of the next); the new turn follows it, then
            // the cards it dealt.
            bool turnAdvanced = after.Turn != before.Turn;
            if (before.PlayerTurn && (!after.PlayerTurn || turnAdvanced)) lines.Add(S.FightEnemyTurn);

            var old = new Dictionary<string, EntitySnap>();
            foreach (var e in before.Entities) old[e.Key] = e;
            var seen = new HashSet<string>();
            foreach (var e in after.Entities)
            {
                seen.Add(e.Key);
                if (!old.TryGetValue(e.Key, out var was))
                {
                    lines.Add(S.FightAppears(e.Name, S.FightCoord(e.X, e.Y)));
                    continue;
                }
                if (was.X != e.X && was.Y != e.Y) lines.Add(S.FightMoves(e.Name, S.FightCoord(e.X, e.Y)));
                else if (was.Y != e.Y) lines.Add(S.FightMovesRow(e.Name, e.Y));
                else if (was.X != e.X) lines.Add(S.FightMovesColumn(e.Name, e.X));
                if (!e.IsMech && was.Health != e.Health) lines.Add(S.FightLabelled(e.Name, S.FightHealth(e.Health)));
                // A named shield (an Explosive Shield) reads by its name: "Migo 1: Explosive Shield".
                if (was.Shielded != e.Shielded || (e.Shielded && was.Shield != e.Shield))
                    lines.Add(!e.Shielded ? S.FightUnshielded(e.Name)
                        : e.Shield != null ? S.FightLabelled(e.Name, e.Shield) : S.FightShielded(e.Name));
                if (was.Shocked != e.Shocked) lines.Add(e.Shocked ? S.FightShocked(e.Name) : S.FightUnshocked(e.Name));
                if (was.Charge != e.Charge) lines.Add(S.FightLabelled(e.Name, S.FightCharge(e.Charge)));
            }
            foreach (var e in before.Entities)
                if (!seen.Contains(e.Key)) lines.Add(S.FightDestroyed(e.Name));

            // New Danger Zones, per attacker in the order they first appear, their tiles merged into
            // shapes (TileRanges): "Shooter 1 targets 2, 1 to 2, 5". Zones that clear are not said:
            // the strike or its cancelling is.
            var known = new HashSet<string>();
            foreach (var d in before.Dangers) known.Add(d.Who + "@" + d.X + "," + d.Y);
            var byWho = new List<KeyValuePair<string, List<(int X, int Y)>>>();
            foreach (var d in after.Dangers)
            {
                if (known.Contains(d.Who + "@" + d.X + "," + d.Y)) continue;
                var tiles = byWho.Find(kv => kv.Key == d.Who).Value;
                if (tiles == null) { tiles = new List<(int X, int Y)>(); byWho.Add(new KeyValuePair<string, List<(int X, int Y)>>(d.Who, tiles)); }
                if (!tiles.Contains((d.X, d.Y))) tiles.Add((d.X, d.Y));
            }
            foreach (var kv in byWho)
                lines.Add(S.FightTargets(kv.Key, string.Join("; ", TileRanges.Parts(kv.Value, after.Columns, after.Rows))));

            if (after.Doom != null && after.Doom != before.Doom) lines.Add(after.Doom);
            if (after.Heat != null && after.Heat != before.Heat) lines.Add(after.Heat);
            if (after.PlayerTurn && (!before.PlayerTurn || turnAdvanced)) lines.Add(S.FightTurn(after.Turn, after.Turns));

            var held = new HashSet<string>();
            foreach (var c in before.Hand) held.Add(c.Key);
            var drawn = new List<string>();
            foreach (var c in after.Hand)
                if (!held.Contains(c.Key)) drawn.Add(c.Name);
            if (drawn.Count > 0) lines.Add(S.FightDrew(string.Join(", ", drawn)));
            return lines;
        }
    }
}
