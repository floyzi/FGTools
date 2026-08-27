extern alias wle;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using FG.Common;
using FG.Common.CMS;
using FGClient;
using FGClient.UI;
using FGTools.Content;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States;
using FGTools.States.Logic;
using FGTools.UI.ConfigManager.UI;
using FGTools.UI.Tabs;
using FGTools.UI.Tabs.Logic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Panels;
using UniverseLib.UI.Widgets;
using wle::Wushu.LevelEditor.Runtime.UI.LevelBrowser;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.Services.MenuThemeService;
using static FGTools.States.Logic.FGTBase;
using static FGTools.UI.ReadyPopups;

namespace FGTools.UI
{
    public class NewGUI(UIBase owner) : PanelBase(owner)
    {
        public enum Tab
        {
            RoundLoader = 0,
            ShowLoader = 1,
            LocalMultiplayer = 2,
            PresetSelector = 3,
            MediaLoader = 4,
            IMG2FGCLoader = 5,
            Misc = 6,
            FGCAutosaves = 7,
            Credits = 9,
            Config = 10,
            RoundLoader_Main = 50,
            RoundLoader_FGC = 51,
            RoundLoader_InGame = 52,
            RoundLoader_Options = 53
        }

        public enum SubLevel
        {
            Default,
            RoundLoader
        }

        public enum ObjectGroup
        {
            Results,
            Explore,
            Menu,
            Loading,
            Gameplay,
            Editor
        }

        public enum GroupOperation
        {
            SetActive,
            Interactable,
        }

        public struct TabControlledElement
        {
            public GameObject TabContainer;
            public ButtonRef TabButton;
            public string TabTitle;
            public SubLevel TabLevel;
        }

        public struct GroupPolicy(ObjectGroup group, GroupOperation policy)
        {
            public ObjectGroup Group = group;
            public GroupOperation Policy = policy;
        }

        public struct UIControlledElement
        {
            public object ControlledObject;
            public Action<ObjectGroup> OnChanged;
            public Dictionary<GroupPolicy, Func<bool>> StatePerGroup;
        }

        internal struct TabMeta
        {
            internal Tab Tab;
            internal SubLevel SubLevel;
            internal UITab TabObject;
            internal TabMeta Copy()
            {
                return new TabMeta()
                {
                    SubLevel = SubLevel,
                    Tab = Tab,
                    TabObject = TabObject
                };
            }
        }

        public override string Name => Launcher.DisplayName;
        public override int MinWidth => 700;
        public override int MinHeight => 480;
        public override Vector2 DefaultAnchorMin => new(0.05f, 0.05f);
        public override Vector2 DefaultAnchorMax => new(0.35f, 0.35f);
        public override bool CanDragAndResize => true;

        public static NewGUI Instance;
        Dictionary<Tab, TabControlledElement> Tabs = new();
        List<UIControlledElement> ControlledObjects = new();
        internal TabMeta CurrentTab;
        internal TabMeta PreviousTab;

        //ui controlled elements
        //ButtonRef showPlayBtn;

        GameObject FGTShowLoaderGUI;
        GameObject FGTLocalMultiplayerGUI;
        GameObject FGTPresetsGUI;
        GameObject FGTMediaGUI;
        GameObject FGTImg2FGCGUI;
        GameObject FGTMiscGUI;
        GameObject FGTAutosavesGUI;
        GameObject FGTCreditsGUI;
        GameObject FGTConfigGUI;

        //GameObject RLG_Gameplay;

        //GameObject gameplayGUI_Content;
        //Text rl_desc;
        //ButtonRef additiveButton;
     
        
      
   
        //GameObject loadingBtnsFGC;

    
        public override Vector2 DefaultPosition => new Vector2(-350, 400);
        readonly Queue<Action> PendindTabs = new();

        Text TabHoverText;
        bool HoveringOnTab;

        readonly List<UITab> _tabMap = 
        [
            new RoundLoaderTab(),
            new ShowLoaderTab(),
#if LAN_MULTIPLAYER
            new LANMultiplayTab(),
#endif
            new PresetsTab(),
            new MediaLoaderTab(),
            new ImgInFgcTab(),
            new MiscTab(),
            new FGCLocalSavesTab(),
            new CreditsTab(),
            
        ];

        internal event Action<TabMeta> OnTabChanged;
        internal event Action<FGTStateManager.ToolsState> OnStateChange;

