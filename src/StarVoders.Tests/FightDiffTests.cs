using System.Collections.Generic;
using StarVoders.Core.Fight;
using Xunit;

namespace StarVoders.Tests
{
    /// <summary>
    /// The fight's events as outcomes between two settled moments: one terse line per fact, in a
    /// fixed order (phase, entities, Doom and Heat, cards drawn), a move naming only what changed.
    /// </summary>
    public class FightDiffTests
    {
        private static EntitySnap Migo(int n, int x, int y, int health = 1) =>
            new EntitySnap { Key = "Basic#" + n, Name = "Migo " + n, X = x, Y = y, Health = health };

        private static EntitySnap Shooter(int n, int x, int y) =>
            new EntitySnap { Key = "Shooter#" + n, Name = "Shooter " + n, Kind = "Shooter", Number = n, X = x, Y = y, Health = 1 };

        private static EntitySnap Web(int n, int x, int y) =>
            new EntitySnap { Key = "Web#" + n, Name = "Web", Kind = "Web", X = x, Y = y, Health = 1 };

        [Fact]
        public void The_destroyed_are_grouped_by_kind_numbers_ascending_and_the_unnumbered_counted()
        {
            var before = Snap(Shooter(8, 1, 6), Web(1, 2, 3), Shooter(1, 2, 6), Web(2, 4, 3), Shooter(7, 3, 6), Shooter(2, 4, 6));
            var after = Snap(Shooter(2, 4, 6));
            Assert.Equal(new[] { "Shooter 1, 7, 8 destroyed", "2x Web destroyed" }, FightDiff.Lines(before, after));
        }

        [Fact]
        public void One_alive_off_the_board_left_it_rather_than_being_destroyed()
        {
            var after = Snap(Shooter(2, 2, 6));
            after.OffGrid.Add("Shooter#1");
            after.OffGrid.Add("Shooter#7");
            Assert.Equal(new[] { "Shooter 8 destroyed", "Shooter 1, 7 left the board" },
                FightDiff.Lines(Snap(Shooter(1, 1, 6), Shooter(2, 2, 6), Shooter(7, 3, 6), Shooter(8, 4, 6)), after));
        }

        [Fact]
        public void Those_taken_off_the_board_flew_away_dissipated_or_were_eaten()
        {
            var angel = new EntitySnap { Key = "enemy:FallenAngel#1", Name = "Fallen Angel", Kind = "Fallen Angel", X = 1, Y = 5 };
            var rift = new EntitySnap { Key = "enemy:VoidRift#2", Name = "Void Rift", Kind = "Void Rift", X = 3, Y = 5 };
            var apple = new EntitySnap { Key = "enemy:Apple#4", Name = "Apple 4", Kind = "Apple", Number = 4, X = 2, Y = 5 };
            var after = Snap();
            after.Taken["enemy:FallenAngel#1"] = FightDiff.FlewAway;
            after.Taken["enemy:VoidRift#2"] = FightDiff.Dissipated;
            after.Taken["enemy:Apple#4"] = FightDiff.Eaten;
            Assert.Equal(new[] { "Fallen Angel flew away", "Void Rift dissipated", "Apple 4 eaten" }, FightDiff.Lines(Snap(angel, rift, apple), after));
        }

        [Fact]
        public void One_destroyed_of_a_kind_reads_as_its_name()
        {
            Assert.Equal(new[] { "Shooter 7 destroyed" }, FightDiff.Lines(Snap(Shooter(7, 1, 6), Shooter(2, 2, 6)), Snap(Shooter(2, 2, 6))));
            Assert.Equal(new[] { "Web destroyed" }, FightDiff.Lines(Snap(Web(1, 2, 3)), Snap()));
        }

        [Fact]
        public void Without_a_kind_the_destroyed_group_by_name()
        {
            var a = new EntitySnap { Key = "a", Name = "Crate" };
            var b = new EntitySnap { Key = "b", Name = "Crate" };
            Assert.Equal(new[] { "2x Crate destroyed" }, FightDiff.Lines(Snap(a, b), Snap()));
        }

        private static FightSnapshot Snap(params EntitySnap[] entities)
        {
            var s = new FightSnapshot { Turn = 1, Turns = 8, PlayerTurn = true, Doom = "Doom 0/5" };
            s.Entities.AddRange(entities);
            return s;
        }

