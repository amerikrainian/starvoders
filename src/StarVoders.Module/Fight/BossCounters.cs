using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;

namespace StarVoders.Module.Fight
{
    /// <summary>
    /// The panels some bosses draw beside the board, each made by its boss's view at runtime: the
    /// Grandmaster's hourglass (<see cref="GrandmasterView.HourglassInstance"/>, its number the
    /// Grandmaster's charge: the turns before it channels Panic), the Hive's timer
    /// (<see cref="QueenBeeCoreView.CounterInstance"/>: pips filling as time runs out, "The Hive 1/4")
    /// and the Hemophage's health bar
    /// (<see cref="ClonerView.HealthbarInstance"/>, its health over the maximum it splits off). Each is
    /// read as its title and drawn figure ("The Hourglass 5", "Hemophage 12/20"), its hover panel's
    /// text as the description (the Hive's "[#]" is the Queen Bees left, as the game fills it).
    /// </summary>
    internal static class BossCounters
    {
        public sealed class Counter
        {
            public string Key, Title, Value, Description;
            /// <summary>The value is words ("3 rows above the board"), set apart by a comma, not a figure.</summary>
            public bool Phrase;
            public string Line => Value == null ? Title : Title + (Phrase ? ", " : " ") + Value;
        }

        /// <summary>Whether the entity's health changes are the panel's to say ("Hemophage 11/25", with
        /// its maximum), not the board's: the Hemophage.</summary>
        public static bool ReadsHealth(AEntityModel entity)
        {
            var enemy = entity?.TryCast<EnemyEntityModel>();
            return enemy != null && enemy.EnemyID.EnemyName == EnemyName.Cloner;
        }

        public static List<Counter> Read()
        {
            var counters = new List<Counter>();
            var model = Fight.Model;
            var grid = Fight.View?.GridView;
            if (model == null || grid == null) return counters;
            try
            {
                foreach (var kv in model.ModelItemDict)
                {
                    var kraken = kv.Value?.TryCast<KrakenCoreModel>();
                    if (kraken != null)
                    {
                        var heart = KrakenHeart(kraken, model);
                        if (heart != null) counters.Add(heart);
                        continue;
                    }
                    var enemy = kv.Value?.TryCast<EnemyEntityModel>();
                    if (enemy == null) continue;
                    var name = enemy.EnemyID.EnemyName;
                    if (name != EnemyName.Grandmaster && name != EnemyName.QueenBeeCore && name != EnemyName.Cloner) continue;
                    var view = grid.GetEntityView(enemy.ID.Cast<EntityID>());
                    if (view == null) continue;
                    var counter = name == EnemyName.Grandmaster ? Hourglass(view.TryCast<GrandmasterView>())
                        : name == EnemyName.QueenBeeCore ? Hive(view.TryCast<QueenBeeCoreView>(), model)
                        : Hemophage(view.TryCast<ClonerView>());
                    if (counter != null) counters.Add(counter);
                }
            }
            catch (Exception e) { CoreLog.Warning("BossCounters: reading the boss panels failed: " + e.Message); }
            return counters;
        }

        // The Kraken Heart while it is drawn above the board (its body coming down a row each turn, at
        // KrakenCoreModel.TheoreticalPosition): how far above the top row it is.
        private static Counter KrakenHeart(KrakenCoreModel heart, EncounterModel model)
        {
            if (model.GridModel.IsEntityOnGrid(heart.ID.Cast<EntityID>())) return null;
            int above = heart.TheoreticalPosition.y - (model.GridModel.GridShape.y - 1);
            if (above <= 0) return null;
            return new Counter
            {
                Key = "kraken",
                Title = GameLoc.EnemyName(EnemyName.KrakenCore) ?? "Kraken Heart",
                Value = Core.Strings.Strings.FightAbove(above),
                Phrase = true,
                Description = GameLoc.Get("KrakenCore_EntityDesc"),
            };
        }

        private static Counter Hourglass(GrandmasterView view)
        {
            var h = view?.HourglassInstance;
            if (h == null || !h.gameObject.activeInHierarchy) return null;
            return new Counter
            {
                Key = "hourglass",
                Title = GameLoc.Get("GrandmasterHourglassTitle_Misc") ?? "Hourglass",
                Value = h.NumberText != null && h.NumberText.gameObject.activeInHierarchy ? GameText.Of(h.NumberText) : null,
                Description = GameLoc.Get("GrandmasterHourglassDesc_Misc"),
            };
        }

        private static Counter Hive(QueenBeeCoreView view, EncounterModel model)
        {
            var h = view?.CounterInstance;
            if (h == null || !h.gameObject.activeInHierarchy) return null;
            string description = GameLoc.Get("QueenBeeCounterDesc_Misc");
            if (description != null)
            {
                int left = model.GridModel.GetAllEnemiesWithName(EnemyName.QueenBee)?.Count ?? 0;
                description = description.Replace("[#]", left.ToString());
            }
            // The timer is drawn as pips that fill as time runs out (shown: enabled and scaled up), its
            // number text hidden; full, a Queen Bee channels Panic.
            string value = null;
            var timer = h._currHoneyTimer;
            if (timer != null && timer._countImages != null && timer._countImages.Count > 0)
            {
                int filled = 0;
                foreach (var pip in timer._countImages)
                    if (pip != null && pip.enabled && pip.gameObject.activeInHierarchy && pip.transform.localScale.x > 0.01f) filled++;
                value = Core.Strings.Strings.Position(filled, timer._countImages.Count);
            }
            return new Counter
            {
                Key = "hive",
                Title = GameLoc.Get("QueenBeeCounterTitle_Misc") ?? "Hive",
                Value = value,
                Description = description,
            };
        }

        private static Counter Hemophage(ClonerView view)
        {
            var h = view?.HealthbarInstance;
            if (h == null || !h.gameObject.activeInHierarchy) return null;
            string now = GameText.Of(h.CurrHealthText), max = GameText.Of(h.MaxHealthText);
            string value = int.TryParse(now, out int n) && int.TryParse(max?.TrimStart('/', ' '), out int m) && m > 0
                ? Core.Strings.Strings.Position(n, m) : now;
            return new Counter
            {
                Key = "hemophage",
                Title = GameLoc.Get("ClonerHealthbarTitle_Misc") ?? "Hemophage",
                Value = value,
                Description = GameLoc.Get("ClonerHealthbarDesc_Misc"),
            };
        }
    }
}
