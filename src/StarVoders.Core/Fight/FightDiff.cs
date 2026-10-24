using System.Collections.Generic;
using S = StarVoders.Core.Strings.Strings;

namespace StarVoders.Core.Fight
{
    /// <summary>
    /// What changed between two settled moments of a fight, worded as outcomes (speechchem's way):
    /// "Enemy turn" first when it began, then per entity: arrived, moved (only the coordinate that
    /// changed: "Migo 1 to row 4"), health, shield, immunity, shock, charge; the destroyed, grouped by kind; a boss off the
    /// board, its health and a phase it enters; new Danger Zones per
    /// attacker; then the cards that land on the grid as pickups and those lost from it without being drawn
    /// (a drawn one is the "Drew" line); then Doom and the energy (Heat, Power, Battery, Mana) as their panels read; then the new turn ("Turn 2/8") and the
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
            if (before.PlayerTurn && (!after.PlayerTurn || turnAdvanced)) lines.Add(after.EnemyTurnText ?? S.FightEnemyTurn);

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
                // Immunity (the Mothership's comes and goes): "Mothership: Immune".
                if ((was.Immune != null) != (e.Immune != null))
                    lines.Add(e.Immune != null ? S.FightLabelled(e.Name, e.Immune) : S.FightUnimmune(e.Name));
                if (was.Shocked != e.Shocked) lines.Add(e.Shocked ? S.FightShocked(e.Name) : S.FightUnshocked(e.Name));
                // Channelling Doom (its amount is the charge): "Martian 6: channelling 1 Doom"; a plain
                // charge only while neither channels.
                if (was.Channel != e.Channel)
                    lines.Add(e.Channel != null ? S.FightLabelled(e.Name, e.Channel) : S.FightUnchannelled(e.Name));
                else if (e.Channel == null && was.Charge != e.Charge) lines.Add(S.FightLabelled(e.Name, S.FightCharge(e.Charge)));
            }
            var gone = new List<EntitySnap>();
            foreach (var e in before.Entities)
                if (!seen.Contains(e.Key)) gone.Add(e);
            lines.AddRange(Destroyed(gone));

            // A boss off the board: its health as drawn ("The Overseer 5/6"), then a phase it enters.
            if (after.Boss != null && after.Boss != before.Boss) lines.Add(after.Boss);
            if (after.BossPhase != null && after.BossPhase != before.BossPhase) lines.Add(after.BossPhase);

            // New Danger Zones, per attacker in the order they first appear, their tiles merged into
            // shapes (TileRanges): "Shooter 1 targets 2 1 to 2 5". Zones that clear are not said:
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

            var inHand = new HashSet<string>();
            foreach (var c in after.Hand) inHand.Add(c.Key);
            foreach (var p in after.Pickups)
                if (!before.Pickups.Exists(q => q.Key == p.Key && q.X == p.X && q.Y == p.Y))
                    lines.Add(S.FightAppears(p.Name, S.FightCoord(p.X, p.Y)));
            foreach (var p in before.Pickups)
                if (!after.Pickups.Exists(q => q.Key == p.Key) && !inHand.Contains(p.Key))
                    lines.Add(S.FightDestroyed(p.Name));

            // Artifacts gained or lost mid-fight; a first snapshot with none (the panel still filling)
            // gains nothing.
            if (before.Artifacts.Count > 0)
            {
                foreach (var a in after.Artifacts)
                    if (!before.Artifacts.Exists(b => b.Key == a.Key)) lines.Add(S.FightArtifactGained(a.Name));
                foreach (var a in before.Artifacts)
                    if (!after.Artifacts.Exists(b => b.Key == a.Key)) lines.Add(S.FightArtifactLost(a.Name));
            }

            if (after.Combo > before.Combo && after.ComboText != null) lines.Add(after.ComboText);
            if (after.Doom != null && after.Doom != before.Doom) lines.Add(after.Doom);
            foreach (var (was, now) in new[] { (before.Heat, after.Heat), (before.Power, after.Power), (before.Battery, after.Battery), (before.Mana, after.Mana) })
                if (now != null && now != was) lines.Add(now);
            if (after.PlayerTurn && (!before.PlayerTurn || turnAdvanced)) lines.Add(after.TurnText ?? S.FightTurn(after.Turn, after.Turns));

            var held = new HashSet<string>();
            foreach (var c in before.Hand) held.Add(c.Key);
            var drawn = new List<string>();
            foreach (var c in after.Hand)
                if (!held.Contains(c.Key)) drawn.Add(c.Name);
            if (drawn.Count > 0) lines.Add(S.FightDrew(string.Join(", ", drawn)));
            return lines;
        }

        // The destroyed, one line per kind in the order the kinds first fall: the numbered by their
        // numbers, ascending ("Shooter 1, 7, 8 destroyed"), the unnumbered counted ("2x Web
        // destroyed"); one alone reads as before ("Shooter 7 destroyed", "Web destroyed").
        private static List<string> Destroyed(List<EntitySnap> gone)
        {
            var kinds = new List<string>();
            var numbers = new Dictionary<string, List<int>>();
            var plain = new Dictionary<string, int>();
            foreach (var e in gone)
            {
                string kind = e.Kind ?? e.Name;
                if (!numbers.ContainsKey(kind))
                {
                    kinds.Add(kind);
                    numbers[kind] = new List<int>();
                    plain[kind] = 0;
                }
                if (e.Kind != null && e.Number > 0) numbers[kind].Add(e.Number);
                else plain[kind]++;
            }
            var lines = new List<string>();
            foreach (var kind in kinds)
            {
                var parts = new List<string>();
                var nums = numbers[kind];
                if (nums.Count > 0)
                {
                    nums.Sort();
                    parts.Add(kind + " " + string.Join(", ", nums));
                }
                if (plain[kind] == 1) parts.Add(kind);
                else if (plain[kind] > 1) parts.Add(S.FightCount(plain[kind], kind));
                lines.Add(S.FightDestroyed(string.Join(", ", parts)));
            }
            return lines;
        }
    }
}