        [Fact]
        public void A_boss_off_the_board_reads_its_health_after_the_destroyed_then_a_phase_it_enters()
        {
            var before = Snap(Migo(1, 2, 6));
            before.Boss = "The Overseer 2/6";
            before.BossPhase = "The Overseer, phase 1";
            var after = Snap();
            after.Boss = "The Overseer 1/6";
            after.BossPhase = "The Overseer, phase 1";
            Assert.Equal(new[] { "Migo 1 destroyed", "The Overseer 1/6" }, FightDiff.Lines(before, after));

            // The bar hidden as phase 2 begins: the phase alone, nothing for the bar going.
            var next = Snap();
            next.BossPhase = "The Overseer, phase 2";
            Assert.Equal(new[] { "The Overseer, phase 2" }, FightDiff.Lines(after, next));
            Assert.Empty(FightDiff.Lines(next, next));
        }

        [Fact]
        public void A_named_shield_reads_by_its_name_and_its_loss_as_any_shield()
        {
            var plain = Migo(1, 2, 6);
            var shielded = Migo(1, 2, 6);
            shielded.Shielded = true;
            var explosive = Migo(1, 2, 6);
            explosive.Shielded = true;
            explosive.Shield = "Explosive Shield";
            Assert.Equal(new[] { "Migo 1: Explosive Shield" }, FightDiff.Lines(Snap(plain), Snap(explosive)));
            Assert.Equal(new[] { "Migo 1: Explosive Shield" }, FightDiff.Lines(Snap(shielded), Snap(explosive)));
            Assert.Equal(new[] { "Migo 1 loses its shield" }, FightDiff.Lines(Snap(explosive), Snap(plain)));
            Assert.Equal(new[] { "Migo 1 shielded" }, FightDiff.Lines(Snap(plain), Snap(shielded)));
        }

        [Fact]
        public void Immunity_reads_by_the_game_word_when_gained_and_its_loss_by_ours()
        {
            var plain = Migo(1, 2, 6);
            var immune = Migo(1, 2, 6);
            immune.Immune = "Immune";
            Assert.Equal(new[] { "Migo 1: Immune" }, FightDiff.Lines(Snap(plain), Snap(immune)));
            Assert.Equal(new[] { "Migo 1 loses immunity" }, FightDiff.Lines(Snap(immune), Snap(plain)));
            Assert.Empty(FightDiff.Lines(Snap(immune), Snap(immune)));
        }

        [Fact]
        public void Channelling_reads_instead_of_its_charge_and_its_end_is_said()
        {
            var plain = Migo(1, 2, 6);
            var channel = Migo(1, 2, 6);
            channel.Charge = 1;
            channel.Channel = "channelling 1 Doom";
            Assert.Equal(new[] { "Migo 1: channelling 1 Doom" }, FightDiff.Lines(Snap(plain), Snap(channel)));
            Assert.Equal(new[] { "Migo 1 stops channelling" }, FightDiff.Lines(Snap(channel), Snap(plain)));
            var charged = Migo(1, 2, 6);
            charged.Charge = 2;
            Assert.Equal(new[] { "Migo 1: charge 2" }, FightDiff.Lines(Snap(plain), Snap(charged)));
        }

        [Fact]
        public void A_card_created_into_a_pile_is_said_but_not_one_moving_between_piles()
        {
            var before = Snap();
            before.Hand.Add(new CardSnap { Key = "Wander#1", Name = "Wander" });
            before.Piles.Add(new CardSnap { Key = "Puppet#1", Name = "Puppet", Pile = "Draw Pile" });
            before.Piles.Add(new CardSnap { Key = "Dash#1", Name = "Dash", Pile = null });
            var after = Snap();
            after.Piles.Add(new CardSnap { Key = "Wander#1", Name = "Wander", Pile = "Discard Pile" });
            after.Piles.Add(new CardSnap { Key = "Puppet#1", Name = "Puppet", Pile = "Discard Pile" });
            after.Piles.Add(new CardSnap { Key = "Dash#1", Name = "Dash", Pile = "Discard Pile" });
            after.Piles.Add(new CardSnap { Key = "Poof#1", Name = "Poof!", Pile = "Draw Pile" });
            after.Piles.Add(new CardSnap { Key = "Ghost#1", Name = "Ghost", Pile = null });
            Assert.Equal(new[] { "Poof! added to Draw Pile" }, FightDiff.Lines(before, after));
            // The piles still filling: nothing is new.
            Assert.Empty(FightDiff.Lines(Snap(), after));
        }

        [Fact]
        public void A_boss_panel_reads_when_its_figure_changes_or_it_appears()
        {
            var before = Snap(Migo(1, 2, 6));
            before.Counters.Add(new CardSnap { Key = "hourglass", Name = "The Hourglass 5" });
            var after = Snap(Migo(1, 2, 6));
            after.Counters.Add(new CardSnap { Key = "hourglass", Name = "The Hourglass 4" });
            Assert.Equal(new[] { "The Hourglass 4" }, FightDiff.Lines(before, after));
            Assert.Empty(FightDiff.Lines(after, after));

            var none = Snap(Migo(1, 2, 6));
            Assert.Equal(new[] { "The Hourglass 4" }, FightDiff.Lines(none, after));
            // A first read with nothing on the board yet says nothing.
            var first = Snap();
            first.Counters.Add(new CardSnap { Key = "hourglass", Name = "The Hourglass 5" });
            Assert.Empty(FightDiff.Lines(Snap(), first));
        }

