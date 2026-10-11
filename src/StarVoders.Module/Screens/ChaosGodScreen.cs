using System;
using System.Collections.Generic;
using HarmonyLib;
using PixelCrushers.DialogueSystem;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The Chaos God after a run (<c>ChaosGodScene</c>, <see cref="ChaosGodController"/>) once its
    /// conversation (<see cref="DialogueScreen"/>) is over: the unlock panel (<c>UnlockCanvas</c>,
    /// faded in by <c>ShowCampaignUnlocks</c>) as a dialog named by its title bar ("Timeline
    /// Divergence"), its shown <see cref="ChaosUnlock"/>s, then Proceed (<c>ProceedButton</c>,
    /// <c>OnProceedPressed</c>: the panel's one button, which leaves the scene). Up while the panel or
    /// Proceed shows.
    ///
    /// An unlock draws its hover text ("New Act 1 Invader") and a picture of what it unlocks; the
    /// picture is read as its name ("New Act 1 Invader: Broodmother"). The game hands each icon one
    /// invader (<c>InitializeEnemy</c>) or, without one, one room (<c>InitializeRoom</c>: a boss or a
    /// special encounter) and keeps only the sprite, so <see cref="Names"/> records the name as the
    /// icon is set up.
    /// </summary>
    public sealed class ChaosGodScreen : Screen
    {
        public override string Key => "chaosgod";
        public override int Layer => 10;
        public override bool Exclusive => true;

        private const float Shown = 0.5f;

        private static ChaosGodController Controller => GameScenes.Controller<ChaosGodController>();

        public override bool IsActive()
        {
            var c = Controller;
            if (c == null) return false;
            try
            {
                if (DialogueManager.isConversationActive) return false;
                return PanelShown(c) || ViewNodes.IsShown(c.ProceedButton);
            }
            catch (Exception e)
            {
                CoreLog.Warning("ChaosGod: reading the unlock panel failed: " + e.Message);
                return false;
            }
        }

        private static bool PanelShown(ChaosGodController c)
            => c.UnlockCanvas != null && c.UnlockCanvas.gameObject.activeInHierarchy && c.UnlockCanvas.alpha >= Shown;

        private static string Title(ChaosGodController c)
        {
            if (c == null || c.UnlockCanvas == null) return null;
            var title = c.UnlockCanvas.transform.Find("Screen/TitleBar");
            return title != null ? GameText.Of(title.GetComponentInChildren<TMP_Text>()) : null;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;
            b.PushContext(PanelShown(c) ? Title(c) : null, Strings.RoleDialog, positions: false);
            if (PanelShown(c) && c.ChaosUnlocks != null)
            {
                // The list runs middle, left, right; read as drawn, left to right.
                var unlocks = new List<ChaosUnlock>();
                foreach (var unlock in c.ChaosUnlocks)
                    if (unlock != null && unlock.gameObject.activeInHierarchy) unlocks.Add(unlock);
                unlocks.Sort((x, y) => x.transform.position.x.CompareTo(y.transform.position.x));
                foreach (var unlock in unlocks)
                {
                    var group = unlock.MainCanvasGroup;
                    if (group != null && group.alpha < Shown) continue;
                    var u = unlock;
                    Func<string> text = () => UnlockText(u);
                    if (string.IsNullOrWhiteSpace(text())) continue;
                    var vt = GameNodes.Text(text);
                    vt.OnFocus = () => ViewNodes.Hover(u);
                    b.AddItem(ControlId.Structural("chaosgod:unlock:" + unlock.gameObject.name), vt);
                }
            }
            if (ViewNodes.IsShown(c.ProceedButton))
                b.AddItem(ControlId.Structural("chaosgod:proceed"), ViewNodes.Button(c.ProceedButton));
            b.PopContext();
        }

        private static string UnlockText(ChaosUnlock unlock)
        {
            string text = GameText.Of(unlock.hoverText);
            string name = Names.Of(unlock);
            if (name == null) return text;
            return text != null ? Strings.UnlockNamed(text, name) : name;
        }

        /// <summary>The name of what each unlock icon shows, recorded as the game sets the icon up,
        /// by the icon's native pointer (the panel's three icons live as long as the scene).</summary>
        [HarmonyPatch]
        internal static class Names
        {
            private static readonly Dictionary<IntPtr, string> s_names = new Dictionary<IntPtr, string>();

            public static string Of(ChaosUnlock unlock)
                => unlock != null && s_names.TryGetValue(unlock.Pointer, out var name) ? name : null;

            [HarmonyPatch(typeof(ChaosUnlock), nameof(ChaosUnlock.InitializeEnemy))]
            [HarmonyPrefix]
            private static void Enemy(ChaosUnlock __instance, EnemyName enemyName)
            {
                try { Record(__instance, GameLoc.EnemyName(enemyName), enemyName.ToString()); }
                catch (Exception e) { CoreLog.Warning("ChaosGod: naming an invader unlock failed: " + e.Message); }
            }

            [HarmonyPatch(typeof(ChaosUnlock), nameof(ChaosUnlock.InitializeRoom))]
            [HarmonyPrefix]
            private static void Room(ChaosUnlock __instance, RoomName roomName)
            {
                try { Record(__instance, RoomTitle(roomName), roomName.ToString()); }
                catch (Exception e) { CoreLog.Warning("ChaosGod: naming a room unlock failed: " + e.Message); }
            }

            private static void Record(ChaosUnlock unlock, string name, string id)
            {
                if (unlock == null) return;
                if (name == null) CoreLog.Warning("ChaosGod: no localized name for unlock " + id);
                s_names[unlock.Pointer] = name;
            }

            // A boss room by its boss title ("The Kraken"); the special encounters by the names the
            // encounter choice gives them.
            private static string RoomTitle(RoomName room)
            {
                switch (room)
                {
                    case RoomName.CloudRoom: return GameLoc.Get("CloudyEncounter_Misc");
                    case RoomName.HolesRoom: return GameLoc.Get("HolesEncounter_Misc");
                    case RoomName.VoidRoom: return GameLoc.Get("VoidZoneEncounter_Misc");
                    default: return GameLoc.Get("BossTitle_" + room + "_Misc");
                }
            }
        }
    }
}