        protected override void ConstructPanelContent()
        {
            Instance = this;

            try
            {
                FGTLog(LogLevel.Info, GetType(), "Trying to draw tabs...");

                GameActions.OnStateChange += StateChange;

                var tabGroup = UIFactory.CreateHorizontalGroup(ContentRoot, "Tabs", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(tabGroup, minHeight: 25, flexibleHeight: 0);

                foreach (var tab in _tabMap)
                {
                    var t = CreateTab(tab.Tab, SubLevel.Default, tabGroup, () => tab.ControlledObject, "todo", "todo");
                    tab.TabButton = t.Component;
                    AssignToGroups(t.Component, tab.StatePerGroup, tab.OnStateChange);
                }

                CreateTab(Tab.Config, SubLevel.Default, tabGroup, () => FGTConfigGUI, "gui_config", "gui_config");
                CreateTab(Tab.Credits, SubLevel.Default, tabGroup, () => FGTCreditsGUI, "gui_credits", "gui_credits");
            }
            catch (Exception e)
            {
                OnUIFail(e, 1);
            }

            try
            {
                FGTLog(LogLevel.Info, GetType(), "Trying to draw UI elements...");

                foreach (var tab in _tabMap)
                {
                    tab.Draw(ContentRoot);
                }

                DrawConfig();

                TabHoverText = UIFactory.CreateLabel(Launcher.UniverseUIBase.RootObject, "TabTitle", "", TextAnchor.MiddleCenter);
                TabHoverText.rectTransform.sizeDelta = new(500, 100);
                TabHoverText.gameObject.AddComponent<Outline>();
                CanvasGroup popupGroup = TabHoverText.gameObject.AddComponent<CanvasGroup>();
                popupGroup.blocksRaycasts = false;
            }
            catch (Exception e)
            {
                OnUIFail(e, 2);
            }

            try
            {
                FGTLog(LogLevel.Info, GetType(), "Finalizing...");

                while (PendindTabs.Count > 0)
                {
                    var val = PendindTabs.Dequeue();
                    val.Invoke();
                }

                GoToTab(Tab.RoundLoader, SubLevel.Default);
                GoToTab(Tab.RoundLoader_Main, SubLevel.RoundLoader);

                FGTBase.FGTServiceManager.OnGUICreated();
            }
            catch (Exception e)
            {
                OnUIFail(e, 3);
            }
        }

        internal void AssignToGroups(object obj, Dictionary<GroupPolicy, Func<bool>> statePerGroup, Action<ObjectGroup> onChanged = null)
        {
            if (statePerGroup == null || statePerGroup.Count == 0) return;

            ControlledObjects.Add(new()
            {
                ControlledObject = obj,
                StatePerGroup = statePerGroup,
                OnChanged = onChanged
            });
        }

        void ToggleGroup(ObjectGroup group)
        {
            foreach (var obj in ControlledObjects)
            {
                var unityObject = obj.ControlledObject as UnityEngine.Object;

                var a = obj.StatePerGroup.Keys.ToList().Find(x => x.Group == group);
                if (a.Equals(default))
                {
                    FGTLog(LogLevel.Warning, GetType(), $"Object \"{unityObject.name}\" doesn't have state on group \"{group}\"!");
                    continue;
                }

                if (obj.StatePerGroup.TryGetValue(a, out var state))
                {
                    switch (a.Policy)
                    {
                        case GroupOperation.SetActive:
                            var actualObj = obj.ControlledObject as GameObject;
                            actualObj.SetActive(state());
                            break;
                        case GroupOperation.Interactable:
                            var actualSelectable = obj.ControlledObject as Selectable;
                            actualSelectable.interactable = state();
                            break;
                    }

                    obj.OnChanged?.Invoke(group);
                }
            }
        }

        internal ButtonRef CreateTab(Tab tab, SubLevel tabLevel, GameObject tabGroup, Func<GameObject> tabControlledObject, string localizedTabName, string localizedTabTitle = "")
        {
            var tabBtn = UIFactory.CreateButton(tabGroup, $"Button_{tab}", $"{LocalizedStr(localizedTabName)}");
            tabBtn.OnClick += () => { GoToTab(tab, tabLevel); };

            PendindTabs.Enqueue(() =>
            {
                Tabs.Add(tab, new()
                {
                    TabButton = tabBtn,
                    TabContainer = tabControlledObject(),
                    TabTitle = localizedTabTitle,
                    TabLevel = tabLevel,
                });
            });

            var trigger = tabBtn.Component.gameObject.GetComponent<EventTrigger>() ?? tabBtn.Component.gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };

            var exit = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerExit,
            };

            entry.callback.AddListener((data) =>
            {
                HoveringOnTab = true;
                TabHoverText.text = LocalizedStr(localizedTabName);
            });

            exit.callback.AddListener((data) =>
            {
                HoveringOnTab = false;
                TabHoverText.text = "";
            });

            trigger.triggers.Add(entry);
            trigger.triggers.Add(exit);

            return tabBtn;
        }

