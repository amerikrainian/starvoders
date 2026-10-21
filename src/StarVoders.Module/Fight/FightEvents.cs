using System;
using System.Collections.Generic;
using HarmonyLib;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Fight;
using StarVoders.Module.Interop;
using StarVoders.Module.Screens;
using StarVoders.Module.UI;
using UnityEngine;

namespace StarVoders.Module.Fight
{
    /// <summary>
    /// The fight's events: the board, Doom, Heat, the turn and the hand are snapshotted whenever the
    /// game settles (its task engine idle for a few frames, or waiting on the player), and what changed
    /// since the last settled snapshot (<see cref="FightDiff"/>) is spoken, an entry per event, queued so it never cuts
    /// off what is being said and nothing the mod says on its own cuts it off, and appended to the
    /// combat log (<see cref="Log"/>, the combat buffer). The log is uncapped and cleared when a new
    /// fight starts. Polled from the module tick; snapshots are plain data, nothing native is kept.
    /// End Turn to the next turn is one busy stretch (the end of the turn, the invaders' phase, the
    /// next turn's start) with nothing settling, so <see cref="Phases"/> adds snapshots inside it: as
    /// the invaders' phase starts, before each invader's action, and as the player's turn starts,
    /// before it resets the combo.
    /// </summary>
    [HarmonyPatch]
    internal static class FightEvents
    {
        /// <summary>Every event line of the current fight, oldest first.</summary>
        public static readonly List<string> Log = new List<string>();

        // "Combo! 4, Nice!": the combo view's caption and count (it shows any count above 0), and
        // the praise line (ComboText_IntensityN_Misc) while it is up.
        private static string ComboText(int combo, ComboView view)
        {
            if (combo <= 0) return null;
            string text = (GameLoc.Get("Combo_Misc") ?? "Combo!") + " " + combo;
            try
            {
                if (view != null && view.IsAmazingShowing)
                {
                    string praise = GameText.Of(view.AmazingText);
                    if (praise != null) text += ", " + praise;
                }
            }
            catch (Exception e) { CoreLog.Warning("FightEvents: reading the combo's praise failed: " + e.Message); }
            return text;
        }

        private const int SettleFrames = 3;
        private static IntPtr _fight;
        private static FightSnapshot _settled;
        private static int _idle;

        /// <summary>Whether the fight settled this frame (see <see cref="Tick"/>): nothing moving, or
        /// waiting on the player.</summary>
        public static bool Settled { get; private set; }

        public static void Tick()
        {
            Settled = false;
            var c = Fight.Controller;
            if (c == null || c.EncounterEnded)
            {
                _settled = null;
                return;
            }
            if (c.Pointer != _fight)
            {
                _fight = c.Pointer;
                Log.Clear();
                _settled = null;
            }

            // Settled: the task engine idle for a few frames, or waiting on the player (a target to
            // choose, a tip to click), when the engine counts as busy but nothing moves. Waiting
            // counts at once, so what the last turn did is heard before the target list, never held
            // back until the player's next card has resolved.
            bool waiting = Fight.Selecting || WaitingOnTip();
            if (!waiting)
            {
                bool busy;
                try { busy = c.TaskEngine == null || c.TaskEngine.IsBusy(); }
                catch { busy = true; }
                if (busy) { _idle = 0; return; }
                if (++_idle < SettleFrames) return;
            }
            Settled = true;

            var now = Take();
            if (now == null) return;
            if (_settled == null) { _settled = now; return; }
            Publish(now);
        }

        // Speak and log what changed since the last snapshot, then keep this one.
        private static void Publish(FightSnapshot now)
        {
            var lines = FightDiff.Lines(_settled, now);
            _settled = now;
            if (lines.Count == 0) return;
            Log.AddRange(lines);
            // One speech entry per event, all queued, in order.
            foreach (var line in lines) Speech.Say(line);
        }

        // The game logs each task as it starts it (TaskEngine.LogTaskExecution, from ProcessTask):
        // the phase boundaries of the busy stretch after End Turn, and each invader's action within
        // it. What the end of the player's turn did (end-of-turn strikes, their combo) is said before
        // the invaders' turn; each invader's action in the order it happens (a Broodmother's charge
        // spent beside the Tick it spawns, a charge built and spent in one turn); the combo before
        // StartTurnTask sets it back to 0. The phase
        // is the task's, not the model's: the model leaves the player's turn at the discard, before
        // the end-of-turn effects are over.
        [HarmonyPatch(typeof(TaskEngine), nameof(TaskEngine.LogTaskExecution))]
        [HarmonyPrefix]
        private static void Phases(TaskEngine __instance, ATask task)
        {
            try
            {
                if (_settled == null || task == null || __instance.IsPreviewMode) return;
                var c = Fight.Controller;
                if (c == null || c.EncounterEnded || c.TaskEngine == null || c.TaskEngine.Pointer != __instance.Pointer) return;
                // Overheating (a card pushing Heat past the Heat sink burns it and ends the turn): the
                // game's floating "Overheat!", after what the card did so far.
                if (task.TryCast<OverheatTask>() != null)
                {
                    var before = Take();
                    if (before != null) Publish(before);
                    string overheat = GameLoc.Get("FloatTextOverheat_Misc");
                    if (overheat != null) { Speech.Say(overheat); Log.Add(overheat); }
                    return;
                }
                bool enemyPhase = task.TryCast<EnemyPhaseTask>() != null;
                bool action = task.TryCast<ProcessEnemyPhaseActionTask>() != null || task.TryCast<ProcessEnemyStartTurnActionTask>() != null;
                if (!enemyPhase && !action && task.TryCast<StartTurnTask>() == null) return;
                var now = Take();
                if (now == null) return;
                now.PlayerTurn = enemyPhase; // still the player's until the invaders' phase starts; an invader's action is theirs
                Publish(now);
            }
            catch (Exception e) { CoreLog.Warning("FightEvents: a phase snapshot failed: " + e.Message); }
        }

