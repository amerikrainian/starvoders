using System;
using StarVaders;
using StarVoders.Core.Graph;
using TMPro;
using UnityEngine;

namespace StarVoders.Module.UI
{
    /// <summary>
    /// A guide page as the pause menu's guide and Tippy's Quick Guide draw them: speech bubbles of prose
    /// beside pictures. The prose is the texts named <c>Line1</c>..<c>Line4</c>, read in hierarchy order
    /// (the bubbles top to bottom), a line per text line; the rest label the pictures (a mock Heat panel's
    /// "Heat", "+1", "New!", "Will whiff!" on the example cards) and are meaningless read alone. A demo
    /// artifact reads as the card viewer's.
    /// </summary>
    internal static class GuidePages
    {
        public static void AddPage(GraphBuilder b, Transform page, string prefix)
        {
            int i = 0;
            Walk(b, page, prefix, ref i);
        }

        private static void Walk(GraphBuilder b, Transform t, string prefix, ref int i)
        {
            if (!t.gameObject.activeInHierarchy) return;
            var artifact = t.GetComponent<DeckArtifactView>();
            if (artifact != null)
            {
                if (artifact.ArtifactModel != null)
                    b.AddItem(ControlId.Structural(prefix + ":artifact" + i++), CardNodes.Artifact(artifact.ArtifactModel, artifact));
                return;
            }
            var tmp = t.name.StartsWith("Line", StringComparison.Ordinal) ? t.GetComponent<TMP_Text>() : null;
            if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                foreach (var raw in tmp.text.Split('\n'))
                {
                    string line = GameText.Clean(raw)?.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    b.AddItem(ControlId.Structural(prefix + ":line" + i++), GameNodes.Text(() => line));
                }
            for (int c = 0; c < t.childCount; c++) Walk(b, t.GetChild(c), prefix, ref i);
        }
    }
}