        [Fact]
        public void A_spell_whose_cooldown_runs_out_is_ready()
        {
            var before = Snap();
            before.Spells.Add(new SpellSnap { Key = "1", Name = "Conjure", Recharging = true });
            var after = Snap();
            after.Spells.Add(new SpellSnap { Key = "1", Name = "Conjure" });
            Assert.Equal(new[] { "Conjure ready" }, FightDiff.Lines(before, after));
            Assert.Empty(FightDiff.Lines(after, before));
            Assert.Empty(FightDiff.Lines(Snap(), after));
        }

        [Fact]
        public void Artifacts_gained_or_lost_mid_fight_are_said_but_not_on_the_first_read()
        {
            var empty = Snap();
            var one = Snap();
            one.Artifacts.Add(new CardSnap { Key = "1", Name = "Cosmic Bond" });
            var two = Snap();
            two.Artifacts.Add(new CardSnap { Key = "1", Name = "Cosmic Bond" });
            two.Artifacts.Add(new CardSnap { Key = "2", Name = "Booby Traps" });
            Assert.Empty(FightDiff.Lines(empty, one));
            Assert.Equal(new[] { "Gained Booby Traps" }, FightDiff.Lines(one, two));
            Assert.Equal(new[] { "Lost Booby Traps" }, FightDiff.Lines(two, one));
        }

        [Fact]
        public void A_fight_without_a_turn_limit_says_the_turn_alone()
        {
            var before = Snap(Migo(1, 2, 6));
            before.Turns = -1;
            before.PlayerTurn = false;
            var after = Snap(Migo(1, 2, 6));
            after.Turns = -1;
            after.Turn = 2;
            Assert.Equal(new[] { "Turn 2" }, FightDiff.Lines(before, after));
        }

        [Fact]
        public void A_rising_combo_reads_as_drawn_and_its_reset_says_nothing()
        {
            var none = Snap(Migo(1, 2, 6));
            var two = Snap(Migo(1, 2, 6));
            two.Combo = 2;
            two.ComboText = "Combo! 2";
            var four = Snap(Migo(1, 2, 6));
            four.Combo = 4;
            four.ComboText = "Combo! 4, Nice!";
            Assert.Equal(new[] { "Combo! 2" }, FightDiff.Lines(none, two));
            Assert.Equal(new[] { "Combo! 4, Nice!" }, FightDiff.Lines(two, four));
            Assert.Empty(FightDiff.Lines(four, none));
        }

        [Fact]
        public void The_game_s_own_turn_words_are_said_when_given()
        {
            var before = Snap(Migo(1, 2, 6));
            before.EnemyTurnText = "Invader Turn";
            var after = Snap(Migo(1, 2, 5));
            after.Turn = 2;
            after.TurnText = "Player Turn 1";
            after.EnemyTurnText = "Invader Turn";
            Assert.Equal(new[] { "Invader Turn", "Migo 1 to row 5", "Player Turn 1" }, FightDiff.Lines(before, after));
        }

        [Fact]
        public void Nothing_changed_says_nothing()
        {
            Assert.Empty(FightDiff.Lines(Snap(Migo(1, 2, 6)), Snap(Migo(1, 2, 6))));
        }

        [Fact]
        public void A_move_names_only_the_coordinate_that_changed()
        {
            var lines = FightDiff.Lines(
                Snap(Migo(1, 2, 6), Migo(2, 4, 6), Migo(3, 1, 7)),
                Snap(Migo(1, 2, 5), Migo(2, 3, 6), Migo(3, 2, 5)));
            Assert.Equal(new[] { "Migo 1 to row 5", "Migo 2 to column 3", "Migo 3 to 2 5" }, lines);
        }

        [Fact]
        public void Arrivals_damage_and_deaths_read_as_outcomes()
        {
            var lines = FightDiff.Lines(
                Snap(Migo(1, 2, 6, health: 3), Migo(2, 4, 6)),
                Snap(Migo(1, 2, 6, health: 2), Migo(3, 5, 7)));
            Assert.Equal(new[] { "Migo 1: 2 health", "Migo 3 appears at 5 7", "Migo 2 destroyed" }, lines);
        }

