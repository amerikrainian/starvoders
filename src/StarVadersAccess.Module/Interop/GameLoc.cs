using System;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Module.UI;

namespace StarVadersAccess.Module.Interop
{
    /// <summary>
    /// The game's localized names and descriptions, by the keys its own <see cref="LocalizationUtils"/>
    /// helpers build (<c>LocalizeEntityTitle</c> is <c>name + "_EntityTitle"</c> and so on). Those
    /// helpers answer through a callback; <see cref="LocalizationUtils.LocalizeSynchronous"/> answers
    /// at once from the same table, and says "No translation for '...'" for a missing key, which
    /// reads as null here.
    /// </summary>
    internal static class GameLoc
    {
        public static string EnemyName(EnemyName name) => Get(name + "_EntityTitle");
        public static string ArtifactName(ArtifactName name) => Get(name + "_ArtiTitle");

        /// <summary>
        /// An entity's description (<c>key</c> is its <c>_EntityDesc</c> key) as its info panel shows
        /// it: <see cref="LocalizationUtils.GetModifiedDescriptions(string, AEntityModel,
        /// EncounterController)"/> fills a counter's "[#]" from the model in a fight (Time Angel's
        /// "([#] left)"), and strips that parenthesis without an encounter.
        /// </summary>
        public static string EntityDescription(AEntityModel entity, string key, EncounterController encounter)
        {
            try
            {
                string text = LocalizationUtils.LocalizeSynchronous(key);
                if (string.IsNullOrWhiteSpace(text) || text.StartsWith("No translation for", StringComparison.Ordinal)) return null;
                if (entity != null) text = LocalizationUtils.GetModifiedDescriptions(text, entity, encounter);
                return GameText.Clean(text);
            }
            catch (Exception e)
            {
                CoreLog.Warning("GameLoc: describing " + key + " failed: " + e.Message);
                return null;
            }
        }

        public static string Get(string key)
        {
            try
            {
                string text = LocalizationUtils.LocalizeSynchronous(key);
                if (string.IsNullOrWhiteSpace(text) || text.StartsWith("No translation for", StringComparison.Ordinal)) return null;
                return GameText.Clean(text);
            }
            catch (Exception e)
            {
                CoreLog.Warning("GameLoc: localizing " + key + " failed: " + e.Message);
                return null;
            }
        }
    }
}