        void OnUIFail(Exception e, int lvl)
        {
            FGTLog(LogLevel.Fatal, GetType(), $"Unable to init UI (failed on {lvl}) due to an error {e.Message} | {e.StackTrace}");
            DoModal(new(LocalizedStr("new_gui_err_title"), LocalizedStr("new_gui_err_desc", [e.Message]), UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Disruptive));
        }

        protected override void OnClosePanelClicked()
        {
            ToggleUI(false);
        }

        internal void ToggleUI(bool state)
        {
            if (HoveringOnTab)
            {
                HoveringOnTab = false;
                TabHoverText.text = null;
            }

            UniversalUI.SetUIActive(UniverseGUID, state);
            UIRoot.gameObject.SetActive(state);

            FGTBase.StateManager.InternalState.LoaderUIToggle = state;
        }

        internal void TryDrawUI(Func<bool> condition, GameObject group, Action onValid)
        {
            if (condition())
            {
                onValid();
                return;
            }

            FGTLog(LogLevel.Warning, GetType(), $"Refused to draw content of {group.name}, feature disabled");

            var failTitle = UIFactory.CreateLabel(group, "failTitle", LocalizedStr("gui_disabled_feature"), TextAnchor.MiddleCenter);
            UIFactory.SetLayoutElement(failTitle.gameObject, minHeight: 25, flexibleHeight: 0);
        }

        internal class EntryInfo(CachedConfigEntry cached)
        {
            public CachedConfigEntry Cached { get; } = cached; public ConfigEntryBase RefEntry;
            public bool IsHidden { get; internal set; }

            internal GameObject content;
        }

        List<EntryInfo> confEntries;

