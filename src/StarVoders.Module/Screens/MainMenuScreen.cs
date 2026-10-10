using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using UnityEngine;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The main menu. The game's own column (<c>VerticalViewItemList</c>: Start or Resume, Daily
    /// Run, Options, Compendium, Run History, Quit, whichever are shown) is the first list, in the
    /// game's order, Resume followed by the run's delete icon ("Delete Current Run", the game's
    /// words; it asks first); then the save file, the language, and the three icon buttons along the edge
    /// (News, Discord, Credits), which carry no caption of their own and so read by the mod's words.
    /// </summary>
    public sealed class MainMenuScreen : Screen
    {
        public override string Key => "mainmenu";
        public override string ScreenName => Strings.ScreenMainMenu;

        private static MainMenuSceneController Controller => GameScenes.Controller<MainMenuSceneController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;

            b.PushContext(null);
            var column = c.VerticalViewItemList;
            if (column != null)
                foreach (var item in column)
                    if (ViewNodes.IsShown(item))
                    {
                        b.AddItem(ControlId.Structural("main:" + item.gameObject.name), ViewNodes.Button(item));
                        // The run's delete icon sits on the Resume button, outside the column: right after it.
                        if (item.gameObject.name == "ResumeButton")
                        {
                            var delete = item.transform.Find("Background/Delet/ButtonTemplate")?.GetComponent<AViewItem>();
                            if (ViewNodes.IsShown(delete))
                                b.AddItem(ControlId.Structural("main:deleterun"), ViewNodes.Button(delete, () => GameLoc.Get("DeleteCurrentRun_Misc")));
                        }
                    }
            b.PopContext();

            b.PushContext(null);
            AddButton(b, c.ProfileButton, "main:profile", null);
            var dropdown = c.LanguageSelector != null ? c.LanguageSelector.GetComponentInChildren<TMP_Dropdown>(false) : null;
            if (dropdown != null && dropdown.gameObject.activeInHierarchy)
                b.AddItem(ControlId.Structural("main:language"), GameNodes.Dropdown(dropdown, () => Strings.MainLanguage));
            var news = Find(c, "News");
            if (news != null)
                b.AddItem(ControlId.Structural("main:news"), ViewNodes.Button(news, () => NewsLabel(c)));
            AddButton(b, c.DiscordButton, "main:discord", () => Strings.MainDiscord);
            var credits = Find(c, "Credits");
            if (credits != null)
                b.AddItem(ControlId.Structural("main:credits"), ViewNodes.Button(credits, () => Strings.MainCredits));
            b.PopContext();
        }

        private static void AddButton(GraphBuilder b, GameObject go, string id, System.Func<string> label)
        {
            var item = go != null ? go.GetComponent<AViewItem>() : null;
            if (ViewNodes.IsShown(item)) b.AddItem(ControlId.Structural(id), ViewNodes.Button(item, label));
        }

        // "News", or "News, New Update!" while the game flags an update it has not shown yet.
        private static string NewsLabel(MainMenuSceneController c)
        {
            var flag = c.NewsUpdateText;
            if (flag == null || !flag.activeInHierarchy) return Strings.MainNews;
            var tmp = flag.GetComponentInChildren<TMP_Text>(true);
            return tmp != null && !string.IsNullOrWhiteSpace(tmp.text) ? Strings.MainNews + ", " + tmp.text : Strings.MainNews;
        }

        // The edge buttons the controller holds no field for, by their object names, under the
        // Discord button's parent (they are siblings on the menu canvas).
        private static AViewItem Find(MainMenuSceneController c, string name)
        {
            var anchor = c.DiscordButton != null ? c.DiscordButton.transform.parent : null;
            var t = anchor != null ? anchor.Find(name) : null;
            var item = t != null ? t.GetComponent<AViewItem>() : null;
            return ViewNodes.IsShown(item) ? item : null;
        }

        public override IEnumerable<ElementAction> GetActions() { yield break; }
    }
}