        private static bool WaitingOnTip()
        {
            var tip = UI.Tips.Current;
            return tip != null && UI.Tips.WaitsForClick(tip);
        }

        private static FightSnapshot Take()
        {
            var model = Fight.Model;
            var board = FightBoard.Read();
            var view = Fight.View;
            if (model == null || board == null || view == null) return null;
            try
            {
                var snap = new FightSnapshot
                {
                    Turn = model.TurnNumber,
                    Turns = model.MaxTurns,
                    PlayerTurn = model.IsPlayerTurn,
                    TurnText = EncounterScreen.TurnText(),
                    EnemyTurnText = GameLoc.Get("OverlayInvaderTurn_Misc"),
                };
                // The model's figures, not the panels': hovering End Turn or a card previews them.
                var doom = view.DoomView;
                if (doom != null && doom.gameObject.activeInHierarchy) snap.Doom = Fight.Figure("Doom");
                snap.Combo = Fight.Value(EncounterValue.Combo) ?? 0;
                snap.ComboText = ComboText(snap.Combo, view.ComboView);
                if (Fight.Shown(view.HeatView)) snap.Heat = Fight.Figure("Heat");
                if (Fight.Shown(view.PowerView)) { snap.Power = Fight.Figure("Power"); snap.Battery = Fight.Battery(); }
                if (Fight.Shown(view.ManaView)) snap.Mana = Fight.Figure("Mana");
                foreach (var entity in board.Entities)
                {
                    var c = model.GridModel.GetEntityCoord(entity.ID.Cast<EntityID>(), false);
                    var kind = board.Kind(entity);
                    snap.Entities.Add(new EntitySnap
                    {
                        Key = Key(entity),
                        Name = board.Name(entity),
                        Kind = kind.Kind,
                        Number = kind.Number,
                        X = c.x + 1,
                        Y = c.y + 1,
                        Health = entity.Health,
                        Shielded = entity.IsShielded,
                        Shield = FightBoard.ShieldName(entity),
                        Immune = FightBoard.ImmuneName(entity),
                        Shocked = entity.IsShocked,
                        Charge = entity.Charge,
                        Channel = FightBoard.ChannelText(entity),
                        IsMech = entity.TryCast<PlayerEntityModel>() != null,
                    });
                }
                snap.Columns = board.Columns;
                snap.Rows = board.Rows;
                for (int y = 0; y < board.Rows; y++)
                    for (int x = 0; x < board.Columns; x++)
                        foreach (var who in board.Attackers(x, y))
                            snap.Dangers.Add(new DangerSnap { Who = who, X = x + 1, Y = y + 1 });
                for (int y = 0; y < board.Rows; y++)
                    for (int x = 0; x < board.Columns; x++)
                    {
                        var pickup = board.PickupCard(x, y).Card;
                        if (pickup != null)
                            snap.Pickups.Add(new PickupSnap { Key = pickup.CardID.CardName + "#" + pickup.CardID.Number, Name = board.Pickup(x, y), X = x + 1, Y = y + 1 });
                    }
                // The artifacts and spells the fight shows, by the game's id.
                var artifacts = view.ArtifactViewDict;
                if (artifacts != null)
                    foreach (var kv in artifacts)
                    {
                        var art = kv.Value?.ArtifactModel;
                        if (art == null || art.Destroyed) continue;
                        snap.Artifacts.Add(new CardSnap { Key = kv.Value.Pointer.ToString(), Name = GameLoc.ArtifactName(art.ArtifactName) ?? art.ArtifactName.ToString() });
                    }

                // The hand pile as the model has it: a card being played is still in it, so choosing
                // its target and cancelling reads as nothing.
                var play = view.CardPlayView;
                foreach (var id in model.CardPlayModel.GetPile(Pile.Hand))
                {
                    var card = play?.GetCardView(id)?.CardModel;
                    if (card == null) continue;
                    snap.Hand.Add(new CardSnap { Key = card.CardID.CardName + "#" + card.CardID.Number, Name = Fight.CardName(card) });
                }
                return snap;
            }
            catch (Exception e)
            {
                CoreLog.Warning("FightEvents: taking a snapshot failed: " + e.Message);
                return null;
            }
        }

        // The entity's id for the fight: its kind and the game's number ("Basic#2"), the mech's own.
        private static string Key(AEntityModel entity)
        {
            var enemy = entity.TryCast<EnemyEntityModel>();
            if (enemy != null) return "enemy:" + enemy.EnemyID.EnemyName + "#" + enemy.EnemyID.Number;
            var item = entity.TryCast<ItemEntityModel>();
            if (item != null) return "item:" + item.ItemID.ItemName + "#" + item.ItemID.Number;
            if (entity.TryCast<PlayerEntityModel>() != null) return "mech";
            return entity.Pointer.ToString();
        }
    }
}