        void SearchConfig(string q)
        {
            q = q.ToLower();
            foreach (var entry in confEntries)
            {
                bool val = (string.IsNullOrEmpty(q)
                                || entry.RefEntry.Definition.Key.ToLower().Contains(q)
                                || (entry.RefEntry.Description?.Description?.Contains(q) ?? false))
                           && (!entry.IsHidden);

                entry.content.SetActive(val);
            }
        }
        void DrawConfig()
        {
            confEntries = [];
            FGTConfigGUI = UIFactory.CreateVerticalGroup(ContentRoot, "Config", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(FGTConfigGUI, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);
            var search = UIFactory.CreateInputField(FGTConfigGUI, "configGUI", LocalizedStr("gui_search"));
            search.OnValueChanged += SearchConfig;
            UIFactory.SetLayoutElement(search.GameObject, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 25);
            GameObject content = UIFactory.CreateScrollView(FGTConfigGUI, "configGUI", out _, out _, new(0.1f, 0.1f, 0.1f));
            UIFactory.SetLayoutElement(content, preferredHeight: 310, flexibleHeight: 9999, flexibleWidth: 9999);
            var scroll = content.GetComponent<ScrollRect>().content.gameObject;

            var dict = new Dictionary<string, List<ConfigEntryBase>>()
                {
                    { "", new List<ConfigEntryBase>() }
                };

            foreach (var entry in Launcher.BepConfig.Keys)
            {
                string sec = entry.Section;
                sec ??= "";

                if (!dict.ContainsKey(sec))
                    dict.Add(sec, []);

                dict[sec].Add(Launcher.BepConfig[entry]);
            }

            foreach (var ctg in dict)
            {
                if (!string.IsNullOrEmpty(ctg.Key))
                {
                    var bg = UIFactory.CreateHorizontalGroup(scroll, "TitleBG", true, true, true, true, 0, default, new Color(0.07f, 0.07f, 0.07f));

                    var title = UIFactory.CreateLabel(bg, $"Title_{ctg.Key}", ctg.Key, TextAnchor.MiddleCenter, default, true, 17);
                    UIFactory.SetLayoutElement(title.gameObject, minHeight: 30, minWidth: 200, flexibleWidth: 9999);
                }

                foreach (var configEntry in ctg.Value)
                {
                    CachedConfigEntry cache = new(configEntry, scroll);
                    cache.Enable();

                    GameObject obj = cache.UIroot;

                    bool advanced = false;

                    if (!advanced)
                    {
                        object[] tags = configEntry.Description?.Tags;
                        if (tags != null && tags.Any())
                        {
                            if (tags.Any(it => it is string s && s == "Advanced"))
                            {
                                advanced = true;
                            }
                            else if (tags.FirstOrDefault(it => it.GetType().Name == "ConfigurationManagerAttributes") is object attributes)
                            {
                                advanced = (bool?)attributes.GetType().GetField("IsAdvanced")?.GetValue(attributes) == true;
                            }
                        }
                    }


                    confEntries.Add(new EntryInfo(cache)
                    {
                        RefEntry = configEntry,
                        content = obj,
                        IsHidden = advanced
                    });
                }
            }

            // hide buttons for completely-hidden categories.

            content.SetActive(true);


        }

        internal void RefreshEverything(bool onlyCleanup = false)
        {
            if (!onlyCleanup)
                FGTLog(LogLevel.Info, GetType(), "Trying to refresh everything");
            try
            {
                foreach (var tab in _tabMap)
                {
                    tab.Refresh();
                }

         
                FGTBase.FGTServiceManager.OnGUIRefresh();

                if (!onlyCleanup)
                {

                }

            }
            catch (Exception ex)
            {
                FGTLog(LogLevel.Fatal, GetType(), $"An error occured with this action: {ex.Message}");
            }
            FGTLog(LogLevel.Info, GetType(), $"Complete!");
        }

        void UpdateTitle(string title)
        {
            if (!string.IsNullOrEmpty(title))
                TitleBar.transform.GetChild(0).GetComponent<Text>().text = $"{Launcher.DisplayName} V{Launcher.BuildInfo.UI_Version} > {title}";
        }

        internal void GoToTab(Tab selectedTab, SubLevel tabLevel, bool shouldChangeTitle = true, bool silent = false)
        {
            var tab = Tabs[selectedTab];
            if (tab.TabContainer == null)
            {
                FGTLog(LogLevel.Error, GetType(), $"Can't find tab for {selectedTab} on {tabLevel}");
                return;
            }

            var groupTabs = Tabs.ToList().FindAll(x => x.Value.TabLevel == tabLevel);
            if (groupTabs == null || groupTabs.Count == 0)
            {
                FGTLog(LogLevel.Error, GetType(), $"Can't find any tabs on {tabLevel}");
                return;
            }

            foreach (var holdedTab in groupTabs)
            {
                if (holdedTab.Key != selectedTab)
                {
                    holdedTab.Value.TabContainer.gameObject.SetActive(false);
                    RuntimeHelper.SetColorBlock(holdedTab.Value.TabButton.Component, UniversalUI.DisabledButtonColor, UniversalUI.DisabledButtonColor * 1.2f);
                }
            }

            RuntimeHelper.SetColorBlock(tab.TabButton.Component, UniversalUI.EnabledButtonColor, UniversalUI.EnabledButtonColor * 1.2f);
            tab.TabContainer.SetActive(true);

            if (silent)
                return;

            var title = Tabs[selectedTab].TabTitle;
            if (shouldChangeTitle && !string.IsNullOrEmpty(title))
                UpdateTitle(LocalizedStr(title));

            PreviousTab = CurrentTab.Copy();
            CurrentTab = new()
            {
                SubLevel = tabLevel,
                Tab = selectedTab,
                TabObject = _tabMap.FirstOrDefault(x => x.Tab == selectedTab)
            };

            OnTabChanged.Invoke(CurrentTab);
        }

        internal T GetTab<T>(Tab tab) where T : UITab
        {
            return _tabMap.FirstOrDefault(x => x.Tab == tab) as T;
        }    

        void StateChange(FGTStateManager.ToolsState state)
        {
            GoToTab(Tab.RoundLoader_Main, SubLevel.RoundLoader, silent: true);

            OnStateChange.Invoke(state);

            switch (state)
            {
                case FGTStateManager.ToolsState.Menu:
                    ToggleGroup(ObjectGroup.Menu);
                    break;
                case FGTStateManager.ToolsState.Results:
                    ToggleGroup(ObjectGroup.Results);
                    break;
                case FGTStateManager.ToolsState.RoundLoading:
                case FGTStateManager.ToolsState.GPFGCLoading:
                case FGTStateManager.ToolsState.RoundIntro:
                    ToggleGroup(ObjectGroup.Loading);
                    break;
                case FGTStateManager.ToolsState.OnlineGameActive:
                case FGTStateManager.ToolsState.GameActive:
                case FGTStateManager.ToolsState.FGCGameActive:
                    ToggleGroup(ObjectGroup.Gameplay);
                    if (StateManager.ExploreState != null)
                        ToggleGroup(ObjectGroup.Explore);
                    break;
                case FGTStateManager.ToolsState.InCreative:
                    ToggleGroup(ObjectGroup.Editor);
                    break;
            }
        }

        public void GUIController()
        {
            try
            {
                if (HoveringOnTab)
                    TabHoverText.transform.position = Input.mousePosition + (Vector3.up * 15);

                CurrentTab.TabObject.Update();

                if (CurrentTab.Tab == Tab.Misc)
                {
                   
                }

                if (CurrentTab.Tab == Tab.MediaLoader)
                {
                    
                }
            }
            catch
            {

            }
        }
    }
}