        [Fact]
        public void A_whole_enemy_turn_at_once_reads_enemy_turn_then_what_it_did_then_the_new_turn()
        {
            var before = Snap(Migo(1, 2, 6));
            before.Hand.Add(new CardSnap { Key = "c1", Name = "Fire!" });
            var after = Snap(Migo(1, 2, 5));
            after.Turn = 2;
            after.Hand.Add(new CardSnap { Key = "c2", Name = "Shift" });
            Assert.Equal(new[] { "Enemy turn", "Migo 1 to row 5", "Turn 2/8", "Drew Shift" }, FightDiff.Lines(before, after));
        }

        [Fact]
        public void The_phase_comes_first_and_the_new_turn_reads_x_of_y()
        {
            var enemy = Snap(Migo(1, 2, 6));
            enemy.PlayerTurn = false;
            Assert.Equal(new[] { "Enemy turn", "Migo 1 to row 5" }, FightDiff.Lines(Snap(Migo(1, 2, 6)), Mutate(enemy, 2, 5)));

            var next = Snap(Migo(1, 2, 5));
            next.Turn = 2;
            Assert.Equal(new[] { "Turn 2/8" }, FightDiff.Lines(Mutate(enemy, 2, 5), next));
        }

        private static FightSnapshot Mutate(FightSnapshot s, int x, int y)
        {
            var copy = new FightSnapshot { Turn = s.Turn, Turns = s.Turns, PlayerTurn = s.PlayerTurn, Doom = s.Doom };
            copy.Entities.Add(Migo(1, x, y));
            return copy;
        }

        [Fact]
        public void Doom_reads_as_its_panel_and_the_mech_has_no_health()
        {
            var before = Snap(new EntitySnap { Key = "mech", Name = "mech", X = 3, Y = 2, Health = 1, IsMech = true });
            var after = Snap(new EntitySnap { Key = "mech", Name = "mech", X = 3, Y = 2, Health = 0, IsMech = true });
            after.Doom = "Doom 1/5";
            Assert.Equal(new[] { "Doom 1/5" }, FightDiff.Lines(before, after));
        }

        [Fact]
        public void The_energy_reads_as_its_panels_each_when_it_changes()
        {
            var before = Snap();
            before.Power = "Power 3/3";
            before.Battery = "Battery 2";
            before.Mana = "Mana 2/5";
            var after = Snap();
            after.Power = "Power 2/3";
            after.Battery = "Battery 2";
            after.Mana = "Mana 4/5";
            Assert.Equal(new[] { "Power 2/3", "Mana 4/5" }, FightDiff.Lines(before, after));
        }

        [Fact]
        public void A_pickup_reads_where_it_lands_and_when_lost_but_not_when_drawn()
        {
            var before = Snap();
            before.Pickups.Add(new PickupSnap { Key = "Dash#1", Name = "Pickup: Dash", X = 4, Y = 7 });
            before.Pickups.Add(new PickupSnap { Key = "Sting#1", Name = "Pickup: Sting", X = 4, Y = 6 });
            var after = Snap();
            after.Pickups.Add(new PickupSnap { Key = "Kunai#1", Name = "Pickup: Kunai", X = 2, Y = 5 });
            after.Hand.Add(new CardSnap { Key = "Sting#1", Name = "Sting" });
            Assert.Equal(new[] { "Pickup: Kunai appears at 2 5", "Pickup: Dash destroyed", "Drew Sting" },
                FightDiff.Lines(before, after));
        }

        [Fact]
        public void Cards_drawn_are_the_new_ones_by_id_not_by_name()
        {
            var before = Snap();
            before.Hand.Add(new CardSnap { Key = "c1", Name = "Fire!" });
            var after = Snap();
            after.Hand.AddRange(new List<CardSnap>
            {
                new CardSnap { Key = "c1", Name = "Fire!" },
                new CardSnap { Key = "c2", Name = "Fire!" },
                new CardSnap { Key = "c3", Name = "Shift" },
            });
            Assert.Equal(new[] { "Drew Fire!, Shift" }, FightDiff.Lines(before, after));
        }

        [Fact]
        public void New_danger_zones_are_grouped_by_attacker_and_old_ones_are_not_repeated()
        {
            var before = Snap();
            before.Dangers.Add(new DangerSnap { Who = "Shooter 1", X = 2, Y = 1 });
            var after = Snap();
            after.Dangers.AddRange(new[]
            {
                new DangerSnap { Who = "Shooter 1", X = 2, Y = 1 },
                new DangerSnap { Who = "Shooter 2", X = 4, Y = 1 },
                new DangerSnap { Who = "Shooter 2", X = 4, Y = 2 },
            });
            Assert.Equal(new[] { "Shooter 2 targets 4 1 to 4 2" }, FightDiff.Lines(before, after));
        }
    }
}
