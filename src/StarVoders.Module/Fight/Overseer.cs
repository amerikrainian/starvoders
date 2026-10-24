using System;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Strings;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;

namespace StarVoders.Module.Fight
{
    /// <summary>
    /// The final fight's Overseer (<see cref="ChaosBossModel"/>), which is never on the board: the
    /// game draws it beside the grid. Its first phase's health is the bar it draws
    /// (<see cref="ChaosAct1HealthBarView"/>, fed <see cref="ChaosBossModel.Phase1HP"/> by
    /// <see cref="ChaosBossView.UpdateView"/>; striking an open Hand of Time takes 1), read as drawn; the
    /// bar is hidden in phases 2 and 3, which draw no figure. The phase is the last
    /// <c>ChaosBossTransitionToPhaseNTask</c> the game started in this fight (<see cref="FightEvents"/>
    /// reports them), but 1 whenever the bar shows (a Rewind back into phase 1, a fight resumed, a
    /// module reloaded); unknown in phases 2 and 3 when no transition was seen.
    /// </summary>
    internal static class Overseer
    {
        private static int _phase;
        private static IntPtr _phaseFight;

        /// <summary>The game began the Overseer's phase <paramref name="phase"/> in the current fight.</summary>
        public static void PhaseStarted(int phase)
        {
            _phase = phase;
            _phaseFight = Fight.Controller?.Pointer ?? IntPtr.Zero;
        }

        /// <summary>The 1-based phase a <c>ChaosBossTransitionToPhaseNTask</c> starts, else 0.</summary>
        public static int PhaseOf(ATask task)
        {
            if (task == null) return 0;
            if (task.TryCast<ChaosBossTransitionToPhase1Task>() != null) return 1;
            if (task.TryCast<ChaosBossTransitionToPhase2Task>() != null) return 2;
            if (task.TryCast<ChaosBossTransitionToPhase3Task>() != null) return 3;
            return 0;
        }

        /// <summary>The fight's Overseer, or null in any other fight.</summary>
        public static ChaosBossModel Model()
        {
            var model = Fight.Model;
            if (model == null) return null;
            try
            {
                foreach (var kv in model.ModelItemDict)
                {
                    var boss = kv.Value?.TryCast<ChaosBossModel>();
                    if (boss != null) return boss;
                }
            }
            catch (Exception e) { CoreLog.Warning("Overseer: finding the boss failed: " + e.Message); }
            return null;
        }

        /// <summary>"The Overseer" (its Compendium title, the one the Chaos globe draws), "The
        /// Overseer...?" on the ultimate path.</summary>
        public static string Name()
        {
            bool ultimate = false;
            try { ultimate = DataManager.PlayerData != null && DataManager.PlayerData.IsUltimateQuestDone(); }
            catch (Exception e) { CoreLog.Warning("Overseer: reading the ultimate quest failed: " + e.Message); }
            string name = GameLoc.Get(ultimate ? "ChaosGodUltimateEnding_Misc" : "ChaosGodGlossaryTitle_Misc")
                ?? GameLoc.Get("ChaosGodGlossaryTitle_Misc");
            if (name == null) return "Overseer";
            // A name in capitals ("THE OVERSEER"): each word capitalised, not just the first as GameText.Calm does.
            return GameText.Calm(name) == name ? name
                : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
        }

        // The health bar while it is drawn.
        private static ChaosAct1HealthBarView Bar(ChaosBossModel boss)
        {
            try
            {
                var grid = Fight.View?.GridView;
                if (grid == null || boss == null) return null;
                var bar = grid.GetEntityView(boss.ID.Cast<EntityID>())?.TryCast<ChaosBossView>()?.SplitDimensionController?.ChaosAct1HealthBar;
                return bar != null && bar.gameObject.activeInHierarchy ? bar : null;
            }
            catch (Exception e) { CoreLog.Warning("Overseer: reading the health bar failed: " + e.Message); return null; }
        }

        /// <summary>"5/6" as the bar draws it, null while it is hidden.</summary>
        public static string Health(ChaosBossModel boss)
        {
            var bar = Bar(boss);
            if (bar == null) return null;
            string now = GameText.Of(bar.CurrHealthText), max = GameText.Of(bar.MaxHealthText);
            if (int.TryParse(now, out int n) && int.TryParse(max?.TrimStart('/', ' '), out int m) && m > 0) return Strings.Position(n, m);
            return now;
        }

        // The bar wins: it shows in phase 1 only, which a Rewind can return to after phase 2 began.
        public static int Phase(ChaosBossModel boss)
        {
            if (Bar(boss) != null) return 1;
            var c = Fight.Controller;
            return _phase > 1 && c != null && c.Pointer == _phaseFight ? _phase : 0;
        }

        /// <summary>"The Overseer 5/6" while the bar is drawn, null otherwise (a snapshot's figure).</summary>
        public static string HealthLine()
        {
            var boss = Model();
            string health = boss != null ? Health(boss) : null;
            return health != null ? Name() + " " + health : null;
        }

        /// <summary>"The Overseer, phase 2", null while the phase is unknown.</summary>
        public static string PhaseLine()
        {
            var boss = Model();
            int phase = boss != null ? Phase(boss) : 0;
            return phase > 0 ? Strings.FightBossPhase(Name(), phase) : null;
        }

        /// <summary>The status item and the enemies buffer's first line: its health while drawn, else
        /// its phase, else its name; null in any other fight.</summary>
        public static string Line()
        {
            var boss = Model();
            if (boss == null) return null;
            string health = Health(boss);
            if (health != null) return Name() + " " + health;
            int phase = Phase(boss);
            return phase > 0 ? Strings.FightBossPhase(Name(), phase) : Name();
        }
    }
}
