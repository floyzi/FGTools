extern alias wle;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using FG.Common;
using FG.Common.CMS;
using FGClient;
using FGClient.UI;
using FGClient.UI.Core;
using FGTools.Config;
using FGTools.Content;
using FGTools.Internal;
using FGTools.Internal.Behaviours;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States;
using FGTools.States.Logic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using static FGTools.States.Logic.FGTStateManager;
using static FGTools.UI.ReadyPopups;
using static Il2CppSystem.Globalization.TimeSpanFormat;

namespace FGTools.UI
{
    public class FGToolsUI : FGTBase
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

            public struct TabMeta
            {
                public Tab Tab;
                public SubLevel SubLevel;

                public TabMeta Copy()
                {
                    return new TabMeta()
                    {
                        SubLevel = SubLevel,
                        Tab = Tab
                    };
                }
            }

            public override string Name => Plugin.DisplayName;
            public override int MinWidth => 700;
            public override int MinHeight => 480;
            public override Vector2 DefaultAnchorMin => new(0.05f, 0.05f);
            public override Vector2 DefaultAnchorMax => new(0.35f, 0.35f);
            public override bool CanDragAndResize => true;

            public static NewGUI Instance;
            Dictionary<Tab, TabControlledElement> Tabs = new();
            List<UIControlledElement> ControlledObjects = new();
            public TabMeta CurrentTab;
            public TabMeta PreviousTab;

            //ui controlled elements
            //ButtonRef showPlayBtn;

            GameObject FGTRoundLoaderGUI;
                GameObject unityLoaderGUI;
                GameObject fgcGUI;
                GameObject gameplayGUI;

            GameObject FGTShowLoaderGUI;
            GameObject FGTLocalMultiplayerGUI;
            GameObject FGTPresetsGUI;
            GameObject FGTMediaGUI;
            GameObject FGTImg2FGCGUI;
            GameObject FGTMiscGUI;
            GameObject FGTAutosavesGUI;
            GameObject FGTCreditsGUI;

            //GameObject RLG_Gameplay;
      
            //GameObject gameplayGUI_Content;
            //Text rl_desc;
            //ButtonRef additiveButton;
            Text langAuthor;

            ButtonRef randt;
            public Text levelInfo;
            GameObject FGCLevelInfo;
            GameObject FGCHistory;
            GameObject fgcHistoryPrefab;
            GameObject FGTRoundLoaderTabs;
            GameObject loadOptionsGUI;
            GameObject loadOptionsGUI_Content;
            //GameObject loadingBtnsFGC;

            public string imgHeight = "75";
            public string imgWidth = "75";
            public bool shouldDeleteWhitePixels = false;
            public bool shouldDeleteBlackPixels = false;
            public bool isDigital = false;
            public bool haveGeneratedLevel;
            string ExploreRoundsCount = "0";

            string selectedLang = null;
            public override Vector2 DefaultPosition => new Vector2(-350, 400);
            Queue<Action> PendindTabs = new();

            internal Text TabHoverText;
            internal bool TabHover;

            void AssignToGroups(object obj, Dictionary<GroupPolicy, Func<bool>> statePerGroup, Action<ObjectGroup> onChanged = null)
            {
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

            ButtonRef CreateTab(Tab tab, SubLevel tabLevel, GameObject tabGroup, Func<GameObject> tabControlledObject, string localizedTabName, string localizedTabTitle = "")
            {
                var tabBtn = UIFactory.CreateButton(tabGroup, $"Button_{tab}", $"{LocalizedStr(localizedTabName)}");
                tabBtn.OnClick += () => { GoToTab(tab, tabLevel); };
                PendindTabs.Enqueue(() => {
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
                    TabHover = true;
                    TabHoverText.text = LocalizedStr(localizedTabName);
                });

                exit.callback.AddListener((data) =>
                {
                    TabHover = false;
                    TabHoverText.text = "";
                });

                trigger.triggers.Add(entry);
                trigger.triggers.Add(exit);
                return tabBtn;
            }

            void OnUIFail(Exception e, int lvl)
            {
                FGTLog(LogLevel.Fatal, GetType(), $"Unable to init UI (failed on {lvl}) due to an error {e.Message} | {e.StackTrace}");
                DoModal(LocalizedStr("new_gui_err_title"), LocalizedStr("new_gui_err_desc", [e.Message]), UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Disruptive);
            }


            protected override void ConstructPanelContent()
            {
                Instance = this;

                try
                {
                    FGTLog(LogLevel.Info, GetType(), "Trying to draw tabs");

                    Commands.OnStateChange += OnStateChange;

                    GameObject tabGroup = UIFactory.CreateHorizontalGroup(ContentRoot, "Tabs", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                    UIFactory.SetLayoutElement(tabGroup, minHeight: 25, flexibleHeight: 0);

                    CreateTab(Tab.RoundLoader, SubLevel.Default, tabGroup, () => FGTRoundLoaderGUI, "gui_cms_loader", "gui_cms_loader");

                    var showTab = CreateTab(Tab.ShowLoader, SubLevel.Default, tabGroup, () => FGTShowLoaderGUI, "gui_show_loader", "gui_show_loader");
                    AssignToGroups(showTab.Component, new()
                    {
                        { new GroupPolicy(ObjectGroup.Editor, GroupOperation.Interactable), () => false },
                        { new GroupPolicy(ObjectGroup.Menu, GroupOperation.Interactable), () => true },
                        { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.Interactable), () => true },
                        { new GroupPolicy(ObjectGroup.Loading, GroupOperation.Interactable), () => false }
                    });

#if LAN_MULTIPLAYER
                    CreateTab(Tab.LocalMultiplayer, SubLevel.Default, tabGroup, () => FGTLocalMultiplayerGUI, "gui_lan_multiplayer", "gui_lan_multiplayer");
#endif
                    var presetsBtn = CreateTab(Tab.PresetSelector, SubLevel.Default, tabGroup, () => FGTPresetsGUI, "gui_presets_title", "gui_presets_title");
                    AssignToGroups(presetsBtn.Component, new()
                    {
                        { new GroupPolicy(ObjectGroup.Editor, GroupOperation.Interactable), () => false },
                        { new GroupPolicy(ObjectGroup.Menu, GroupOperation.Interactable), () => true },
                        { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.Interactable), () => true },
                        { new GroupPolicy(ObjectGroup.Loading, GroupOperation.Interactable), () => false }
                    });

                    CreateTab(Tab.MediaLoader, SubLevel.Default, tabGroup, () => FGTMediaGUI, "gui_media_tools_title", "gui_media_tools_title");

                    var img2fgc = CreateTab(Tab.IMG2FGCLoader, SubLevel.Default, tabGroup, () => FGTImg2FGCGUI, "gui_img2fgc_tab", "gui_img2fgc");
                    AssignToGroups(img2fgc.Component, new()
                    {
                        { new GroupPolicy(ObjectGroup.Editor, GroupOperation.Interactable), () => false },
                        { new GroupPolicy(ObjectGroup.Menu, GroupOperation.Interactable), () => true },
                        { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.Interactable), () => false },
                        { new GroupPolicy(ObjectGroup.Loading, GroupOperation.Interactable), () => false }
                    });

                    CreateTab(Tab.Misc, SubLevel.Default, tabGroup, () => FGTMiscGUI, "gui_misc", "gui_misc");
                    CreateTab(Tab.FGCAutosaves, SubLevel.Default, tabGroup, () => FGTAutosavesGUI, "gui_fgc_local_autosaves", "gui_fgc_local_autosaves");
                    CreateTab(Tab.Credits, SubLevel.Default, tabGroup, () => FGTCreditsGUI, "gui_credits", "gui_credits");
                }
                catch (Exception e)
                {
                    OnUIFail(e, 1);
                }

                try
                {
                    FGTLog(LogLevel.Info, GetType(), "Trying to draw ui elements");
                    DrawRoundLoader();
                    DrawShowLoader();
#if LAN_MULTIPLAYER
                    DrawLocalMultiplayer();
#endif
                    DrawPresetSelector();
                    DrawMediaLoader();
                    DrawIMG2FGC();
                    DrawMisc();
                    DrawFGCAutosaves();
                    DrawCredits();

                    TabHoverText = UIFactory.CreateLabel(Plugin.UniverseUIBase.RootObject, "TabTitle", "", TextAnchor.MiddleCenter);
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
                }
                catch (Exception e)
                {
                    OnUIFail(e, 3);
                }
            }

            protected override void OnClosePanelClicked()
            {
                UniversalUI.SetUIActive(UniverseGUID, false);
                UIRoot.gameObject.SetActive(false);
                StateManager.InternalState.LoaderUIToggle = false;
            }

            void TryDrawUI(Func<bool> condition, GameObject group, Action onValid)
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

            void DrawRoundLoader()
            {
                #region ROUND LOADER - SETUP
                FGTRoundLoaderGUI = UIFactory.CreateVerticalGroup(ContentRoot, "FGTRoundLoader", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(FGTRoundLoaderGUI, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);

                FGTRoundLoaderTabs = UIFactory.CreateHorizontalGroup(ContentRoot, "FGTRoundLoaderTabs", true, false, true, false, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(FGTRoundLoaderTabs, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                var mainTab = CreateTab(Tab.RoundLoader_Main, SubLevel.RoundLoader, FGTRoundLoaderTabs, () => unityLoaderGUI, "gui_loader");
                var fgcTab = CreateTab(Tab.RoundLoader_FGC, SubLevel.RoundLoader, FGTRoundLoaderTabs, () => fgcGUI, "gui_creative_loader");
                AssignToGroups(fgcTab.Component, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.Interactable), () => true },
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.Interactable), () => false },
                });
                var gpTab = CreateTab(Tab.RoundLoader_InGame, SubLevel.RoundLoader, FGTRoundLoaderTabs, () => gameplayGUI, "gui_ingame_tab");
                AssignToGroups(gpTab.GameObject, new()
                {
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                });
                var optionsTab = CreateTab(Tab.RoundLoader_Options, SubLevel.RoundLoader, FGTRoundLoaderTabs, () => loadOptionsGUI, "gui_round_options");
                AssignToGroups(optionsTab.Component, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.Interactable), () => true },
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.Interactable), () => false },
                });
                FGTRoundLoaderTabs.transform.SetSiblingIndex(2);

                unityLoaderGUI = UIFactory.CreateVerticalGroup(FGTRoundLoaderGUI, "Loader_GUI", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(unityLoaderGUI, minHeight: 25, flexibleHeight: 0);

                fgcGUI = UIFactory.CreateVerticalGroup(FGTRoundLoaderGUI, "FGC_GUI", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(fgcGUI, minHeight: 25, flexibleHeight: 0);
                fgcGUI.gameObject.SetActive(false);

                gameplayGUI = UIFactory.CreateScrollView(FGTRoundLoaderGUI, "Gameplay_GUI", out var gameplayGUI_Content, out _, new(0.1f, 0.1f, 0.1f));
                gameplayGUI.transform.SetParent(FGTRoundLoaderGUI.transform, false);
                gameplayGUI.gameObject.SetActive(false);

                loadOptionsGUI = UIFactory.CreateScrollView(FGTRoundLoaderGUI, "Gameplay_GUI", out loadOptionsGUI_Content, out _, new(0.1f, 0.1f, 0.1f));
                loadOptionsGUI.transform.SetParent(FGTRoundLoaderGUI.transform, false);
                loadOptionsGUI.gameObject.SetActive(false);
                #endregion

                #region ROUND LOADER - DEFAULT

                Text stats = null;
                Dropdown roundNamesDrop = null;
                Dropdown roundVariantsDrop = null;
                InputFieldRef inputFieldRef = null; 

                TryDrawUI(() => FGTTargetSettings.RoundLoader, unityLoaderGUI, new(() =>
                {
                    var searchbarGroup = UIFactory.CreateHorizontalGroup(unityLoaderGUI, "Search", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    AssignToGroups(searchbarGroup, new()
                    {
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false }
                    });

                    UIFactory.SetLayoutElement(searchbarGroup, minHeight: 30, flexibleHeight: 0);
                    inputFieldRef = UIFactory.CreateInputField(searchbarGroup, "searchInRoundNames", $"{LocalizedStr("gui_search_round")}");
                    ButtonRef printCms = UIFactory.CreateButton(searchbarGroup, "Advanced_PrintCms", $"{LocalizedStr("gui_print_round_list")}", null);
                    printCms.OnClick += () => { FGTRoundLoader.GenerateCMSList(); };
                    printCms.GameObject.SetActive(false);
                    ButtonRef delList = UIFactory.CreateButton(searchbarGroup, "Advanced_DelList", $"{LocalizedStr("gui_del_list")}", GUIRed);
                    delList.OnClick += () =>
                    {
                        if (File.Exists(Plugin.CMSRounds))
                            File.Delete(Plugin.CMSRounds);
                    };
                    delList.GameObject.SetActive(false);

                    var roundNamesGroup = UIFactory.CreateHorizontalGroup(unityLoaderGUI, "Dropdowns", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    AssignToGroups(roundNamesGroup, new()
                {
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.SetActive), () => true },
                });

                    GameObject roundNamesDropUI = UIFactory.CreateDropdown(roundNamesGroup, "RoundNames", out roundNamesDrop, $"{LocalizedStr("dropdown_placeholder")}", 14, null, null);
                    UIFactory.SetLayoutElement(roundNamesDropUI, minHeight: 25, flexibleHeight: 0);
                    GameObject roundIdsDrop = UIFactory.CreateDropdown(roundNamesGroup, "RoundIds", out roundVariantsDrop, $"{LocalizedStr("dropdown_placeholder")}", 14, null, null);
                    UIFactory.SetLayoutElement(roundIdsDrop, minHeight: 25, flexibleHeight: 0);
                    roundVariantsDrop.gameObject.SetActive(false);

                    InputFieldRef advRoundInput = UIFactory.CreateInputField(roundNamesGroup, "advRoundInput", $"{LocalizedStr("gui_rl_a_inputholder")}");
                    advRoundInput.Text = "round_";
                    advRoundInput.OnValueChanged += input => { FGTRoundLoader.RoundToLoad = input; };
                    UIFactory.SetLayoutElement(advRoundInput.GameObject, minHeight: 25, flexibleHeight: 0);
                    advRoundInput.GameObject.gameObject.SetActive(false);
                    UIFactory.SetLayoutElement(roundNamesGroup, minHeight: 25, flexibleHeight: 0);

                    var loadingBtns = UIFactory.CreateHorizontalGroup(unityLoaderGUI, "LoadButtons", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    AssignToGroups(loadingBtns, new()
                {
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.SetActive), () => true }
                });

                    UIFactory.SetLayoutElement(loadingBtns, minHeight: 25, flexibleHeight: 0);
                    var singleButton = UIFactory.CreateButton(loadingBtns, "single", $"{LocalizedStr("gui_single_load")}", null);
                    AssignToGroups(singleButton.GameObject, new()
                {
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true }
                });
                    singleButton.OnClick += () =>
                    {
                        FGTRoundLoader.LoadLatestRound(LoadSceneMode.Single);
                    };

                    UIFactory.SetLayoutElement(singleButton.GameObject, 30, 20, null, 0, null, null, null);
                    var additiveButton = UIFactory.CreateButton(loadingBtns, "additive", $"{LocalizedStr("gui_additive_load")}", null);
                    AssignToGroups(additiveButton.GameObject, new()
                {
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true }
                });
                    additiveButton.OnClick += () =>
                    {
                        FGTRoundLoader.LoadLatestRound(LoadSceneMode.Additive);
                    };
                    UIFactory.SetLayoutElement(additiveButton.GameObject, 30, 20, null, 0, null, null, null);

                    var roundLoadingBtns = UIFactory.CreateHorizontalGroup(unityLoaderGUI, "roundLoadingBtns", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(roundLoadingBtns, minHeight: 25, flexibleHeight: 0);
                    ButtonRef cancel = UIFactory.CreateButton(roundLoadingBtns, "cancel", $"{LocalizedStr("gui_cancel_loading")}", GUIRed);
                    cancel.OnClick += () =>
                    {
                        if (SceneManager.GetActiveScene().name == "MainMenu")
                            cancel.GameObject.SetActive(false);
                        LeaveMatchPopupManager.Instance.OnClose(true);
                    };
                    UIFactory.SetLayoutElement(cancel.GameObject, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);
                    roundLoadingBtns.gameObject.SetActive(false);

                    GameObject mainTools = UIFactory.CreateHorizontalGroup(unityLoaderGUI, "mainTools", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(mainTools, minHeight: 25, flexibleHeight: 0);

                    var UnityExploreBtn = UIFactory.CreateButton(mainTools, "UnityExploreBtn", $"{LocalizedStr("gui_play_explore_unity")}", null);
                    AssignToGroups(UnityExploreBtn.GameObject, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false }
                });

                    UnityExploreBtn.OnClick += () => { StateManager.TryJoinExplore(UltimatePartyState.JoinPolicy.Random); };
                    UIFactory.SetLayoutElement(UnityExploreBtn.GameObject, 30, 20, null, 0, null, null, null);

                    var EndlessExploreBtn = UIFactory.CreateButton(mainTools, "EndlessExploreBtn", $"{LocalizedStr("gui_play_explore_endless")}", null);
                    AssignToGroups(EndlessExploreBtn.GameObject, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false }
                });

                    EndlessExploreBtn.OnClick += () =>
                    {
                        DoModal(LocalizedStr("gui_explore_endless_title"), LocalizedStr("gui_explore_endless_desc") + "\n\n" + LocalizedStr("gui_explore_desc_base"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, new Action<bool>((bool wasok) =>
                        {
                            if (wasok)
                                StateManager.TryJoinExplore(UltimatePartyState.JoinPolicy.Endless);
                        }), hideGUI: ModalHideGUIType.ShowOnCancel);
                    };
                    UIFactory.SetLayoutElement(EndlessExploreBtn.GameObject, 30, 20, null, 0, null, null, null);

                    var RequestRandomRound = UIFactory.CreateButton(mainTools, "NormalExploreBtn", $"{LocalizedStr("gui_random_round")}", null);
                    AssignToGroups(RequestRandomRound.GameObject, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                });

                    RequestRandomRound.OnClick += () =>
                    {
                        StateManager.ExploreState.RequestNewRound();
                    };

                    UIFactory.SetLayoutElement(EndlessExploreBtn.GameObject, 30, 20, null, 0, null, null, null);
                    RequestRandomRound.GameObject.SetActive(false);

                    var upPromptTxt = UIFactory.CreateHorizontalGroup(unityLoaderGUI, "upPromptTxt", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    AssignToGroups(upPromptTxt.gameObject, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => true }
                });

                    UIFactory.SetLayoutElement(upPromptTxt, minHeight: 25, flexibleHeight: 0);
                    var upPromptTxt2 = UIFactory.CreateLabel(upPromptTxt, "UP_Prompt", LocalizedStr("gui_up_prompt"), TextAnchor.UpperCenter);
                    upPromptTxt.gameObject.SetActive(false);

                    var advMode = UIFactory.CreateHorizontalGroup(unityLoaderGUI, "advMode", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(advMode, minHeight: 25, flexibleHeight: 0);
                    AssignToGroups(advMode.gameObject, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.SetActive), () => true }
                });

                    GameObject advModeToggle = UIFactory.CreateToggle(advMode, "advModeToggle", out Toggle advToggle, out Text advToggle_t);
                    UIFactory.SetLayoutElement(advModeToggle, flexibleWidth: 9999);
                    advToggle_t.text = $"{LocalizedStr("gui_rl_advenced")}";
                    advToggle.isOn = false;

                    GameObject bottomGroup2 = UIFactory.CreateVerticalGroup(unityLoaderGUI, "bottomGroup", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    stats = UIFactory.CreateLabel(bottomGroup2, "stats", $"{LocalizedStr("gui_total_rounds")}: {roundNamesDrop.options.Count - 1} ({LocalizedStr("gui_deleted_rounds")}: {0}) | {LocalizedStr("gui_total_ids")}: {0}", TextAnchor.UpperCenter, default, true, 14);
                    //AssignToGroups(bottomGroup2.gameObject, new()
                    //{
                    //    { new GroupPolicy(ObjectGroup.Menu, ObjectInGroupPolicy.SetActive), () => true },
                    //    { new GroupPolicy(ObjectGroup.Loading, ObjectInGroupPolicy.SetActive), () => true },
                    //    { new GroupPolicy(ObjectGroup.Results, ObjectInGroupPolicy.SetActive), () => true },
                    //    { new GroupPolicy(ObjectGroup.Gameplay, ObjectInGroupPolicy.SetActive), () => true },
                    //    { new GroupPolicy(ObjectGroup.Explore, ObjectInGroupPolicy.SetActive), () => false }
                    //});

                    UIFactory.SetLayoutElement(stats.gameObject, preferredHeight: 100, flexibleHeight: 100, flexibleWidth: 9999);
                    //Text hotkeys = UIFactory.CreateLabel(bottomGroup2, "hotkeysHint", $"{LocalizedStr("gui_hotkeys", [ConfigManager.ToggleCusorHotkey.Value, ConfigManager.ToggleUIHotkey.Value, ConfigManager.DebugUIHotkey.Value, ConfigManager.FreeFlyModeHotkey.Value, ConfigManager.RespawnHotkey.Value, ConfigManager.CheckpointHotkey.Value, ConfigManager.ToggleFreeCamHotkey.Value, ConfigManager.ResetCheckpointHotkey.Value])}", TextAnchor.MiddleCenter, default, true, 14);
                    //UIFactory.SetLayoutElement(hotkeys.gameObject, preferredHeight: 100, flexibleHeight: 100, flexibleWidth: 9999);
                    var rl_desc = UIFactory.CreateLabel(bottomGroup2, "stats", $"{LocalizedStr("gui_rl_desc")}\n{LocalizedStr("gui_deleted_rounds_desc")}", TextAnchor.LowerCenter, default, true, 14);
                    UIFactory.SetLayoutElement(rl_desc.gameObject, preferredHeight: 100, flexibleHeight: 100, flexibleWidth: 9999);
                    advToggle.onValueChanged.AddListener((val) =>
                    {
                        if (val)
                        {
                            inputFieldRef.GameObject.SetActive(false);
                            printCms.GameObject.SetActive(true);
                            delList.GameObject.SetActive(true);
                            roundIdsDrop.gameObject.SetActive(false);
                            roundNamesDropUI.gameObject.SetActive(false);
                            advRoundInput.GameObject.gameObject.SetActive(true);
                            rl_desc.text = $"{LocalizedStr("gui_rla_desc")}";
                        }
                        else
                        {
                            inputFieldRef.GameObject.SetActive(true);
                            printCms.GameObject.SetActive(false);
                            delList.GameObject.SetActive(false);
                            roundNamesDropUI.gameObject.SetActive(true);
                            advRoundInput.GameObject.gameObject.SetActive(false);
                            rl_desc.text = $"{LocalizedStr("gui_rl_desc")}\n{LocalizedStr("gui_deleted_rounds_desc")}";
                        }
                    });
                    UIFactory.SetLayoutElement(stats.gameObject, minHeight: 25, flexibleHeight: 99);
                }));
                #endregion

                #region ROUND LOADER - GAMEPLAY
                TryDrawUI(() => FGTTargetSettings.RoundLoader, gameplayGUI, new(() =>
                {
                    GameObject title1 = UIFactory.CreateHorizontalGroup(gameplayGUI_Content, "variantSelector", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(title1, minHeight: 20, flexibleHeight: 0);
                    Text st22ats = UIFactory.CreateLabel(title1, "stats", LocalizedStr("gui_ingame_only_ui"), TextAnchor.UpperLeft, default, true, 14);
                    UIFactory.SetLayoutElement(st22ats.gameObject, minHeight: 20, preferredHeight: 0);

                    GameObject btnRow1 = UIFactory.CreateHorizontalGroup(gameplayGUI_Content, "ROW1", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(btnRow1, minHeight: 25, flexibleHeight: 0);

                    ButtonRef leave = UIFactory.CreateButton(btnRow1, "leaveToMenu", $"{LocalizedStr("gui_menu")}", null);
                    leave.OnClick += () => { LeaveMatchPopupManager.Instance.OnClose(true); };
                    UIFactory.SetLayoutElement(leave.GameObject, 30, 20, null, 0, null, null, null);
                    ButtonRef fixo = UIFactory.CreateButton(btnRow1, "fixObstacles", $"{LocalizedStr("gui_fix_obstacles")}", null);
                    fixo.OnClick += () => { StateManager.GetState<GameplayState>().Controller.FixObstacles(); };
                    UIFactory.SetLayoutElement(fixo.GameObject, 30, 20, null, 0, null, null, null);
                    randt = UIFactory.CreateButton(btnRow1, "guiRandTeam", $"{LocalizedStr("gui_move_team")}", null);
                    randt.OnClick += () => { FallGuyBehaviour._instance.UpdateTeam(); };
                    UIFactory.SetLayoutElement(randt.GameObject, 30, 20, null, 0, null, null, null);
                    randt.GameObject.SetActive(false);

                    GameObject btnRow2 = UIFactory.CreateHorizontalGroup(gameplayGUI_Content, "ROW2", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(btnRow2, minHeight: 25, flexibleHeight: 0);

                    ButtonRef randCosmetics = UIFactory.CreateButton(btnRow2, "ramdCos", $"{LocalizedStr("gui_handle_random_cosmetics")}", null);
                    randCosmetics.OnClick += InternalState.HandleRandomCosmetics;
                    UIFactory.SetLayoutElement(randCosmetics.GameObject, 30, 20, null, 0, null, null, null);
                    ButtonRef defaultCos = UIFactory.CreateButton(btnRow2, "defaultCos", $"{LocalizedStr("gui_handle_default_cosmetics")}", null);
                    defaultCos.OnClick += () =>
                    {
                        InternalState.ResetRandomCosmetics();
                    };
                    UIFactory.SetLayoutElement(defaultCos.GameObject, 30, 20, null, 0, null, null, null);

                    GameObject title2 = UIFactory.CreateHorizontalGroup(gameplayGUI_Content, "variantSelector", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(title2, minHeight: 20, flexibleHeight: 0);
                    Text st2ats = UIFactory.CreateLabel(title2, "stats", LocalizedStr("gui_variant_title"), TextAnchor.UpperLeft, default, true, 14);
                    UIFactory.SetLayoutElement(st2ats.gameObject, minHeight: 20, preferredHeight: 0);

                    GameObject variantSelector = UIFactory.CreateHorizontalGroup(gameplayGUI_Content, "variantSelector", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(variantSelector, minHeight: 25, flexibleHeight: 0);

                    var variants = UIFactory.CreateDropdown(variantSelector, "variants", out VariationsDropdown, LocalizedStr("dropdown_placeholder"), 14, null);
                    UIFactory.SetLayoutElement(variants, minWidth: 325, minHeight: 25, preferredWidth: 230);
                    var toggleBtn = UIFactory.CreateButton(variantSelector, "toggleBtn", LocalizedStr("gui_toggle"));
                    UIFactory.SetLayoutElement(toggleBtn.GameObject, minWidth: 60, minHeight: 25, preferredWidth: 170);


                    var ToggleVariant = UIFactory.CreateToggle(variantSelector, "Variant Toggle", out var Toggle, out var ToggleText, default, 25, 25);
                    Toggle.interactable = false;
                    Toggle.Set(false);
                    UIFactory.SetLayoutElement(toggleBtn.GameObject, minWidth: 25, minHeight: 25, preferredWidth: 170);

                    toggleBtn.OnClick += () =>
                    {
                        var Variant = AvailableVariations[VariationsDropdown.options[VariationsDropdown.value].text];
                        var Enabled = false;
                        if (Variant != null && Variant.SwitchableSetHolder != null)
                        {
                            Variant.SwitchableSetHolder.SetActive(!Variant.SwitchableSetHolder.activeSelf);
                            Enabled = Variant.SwitchableSetHolder.activeSelf;
                            Toggle.Set(Enabled);
                        }
                    };

                    VariationsDropdown.onValueChanged.AddListener(x =>
                    {
                        var Variant = AvailableVariations[VariationsDropdown.options[VariationsDropdown.value].text];
                        if (Variant.SwitchableSetHolder != null)
                            Toggle.Set(Variant.SwitchableSetHolder.activeSelf);
                        else Toggle.Set(false);
                    });

                    GameObject controlsVariant = UIFactory.CreateHorizontalGroup(gameplayGUI_Content, "controlsVariant", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(controlsVariant, minHeight: 25, flexibleHeight: 0);

                    var debugsave = UIFactory.CreateButton(controlsVariant, "debugsave", LocalizedStr("gui_save"));
                    debugsave.OnClick += () => SaveVariants();
                    UIFactory.SetLayoutElement(debugsave.GameObject, 30, 20, null, 0, null, null, null);
                    var lastsave = UIFactory.CreateButton(controlsVariant, "lastsave", LocalizedStr("gui_variant_lastsave"));
                    lastsave.OnClick += () => SetVariants();
                    UIFactory.SetLayoutElement(lastsave.GameObject, 30, 20, null, 0, null, null, null);

                    bool toggled = false;

                    var setall = UIFactory.CreateButton(controlsVariant, "setall", LocalizedStr("gui_variant_toggleall"));
                    setall.OnClick += () =>
                    {
                        toggled = !toggled;
                        foreach (var Switcher in UnityEngine.Object.FindObjectsOfType<SetSwitcher>())
                        {
                            foreach (var mapping in Switcher.SwitchableSetMappings)
                            {
                                if (mapping.SwitchableSetHolder != null)
                                    mapping.SwitchableSetHolder.gameObject.SetActive(toggled);
                            }
                        }
                    };
                    UIFactory.SetLayoutElement(toggleBtn.GameObject, minWidth: 60, minHeight: 25, preferredWidth: 170);
                }));
                #endregion

                #region ROUND LOADER - CREATIVE
                TryDrawUI(() => FGTTargetSettings.RoundLoader, fgcGUI, new(() =>
                {
#if DEV_BUILD
                string code = "4184-8071-9230";
#else
                    string code = "";
#endif
                    GameObject basicGroup = UIFactory.CreateVerticalGroup(fgcGUI, "basicGroup", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(basicGroup, minHeight: 25, flexibleHeight: 0);
                    GameObject fgcInput = UIFactory.CreateHorizontalGroup(basicGroup, "searchGroup", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(fgcInput, minHeight: 30, flexibleHeight: 0);

                    InputFieldRef fgcInputfield = UIFactory.CreateInputField(fgcInput, "searchInRoundNames", $"{LocalizedStr("code_input_holder")}");
                    fgcInputfield.Component.characterLimit = 14;
                    fgcInputfield.Component.text = code;
                    fgcInputfield.Component.onValueChanged.AddListener(x =>
                    {
                        code = CodeUtils.SanitizeCode(x, 3, 4, '-');
                        fgcInputfield.Component.text = code;
                        if (x.Length == 6 || x.Length == 10)
                            fgcInputfield.Component.MoveTextEnd(false);
                        if (x.Length == 14)
                        {
                            //StateManager.InternalState.shouldSkipErrors = true;
                            levelInfo.text = LocalizedStr("fgc_level_load");
                            FGTRoundLoader.GetOnlyLevelDto(x);
                        }
                    });
                    UIFactory.SetLayoutElement(fgcInputfield.Component.gameObject, minWidth: 100, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);

                    //InputFieldRef lvlVersion = UIFactory.CreateInputField(fgcInput, "ver", $"{0}");
                    //lvlVersion.Text = "0";
                    //lvlVersion.Component.characterLimit = 5;
                    //lvlVersion.OnValueChanged += input => { lvlVersion = int.Parse(input); };
                    //UIFactory.SetLayoutElement(lvlVersion.Component.previewTheme, minWidth: 20, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);

                    var loadingBtnsFGC = UIFactory.CreateHorizontalGroup(basicGroup, "loadingBtnsFGC", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    AssignToGroups(loadingBtnsFGC, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false },
                });

                    UIFactory.SetLayoutElement(loadingBtnsFGC, minHeight: 25, flexibleHeight: 0);
                    ButtonRef load = UIFactory.CreateButton(loadingBtnsFGC, "cancel", $"{LocalizedStr("gui_play")}");
                    load.OnClick += () =>
                    {
                        //FGT.shouldSkipErrors = false;
                        FGTRoundLoader.LoadFGCRound(code, null, true);
                    };

                    ButtonRef explorePlay = UIFactory.CreateButton(loadingBtnsFGC, "explore", $"{LocalizedStr("gui_play_explore")}");
                    explorePlay.OnClick += () =>
                    {
                        if (OnlineCheck != null && OnlineCheck.ExploreCodes != null && OnlineCheck.ExploreCodes.Count > 0)
                        {
                            DoModal(LocalizedStr("explore_start_title"), LocalizedStr("explore_start_desc") + "\n\n" + LocalizedStr("gui_explore_desc_base"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, new Action<bool>(OnClick), hideGUI: ModalHideGUIType.ShowOnCancel);
                            void OnClick(bool wasok)
                            {
                                if (wasok)
                                {
                                    StateManager.IsPlayingExploreFGC = true;
                                    _stateManager.TryJoinExplore(UltimatePartyState.JoinPolicy.FGC);
                                }
                            }
                        }
                        else
                            DoModal(LocalizedStr("gui_explore_error_title"), LocalizedStr("gui_explore_error_desc"), UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Default, hideGUI: ModalHideGUIType.KeepHiddenForThisModal);
                    };

                    GameObject levelInfoZone = UIFactory.CreateHorizontalGroup(basicGroup, "levelInfoZone", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    FGCLevelInfo = UIFactory.CreateScrollView(levelInfoZone, "FGCLevelInfo", out GameObject content, out AutoSliderScrollbar scrollBar, new(0.1f, 0.1f, 0.1f));
                    UIFactory.SetLayoutElement(FGCLevelInfo, flexibleHeight: 9999, minHeight: 180);
                    Transform settingsList = FGCLevelInfo.GetComponent<ScrollRect>().content.transform;
                    levelInfo = UIFactory.CreateLabel(settingsList.gameObject, "levelInfo", $"{LocalizedStr("gui_fgc_placeholder")}", TextAnchor.LowerLeft, default, true, 14);

                    fgcHistoryPrefab = UIFactory.CreateHorizontalGroup(basicGroup, "fgcHistoryPrefab", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    Text a = UIFactory.CreateLabel(fgcHistoryPrefab, "code", $"9999-9999-9999", TextAnchor.UpperLeft, default, true, 14);
                    ButtonRef playRound = UIFactory.CreateButton(fgcHistoryPrefab, $"play", $"{LocalizedStr("gui_play")}");
                    fgcHistoryPrefab.hideFlags = HideFlags.HideAndDontSave;
                    fgcHistoryPrefab.transform.SetParent(null, false);

                    GameObject FGCH = UIFactory.CreateScrollView(levelInfoZone, "FGCHistory", out FGCHistory, out AutoSliderScrollbar historyScrollbar, new(0.1f, 0.1f, 0.1f));
                    UIFactory.SetLayoutElement(FGCH, flexibleHeight: 9999, minHeight: 180);
                    LoadFGCHistory();

                    GameObject bottomGroup = UIFactory.CreateVerticalGroup(basicGroup, "bottomGroup", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    fgc_desc = UIFactory.CreateLabel(bottomGroup, "stats", "", TextAnchor.LowerCenter, default, true, 14);
                    UIFactory.SetLayoutElement(fgc_desc.gameObject, preferredHeight: 100, flexibleHeight: 100, flexibleWidth: 9999);
                }));
                #endregion

                #region ROUND LOADER - ROUND RULES
                TryDrawUI(() => FGTTargetSettings.RoundRules, loadOptionsGUI_Content, new(() =>
                {
                    var latestOptions = FGTServiceManager.GetService<RoundOptionsService>().ReturnLatestOptions();

                    GameObject PCOUNT_CONTENT = UIFactory.CreateHorizontalGroup(loadOptionsGUI_Content, "loadOptionsContent", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(PCOUNT_CONTENT, minHeight: 25, flexibleHeight: 9999, flexibleWidth: 9999);
                    GameObject PCOUNT_LeftGroup = UIFactory.CreateVerticalGroup(PCOUNT_CONTENT, "LeftGroup", false, false, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(PCOUNT_LeftGroup, minWidth: 40, preferredWidth: 40);
                    GameObject PCONUT_RightGroup = UIFactory.CreateVerticalGroup(PCOUNT_CONTENT, "RightGroup", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(PCONUT_RightGroup, minWidth: 40, preferredWidth: 40);
                    UIFactory.CreateLabel(PCOUNT_LeftGroup, "playerAmountTitle", LocalizedStr("gui_round_options_pcount"), TextAnchor.UpperLeft);
                    InputFieldRef playerAmountInput = UIFactory.CreateInputField(PCONUT_RightGroup, "playerAmountInput", LocalizedStr("gui_round_options_pcount_desc"));
                    playerAmountInput.Component.characterLimit = 2;
                    playerAmountInput.Component.contentType = InputField.ContentType.IntegerNumber;
                    playerAmountInput.Component.text = latestOptions.PlayerCount.ToString();
                    UIFactory.SetLayoutElement(playerAmountInput.GameObject, minWidth: 40, preferredWidth: 40, preferredHeight: 20, minHeight: 20);

                    GameObject ROUNDSEED_CONTENT = UIFactory.CreateHorizontalGroup(loadOptionsGUI_Content, "loadOptionsContent", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(ROUNDSEED_CONTENT, minHeight: 25, flexibleHeight: 9999, flexibleWidth: 9999);
                    GameObject SEED_LeftGroup = UIFactory.CreateVerticalGroup(ROUNDSEED_CONTENT, "LeftGroup", false, false, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(SEED_LeftGroup, minWidth: 40, preferredWidth: 40);
                    GameObject SEED_RightGroup = UIFactory.CreateVerticalGroup(ROUNDSEED_CONTENT, "RightGroup", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(SEED_RightGroup, minWidth: 40, preferredWidth: 40);
                    UIFactory.CreateLabel(SEED_LeftGroup, "playerAmountTitle", LocalizedStr("gui_round_options_seed"), TextAnchor.UpperLeft);
                    InputFieldRef seedInput = UIFactory.CreateInputField(SEED_RightGroup, "seedInput", LocalizedStr("gui_round_options_seed_desc"));
                    seedInput.Component.characterLimit = 12;
                    seedInput.Component.contentType = InputField.ContentType.IntegerNumber;
                    seedInput.Component.text = latestOptions.RoundSeed.ToString();
                    UIFactory.SetLayoutElement(seedInput.GameObject, minWidth: 40, preferredWidth: 40, preferredHeight: 20, minHeight: 20);

                    GameObject TIMERTOGGLE_CONTENT = UIFactory.CreateHorizontalGroup(loadOptionsGUI_Content, "loadOptionsContent", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(TIMERTOGGLE_CONTENT, minHeight: 25, flexibleHeight: 9999, flexibleWidth: 9999);
                    GameObject TIMERTOGGLE_LeftGroup = UIFactory.CreateVerticalGroup(TIMERTOGGLE_CONTENT, "LeftGroup", false, false, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(TIMERTOGGLE_LeftGroup, minWidth: 40, preferredWidth: 40);
                    GameObject TIMERTOGGLE_RightGroup = UIFactory.CreateVerticalGroup(TIMERTOGGLE_CONTENT, "RightGroup", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(TIMERTOGGLE_RightGroup, minWidth: 40, preferredWidth: 40);

                    UIFactory.CreateLabel(TIMERTOGGLE_LeftGroup, "playerAmountTitle", LocalizedStr("gui_round_options_timer_limit"), TextAnchor.UpperLeft);
                    UIFactory.CreateToggle(TIMERTOGGLE_RightGroup, "timeLimitToggle", out Toggle timeLimitToggle, out Text timeLimitToggle_txt);
                    timeLimitToggle_txt.text = LocalizedStr("gui_round_options_timer");
                    timeLimitToggle.isOn = latestOptions.TimeLimit;

                    InputFieldRef minInput = UIFactory.CreateInputField(TIMERTOGGLE_RightGroup, "minInput", LocalizedStr("gui_minutes"));
                    var min = latestOptions.TimeLimitLength / 60;
                    if (min > 99)
                        min = 99;

                    minInput.Component.characterLimit = 2;
                    minInput.Component.contentType = InputField.ContentType.IntegerNumber;
                    minInput.Component.text = min.ToString();
                    minInput.Component.onValueChanged.AddListener(x =>
                    {
                        if (int.Parse(x) > 99)
                            minInput.Component.text = "99";
                    });

                    UIFactory.SetLayoutElement(minInput.GameObject, minWidth: 40, preferredWidth: 40, preferredHeight: 20, minHeight: 20);
                    InputFieldRef secInput = UIFactory.CreateInputField(TIMERTOGGLE_RightGroup, "secInput", LocalizedStr("gui_seconds"));
                    var sec = latestOptions.TimeLimitLength % 60;
                    if (sec > 59)
                        sec = 59;

                    secInput.Component.characterLimit = 2;
                    secInput.Component.contentType = InputField.ContentType.IntegerNumber;
                    secInput.Component.text = sec.ToString();
                    secInput.Component.onValueChanged.AddListener(x =>
                    {
                        if (int.Parse(x) > 59)
                            secInput.Component.text = "59";
                    });
                    UIFactory.SetLayoutElement(secInput.GameObject, minWidth: 40, preferredWidth: 40, preferredHeight: 20, minHeight: 20);

                    UIFactory.CreateLabel(TIMERTOGGLE_RightGroup, "note", LocalizedStr("gui_round_options_timer_limit_desc"), TextAnchor.UpperLeft);

                    //Bottom
                    GameObject SaveBtn = UIFactory.CreateVerticalGroup(loadOptionsGUI_Content, "loadOptionsContent", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    ButtonRef loadOptions_SaveBtn = UIFactory.CreateButton(SaveBtn, "SaveBtn", LocalizedStr("gui_save_apply"));
                    loadOptions_SaveBtn.OnClick += () =>
                    {
                        int.TryParse(minInput.Text, out int MinInt);
                        int.TryParse(secInput.Text, out int SecInt);
                        int.TryParse(playerAmountInput.Text, out int PlayerAmount);
                        int.TryParse(seedInput.Text, out int RoundSeed);
                        latestOptions.TimeLimitLength = MinInt * 60 + SecInt;
                        latestOptions.TimeLimit = timeLimitToggle.isOn;
                        latestOptions.PlayerCount = PlayerAmount;
                        latestOptions.RoundSeed = RoundSeed;
                        FGTServiceManager.GetService<RoundOptionsService>().WriteData();
                    };
                    UIFactory.SetLayoutElement(loadOptions_SaveBtn.GameObject, minWidth: 120, preferredWidth: 9999, preferredHeight: 20, minHeight: 20);
                }));
                #endregion

                FGTRoundLoader.SetUIReferences([
                    roundNamesDrop,
                    roundVariantsDrop,
                    inputFieldRef,
                    stats,
                ]);
            }

            Text fgc_desc;
            List<GameObject> fgc_history = new();
            void LoadFGCHistory()
            {
                if (FGTServiceManager.GetService<StatisticsService>().currentStats != null && FGTServiceManager.GetService<StatisticsService>().currentStats.FGCSearchHistory != null)
                {
                    foreach (string code in FGTServiceManager.GetService<StatisticsService>().currentStats.FGCSearchHistory)
                    {
                        var a = UnityEngine.Object.Instantiate(fgcHistoryPrefab);
                        a.transform.GetChild(0).gameObject.GetComponent<Text>().text = code;
                        void b() => FGTRoundLoader.LoadFGCRound(code, null, true);
                        a.transform.GetChild(1).gameObject.GetComponent<Button>().onClick.AddListener(b);
                        a.transform.SetParent(FGCHistory.transform);
                        a.transform.SetSiblingIndex(0);
                        fgc_history.Add(a);
                    }
                }
            }

            public void RefreshFGCHistory(string newLevel)
            {
                var a = UnityEngine.Object.Instantiate(fgcHistoryPrefab);
                a.transform.GetChild(0).gameObject.GetComponent<Text>().text = newLevel;
                void b() => FGTRoundLoader.LoadFGCRound(newLevel, null, true);
                a.transform.GetChild(1).gameObject.GetComponent<Button>().onClick.AddListener(b);
                a.transform.SetParent(FGCHistory.transform);
                a.transform.SetSiblingIndex(0);
                fgc_history.Add(a);
            }

            public static string ParseLevelDTO(LevelInfoDto dto)
            {
                string outLine = string.Empty;
                if (dto == null)
                    return LocalizedStr("fgc_level_nodto");
                try
                {
                    var locale = CMSLoader.Instance._localisedStrings._localisedStrings;
                    string extraLine = $"\n\n<b>{LocalizedStr("fgc_level_extra").ToUpper()}</b>";

                    string gmType = locale["archetype_race"].ToUpper();
                    if (dto.GameModeId.Contains("survival"))
                        gmType = locale["archetype_survival"].ToUpper();
                    if (dto.GameModeId.Contains("points"))
                        gmType = locale["archetype_points"].ToUpper();

                    if (dto.Config.TryGetValue("qualification_percentage", out Il2CppSystem.Object qp) && qp != null)
                        extraLine += $"\n- {LocalizedStr("fgc_level_qp")}: {qp.TryCast<long>()}%";
                    if (dto.Config.TryGetValue("survival_percentage", out Il2CppSystem.Object sp) && sp != null)
                        extraLine += $"\n- {LocalizedStr("fgc_level_sp")}: {sp.TryCast<long>()}%";
                    if (dto.Config.TryGetValue("time_limit_seconds", out Il2CppSystem.Object tl) && tl != null)
                        extraLine += $"\n- {LocalizedStr("fgc_level_timel")}: {TimeSpan.FromSeconds(tl.TryCast<long>()):mm\\:ss}";
                    if (dto.Config.TryGetValue("ingame_music_soundtrack", out Il2CppSystem.Object ost) && ost != null)
                        extraLine += $"\n- {LocalizedStr("fgc_level_muz")}: {ost.TryCast<string>()}";
                    if (dto.Config.TryGetValue("ingame_theme", out Il2CppSystem.Object theme) && theme != null)
                        extraLine += $"\n- {LocalizedStr("fgc_level_theme")}: {theme.TryCast<string>()}";
                    if (dto.Tags != null && dto.Tags.Count > 0)
                        extraLine += $"\n- {LocalizedStr("fgc_level_tags")}: {string.Join(", ", dto.Tags)}";
                    if (dto.UserTags != null && dto.UserTags.Count > 0)
                        extraLine += $"\n- {LocalizedStr("fgc_level_utags")}: {string.Join(", ", dto.UserTags)}";

                    string compStat = dto.IsCompleted ? LocalizedStr("gui_yes") : LocalizedStr("gui_no");

                    outLine = $"<b>{LocalizedStr("fgc_level_inf")} - <color=grey><i>{dto.ShareCode}</i></color></b>\n\n- {LocalizedStr("fgc_level_name")}: {dto.Title}\n- {LocalizedStr("fgc_level_desc")}: {dto.Description}\n- {LocalizedStr("fgc_level_gamemode")}: {gmType}\n- {LocalizedStr("fgc_level_author")}: {dto.GetCreatorPlatformName(default)}\n- {LocalizedStr("fgc_level_ver")}: {dto.Version}\n- {LocalizedStr("fgc_level_compstat")}: {compStat}\n- {LocalizedStr("fgc_level_likes")}: {dto.LevelStats.LikesCount}\n- {LocalizedStr("fgc_level_pc")}: {dto.LevelStats.PlayCount}\n- {LocalizedStr("fgc_level_maxp")}: {dto.MaxPlayers}\n- {LocalizedStr("fgc_level_platform")}: {dto.Platform}";
                    outLine += extraLine;
                    return outLine;
                }
                catch
                {
                    return $"{LocalizedStr("failed_desc_new")}\n\nOutput:\n\n{outLine}";
                }
            }


            void TryToSetLang()
            {
                if (selectedLang != null && ConfigManager.LangFileName.Value != selectedLang)
                {
                    if (SceneManager.GetActiveScene().name == "MainMenu" && StateManager.FGTCurrentState == FGTStateManager.FGTState.Menu)
                    {
                        DoModal(LocalizedStr("gui_localization_act0"), LocalizedStr("gui_localization_act1"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, new Action<bool>((bool wasok) =>
                        {
                            if (wasok)
                            {
                                OnlineCheck.DownloadNewLang(selectedLang, new(() =>
                                {
                                    RefreshEverything(true);
                                    Instance.uiRoot.hideFlags = HideFlags.HideAndDontSave;
                                    Instance.uiRoot.transform.SetParent(null);
                                    Instance.uiRoot.gameObject.SetActive(false);
                                    Instance = null;
                                    StateManager.FirstTimeLogin = false;
                                    StateManager.GetState<MenuState>().menuComplete = false;
                                    FGTServiceManager.GetService<LocalizationService>().SetupLocalization(Path.Combine(Plugin.LocalizationDir, selectedLang, "locale.json"));
                                    ConfigManager.LangFileName.Value = selectedLang;
                                    StateManager.GetState<MenuState>().menuManager.ShowMainMenu(true, true, false);
                                }));
                            }
                        }), hideGUI: ModalHideGUIType.KeepHiddenForThisModal);
                    }
                    else
                        DoModal(LocalizedStr("gui_unavailable"), LocalizedStr("gui_localization_act2"), UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Default, hideGUI: ModalHideGUIType.KeepHidden);

                }
            }


            public static Dropdown VariationsDropdown;
            void DrawShowLoader()
            {
                FGTShowLoaderGUI = UIFactory.CreateVerticalGroup(ContentRoot, "ShowLoader", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(FGTShowLoaderGUI, minHeight: 25, flexibleHeight: 0);

                TryDrawUI(() => FGTTargetSettings.ShowLoader, FGTShowLoaderGUI, new(() =>
                {
                    var sL = FGTServiceManager.Instance.GetService<ShowLoaderService>();

                    GameObject showSearchbarGroup = UIFactory.CreateHorizontalGroup(FGTShowLoaderGUI, "searchGroup", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(showSearchbarGroup, minHeight: 30, flexibleHeight: 0);
                    InputFieldRef showSearchBar = UIFactory.CreateInputField(showSearchbarGroup, "searchInRoundNames", $"{LocalizedStr("gui_search_show")}");

                    GameObject showsDropGroup = UIFactory.CreateHorizontalGroup(FGTShowLoaderGUI, "showsDropGroup", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(showsDropGroup, minHeight: 30, flexibleHeight: 0);
                    GameObject showsDropUI = UIFactory.CreateDropdown(showsDropGroup, "showsDropUI", out Dropdown showListDrop, $"{LocalizedStr("dropdown_placeholder")}", 14, null, null);

                    GameObject showLoadBtns = UIFactory.CreateHorizontalGroup(FGTShowLoaderGUI, "loadingBtns", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(showLoadBtns, minHeight: 25, flexibleHeight: 0);
                    var showPlayBtn = UIFactory.CreateButton(showLoadBtns, "play", $"{LocalizedStr("gui_play")}", null);
                    AssignToGroups(showPlayBtn.GameObject, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.SetActive), () => false }
                });

                    UIFactory.SetLayoutElement(showPlayBtn.GameObject, 30, 20, null, 0, null, null, null);

                    var showInfoGroup = UIFactory.CreateHorizontalGroup(FGTShowLoaderGUI, "infoArea", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    var placeholder = UIFactory.CreateLabel(showInfoGroup, "ShowDesc", $"...", TextAnchor.UpperLeft, default, true, 14);
                    UIFactory.SetLayoutElement(placeholder.gameObject, minHeight: 25, flexibleHeight: 0, preferredWidth: 4);
                    UIFactory.SetLayoutElement(showInfoGroup, minHeight: 55, flexibleHeight: 0);

                    GameObject showInfoButtons = UIFactory.CreateVerticalGroup(showInfoGroup, "infoArea", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    ButtonRef viewImgBtn = UIFactory.CreateButton(showInfoButtons, "viewImg", $"{LocalizedStr("gui_show_image")}", null);
                    UIFactory.SetLayoutElement(viewImgBtn.GameObject, minHeight: 25, flexibleHeight: 0);
                    UIFactory.SetLayoutElement(showInfoButtons, minHeight: 25, flexibleHeight: 0);

                    var showRoundList = UIFactory.CreateHorizontalGroup(FGTShowLoaderGUI, "roundsArea", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    GameObject hell = UIFactory.CreateScrollView(showRoundList, "showRounds", out GameObject content, out AutoSliderScrollbar scrollBar, new(0.1f, 0.1f, 0.1f));
                    UIFactory.SetLayoutElement(hell, flexibleHeight: 9999, minHeight: 120);
                    Transform settingsList = hell.GetComponent<ScrollRect>().content.transform;
                    var showlist = UIFactory.CreateLabel(settingsList.gameObject, "showList", $"...", TextAnchor.LowerLeft, default, true, 14);
                    //UIFactory.SetLayoutElement(hotkeys.previewTheme, minHeight: 25, flexibleHeight: 0);
                    showInfoGroup.gameObject.SetActive(false);
                    showRoundList.gameObject.SetActive(false);

                    GameObject icoGrp = UIFactory.CreateVerticalGroup(showRoundList, "Image", false, false, true, true, 0, new Vector4(0, 0, 0, 0), childAlignment: TextAnchor.UpperLeft);
                    var showIco = UIFactory.CreateUIObject("gradient", icoGrp).AddComponent<Image>();
                    UIFactory.SetLayoutElement(showIco.gameObject, minHeight: 170, preferredHeight: 170, flexibleHeight: 170, flexibleWidth: 168, preferredWidth: 168, minWidth: 168);

                    var SLG_Gameplay = UIFactory.CreateHorizontalGroup(FGTShowLoaderGUI, "ingameUI", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    AssignToGroups(SLG_Gameplay, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => StateManager.ShowState != null },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.SetActive), () => false }
                });
                    UIFactory.SetLayoutElement(SLG_Gameplay, minHeight: 25, flexibleHeight: 0);
                    SLG_Gameplay.gameObject.SetActive(false);
                    var randomShow = UIFactory.CreateButton(SLG_Gameplay, "randomShow", $"{LocalizedStr("gui_random_round")}", null);
                    UIFactory.SetLayoutElement(randomShow.GameObject, 30, 20, null, 0, null, null, null);
                    var showInfo = UIFactory.CreateLabel(FGTShowLoaderGUI, "showInfo", $"{LocalizedStr("gui_loaded_show")}: ...", TextAnchor.LowerCenter, default, true, 14);
                    UIFactory.SetLayoutElement(showInfo.gameObject, minHeight: 25, flexibleHeight: 0);
                    GameObject placeholder42 = UIFactory.CreateLabel(FGTShowLoaderGUI, "ShowLoaderDesc", $"{LocalizedStr("gui_show_loader_desc")}", TextAnchor.LowerCenter, default, true, 14).gameObject;
                    UIFactory.SetLayoutElement(placeholder42.gameObject, preferredHeight: 1000, flexibleHeight: 9999, flexibleWidth: 9999);

                    sL.SetUIReferences([showListDrop, showSearchBar, showRoundList, showIco, randomShow, placeholder, showInfo, showInfoGroup, showlist, viewImgBtn, showPlayBtn]);
                }));
            }

#if LAN_MULTIPLAYER
            void DrawLocalMultiplayer()
            {
                var server = FGTServiceManager.GetService<LocalServerService>();
                string round2play = "round_gauntlet_01";

                FGTLocalMultiplayerGUI = UIFactory.CreateVerticalGroup(ContentRoot, "FGTLocalMultiplayer", true, true, true, true, 5, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(FGTLocalMultiplayerGUI, minHeight: 25, flexibleHeight: 0);


                #region LOCAL MULTIPLAYER DEV
                TryDrawUI(() => FGTTargetSettings.LocalMultiplayer, FGTLocalMultiplayerGUI, new(() =>
                {
                    var fields = UIFactory.CreateHorizontalGroup(FGTLocalMultiplayerGUI, "HostFields", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(fields, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                    var ipField = UIFactory.CreateInputField(fields, "ipField", LocalizedStr("lan_ip_field_holder"));
                    ipField.Text = "127.0.0.1";
                    ipField.Component.characterLimit = 15;
                    UIFactory.SetLayoutElement(ipField.GameObject, minHeight: 25, flexibleHeight: 25, flexibleWidth: 20, minWidth: 20, preferredWidth: 20);

                    var portField = UIFactory.CreateInputField(fields, "portField", LocalizedStr("lan_port_field_holder"));
                    portField.Text = "1002";
                    portField.Component.characterLimit = 4;
                    UIFactory.SetLayoutElement(portField.GameObject, minHeight: 25, flexibleHeight: 25, flexibleWidth: 20, minWidth: 20, preferredWidth: 20);

                    var lobbySize = UIFactory.CreateInputField(fields, "lobbySize", LocalizedStr("lan_players_field_holder"));
                    lobbySize.Text = "1";
                    lobbySize.Component.characterLimit = 2;
                    UIFactory.SetLayoutElement(lobbySize.GameObject, minHeight: 25, flexibleHeight: 25, flexibleWidth: 20, minWidth: 20, preferredWidth: 20);

                    var roundField = UIFactory.CreateInputField(FGTLocalMultiplayerGUI, "joinServ", $"id of round to play on");
                    roundField.Text = round2play;
                    roundField.OnValueChanged += (string s) =>
                    {
                        round2play = s;
                    };
                    UIFactory.SetLayoutElement(roundField.GameObject, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                    var actionButtons = UIFactory.CreateHorizontalGroup(FGTLocalMultiplayerGUI, "HostFields", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(actionButtons, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                    var hostServ = UIFactory.CreateButton(actionButtons, "hostServ", LocalizedStr("gui_server_host"));
                    hostServ.OnClick += () =>
                    {
                        if (!string.IsNullOrEmpty(ipField.Text) && !string.IsNullOrEmpty(portField.Text) && !string.IsNullOrEmpty(lobbySize.Text))
                            LocalServerService.Host(ipField.Text, Convert.ToInt32(portField.Text), Convert.ToInt32(lobbySize.Text), CMSLoader.Instance.CMSData.Rounds[round2play]);
                    };
                    UIFactory.SetLayoutElement(hostServ.GameObject, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                    var joinServ = UIFactory.CreateButton(actionButtons, "joinServ", LocalizedStr("gui_server_join"));
                    joinServ.OnClick += () =>
                    {
                        if (!string.IsNullOrEmpty(ipField.Text) && !string.IsNullOrEmpty(portField.Text))
                            LocalServerService.Join(ipField.Text, Convert.ToInt32(portField.Text));
                    };
                    UIFactory.SetLayoutElement(joinServ.GameObject, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                    var terminateServ = UIFactory.CreateButton(FGTLocalMultiplayerGUI, "joinServ", LocalizedStr("gui_server_shutdown"));
                    terminateServ.OnClick += () =>
                    {
                        server.ShutdownSerer(new(() =>
                        {

                        }));
                    };
                    UIFactory.SetLayoutElement(terminateServ.GameObject, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                    var allRounds = UIFactory.CreateButton(FGTLocalMultiplayerGUI, "allRounds", $"Print all rounds in console");
                    allRounds.OnClick += () =>
                    {
                        foreach (var r in CMSLoader.Instance.CMSData.Rounds)
                            FGTLog(LogLevel.Info, GetType(), $"{r.key} - {r.value.Archetype._name} - {r.value.GetSceneName()}");
                    };
                    UIFactory.SetLayoutElement(allRounds.GameObject, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);
                }));
                #endregion
            }
#endif

            GameObject mediaFGPosGrp;
            InputFieldRef posX;
            InputFieldRef posY;
            InputFieldRef posZ;

            InputFieldRef imgLocalLoad;
            InputFieldRef imgRemoteLoad;

            ButtonRef imagesBtn;
            GameObject mediaTab;
            void DrawMediaLoader()
            {
                FGTMediaGUI = UIFactory.CreateVerticalGroup(ContentRoot, "MediaTools", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(FGTMediaGUI, minHeight: 30, flexibleHeight: 0);

                GameObject mediaTabs = UIFactory.CreateHorizontalGroup(FGTMediaGUI, "Tabs", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(mediaTabs, minHeight: 25, flexibleHeight: 0);
                imagesBtn = UIFactory.CreateButton(mediaTabs, $"Button_Img", $"{LocalizedStr("gui_img_loader")}");

                TryDrawUI(() => FGTTargetSettings.MediaLoaderImages, FGTMediaGUI, new(() =>
                {
                    mediaTab = UIFactory.CreateVerticalGroup(FGTMediaGUI, "img", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                    UIFactory.SetLayoutElement(mediaTab, minHeight: 25, flexibleHeight: 0);
                    RuntimeHelper.SetColorBlock(imagesBtn.Component, UniversalUI.EnabledButtonColor, UniversalUI.EnabledButtonColor * 1.2f);

                    //IMG NAME INPUT
                    imgLocalLoad = UIFactory.CreateInputField(mediaTab, "imgField", $"{LocalizedStr("inputfield_placeholder")}");
                    imgLocalLoad.Text = FGTServiceManager.GetService<MediaService>().imgPath;
                    imgLocalLoad.OnValueChanged += input =>
                    {
                        FGTServiceManager.GetService<MediaService>().imgPath = input;
                    };
                    UIFactory.SetLayoutElement(imgLocalLoad.GameObject, 30, 25, null, 0, null, null, null);
                    imgRemoteLoad = UIFactory.CreateInputField(mediaTab, "imgField_web", $"{LocalizedStr("inputfield_placeholder")}");
                    imgRemoteLoad.Text = FGTServiceManager.GetService<MediaService>().url;
                    imgRemoteLoad.OnValueChanged += input =>
                    {
                        FGTServiceManager.GetService<MediaService>().url = input;
                    };
                    UIFactory.SetLayoutElement(imgRemoteLoad.GameObject, 30, 25, null, 0, null, null, null);
                    imgRemoteLoad.GameObject.SetActive(false);


                    //BTN ACTS
                    GameObject btnActs = UIFactory.CreateHorizontalGroup(mediaTab, "btnActs", true, true, true, true, 2, new Vector4(2, 2, 2, 2));

                    ButtonRef loadBtn = UIFactory.CreateButton(btnActs, "loadBtn", $"{LocalizedStr("gui_media_load")}", new Color(0.2f, 0.3f, 0.2f));
                    loadBtn.OnClick += () =>
                    {
                        if (!FGTServiceManager.GetService<MediaService>().urlLoad)
                            CoroutineRunner.Instance.StartCoroutine(FGTServiceManager.GetService<MediaService>().LoadImage(Plugin.ImgDir + FGTServiceManager.GetService<MediaService>().imgPath).WrapToIl2Cpp());
                        else
                            CoroutineRunner.Instance.StartCoroutine(FGTServiceManager.GetService<MediaService>().LoadImage(FGTServiceManager.GetService<MediaService>().url).WrapToIl2Cpp());
                    };
                    UIFactory.SetLayoutElement(loadBtn.GameObject, 30, 25, null, 0, null, null, null);
                    ButtonRef dirBtn = UIFactory.CreateButton(btnActs, "dirBtn", $"{LocalizedStr("gui_folder")}", null);
                    dirBtn.OnClick += () =>
                    {
                        Application.OpenURL(Plugin.ImgDir);
                    };
                    UIFactory.SetLayoutElement(dirBtn.GameObject, 30, 25, null, 0, null, null, null);
                    ButtonRef destAllBtn = UIFactory.CreateButton(btnActs, "destAllBtn", $"{LocalizedStr("gui_destroy_all")}", GUIRed);
                    destAllBtn.OnClick += () =>
                    {
                        void pop(bool wasOk)
                        {
                            if (wasOk)
                            {
                                foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
                                {
                                    if (obj.name.StartsWith("Loaded Image"))
                                        UnityEngine.Object.Destroy(obj.gameObject);
                                }
                            }
                        }
                        AreYouSurePopup($"{LocalizedStr("gui_del_images_act")}", popAct: new Action<bool>(pop));
                    };
                    UIFactory.SetLayoutElement(destAllBtn.GameObject, 30, 25, null, 0, null, null, null);

                    GameObject btnActsRow2 = UIFactory.CreateHorizontalGroup(mediaTab, "btnActsRow2", true, true, true, true, 2, new Vector4(2, 2, 2, 2));

                    ButtonRef flzRuImg = UIFactory.CreateButton(btnActsRow2, "flzruImg", $"{LocalizedStr("gui_flzru_img")}", null);
                    flzRuImg.OnClick += () =>
                    {
                        var oS = OnlineCheck;
                        var randConfig = oS.FGTContent.RandomImages;

                        if (!oS.FGTContent.RandomImages.Enabled)
                            return;

                        string url;
                        string urlBase = oS.FGTContent.RandomImages.Url;
                        if (randConfig.TotalImages != -1)
                        {
                            int randVal = UnityEngine.Random.Range(0, randConfig.TotalImages);
                            if (randConfig.BannedImages != null && randConfig.BannedImages.Contains(randVal))
                                randVal = randConfig.Fallback;
                            url = $"{urlBase}{randVal}.png";
                        }
                        else
                            url = $"{urlBase}_146.png";

                        CoroutineRunner.Instance.StartCoroutine(FGTServiceManager.GetService<MediaService>().LoadImage(url, false, true).WrapToIl2Cpp());
                    };
                    UIFactory.SetLayoutElement(flzRuImg.GameObject, 30, 25, null, 0, null, null, null);

                    GameObject t = UIFactory.CreateHorizontalGroup(mediaTab, "t", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                    GameObject asCube = UIFactory.CreateToggle(t, "asCube", out Toggle asCube_toggle, out Text asCube_t);
                    UIFactory.SetLayoutElement(asCube.gameObject, minWidth: 170, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);
                    asCube_t.text = $"{LocalizedStr("gui_as_cube")}";
                    asCube_toggle.isOn = false;
                    asCube_toggle.onValueChanged.AddListener((val) => { FGTServiceManager.GetService<MediaService>().asCube = val; });
                    GameObject nearFG = UIFactory.CreateToggle(t, "nearFG", out Toggle nearFG_toggle, out Text nearFG_t);
                    UIFactory.SetLayoutElement(nearFG, flexibleWidth: 9999);
                    nearFG_t.text = $"{LocalizedStr("gui_near_fg")}";
                    nearFG_toggle.isOn = true;
                    nearFG_toggle.onValueChanged.AddListener((val) => { FGTServiceManager.GetService<MediaService>().imgLoadNearFG = val; });
                    GameObject viaURL = UIFactory.CreateToggle(t, "viaURL", out Toggle viaURL_toggle, out Text viaURL_t);
                    UIFactory.SetLayoutElement(viaURL, flexibleWidth: 9999);
                    viaURL_t.text = $"{LocalizedStr("gui_url_load")}";
                    viaURL_toggle.isOn = false;
                    viaURL_toggle.onValueChanged.AddListener((val) => { FGTServiceManager.GetService<MediaService>().urlLoad = val; });

                    mediaFGPosGrp = UIFactory.CreateVerticalGroup(mediaTab, "mediaFGPosGrp", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                    posX = UIFactory.CreateInputField(mediaFGPosGrp, "X", $"{LocalizedStr("inputfield_placeholder")}");
                    posX.Text = FGTServiceManager.GetService<MediaService>().transX.ToString();
                    posX.OnValueChanged += input =>
                    {
                        if (float.TryParse(input, out float x))
                            FGTServiceManager.GetService<MediaService>().transX = x;
                    };
                    UIFactory.SetLayoutElement(posX.Component.gameObject, 30, 25, null, 0, null, null, null);
                    posY = UIFactory.CreateInputField(mediaFGPosGrp, "Y", $"{LocalizedStr("inputfield_placeholder")}");
                    posY.Text = FGTServiceManager.GetService<MediaService>().transY.ToString();
                    posY.OnValueChanged += input =>
                    {
                        if (float.TryParse(input, out float y))
                            FGTServiceManager.GetService<MediaService>().transY = y;
                    };
                    UIFactory.SetLayoutElement(posY.Component.gameObject, 30, 25, null, 0, null, null, null);
                    posZ = UIFactory.CreateInputField(mediaFGPosGrp, "Z", $"{LocalizedStr("inputfield_placeholder")}");
                    posZ.Text = FGTServiceManager.GetService<MediaService>().transZ.ToString();
                    posZ.OnValueChanged += input =>
                    {
                        if (float.TryParse(input, out float z))
                            FGTServiceManager.GetService<MediaService>().transZ = z;
                    };
                    UIFactory.SetLayoutElement(posZ.Component.gameObject, 30, 25, null, 0, null, null, null);

                    GameObject followFgPos = UIFactory.CreateToggle(mediaFGPosGrp, "followFgPos", out Toggle followFgPos_toggle, out Text followFgPos_t);
                    UIFactory.SetLayoutElement(followFgPos.gameObject, minWidth: 170, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);
                    followFgPos_t.text = $"{LocalizedStr("gui_follow_fg_pos")}";
                    followFgPos_toggle.isOn = true;
                    followFgPos_toggle.onValueChanged.AddListener((val) => { FGTServiceManager.GetService<MediaService>().followFGPos = val; });

                    Text showInfo = UIFactory.CreateLabel(mediaFGPosGrp, "desc", $"X/Y/Z", TextAnchor.LowerCenter, default, true, 14);
                    UIFactory.SetLayoutElement(showInfo.gameObject, minHeight: 25, flexibleHeight: 0);
                    mediaFGPosGrp.gameObject.SetActive(false);

                    Text desc = UIFactory.CreateLabel(mediaTab, "desc", $"\n{LocalizedStr("gui_imgloader_desc")}\n\n - {LocalizedStr("gui_imgloader_desc_01")}\n - {LocalizedStr("gui_imgloader_desc_02")}", TextAnchor.LowerCenter, default, true, 14);
                    //UIFactory.SetLayoutElement(desc.previewTheme, minHeight: 25, flexibleHeight: 0);
                }));

            }
            void DrawIMG2FGC()
            {
                FGTImg2FGCGUI = UIFactory.CreateVerticalGroup(ContentRoot, "IMG2FGC", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(FGTImg2FGCGUI, minHeight: 25, flexibleHeight: 0);

                TryDrawUI(() => FGTTargetSettings.Img2Fgc, FGTImg2FGCGUI, new(() =>
                {
                    //image input file name area
                    GameObject img2fgc_imageName = UIFactory.CreateHorizontalGroup(FGTImg2FGCGUI, "imageNameGroup", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    Text img2fgc_imgName = UIFactory.CreateLabel(img2fgc_imageName, "imgNameHint", $"{LocalizedStr("gui_img2fgc_img_name")}", TextAnchor.UpperLeft, default, true, 14);
                    UIFactory.SetLayoutElement(img2fgc_imgName.gameObject, minWidth: 110, flexibleWidth: 0);
                    InputFieldRef img2fgcInput = UIFactory.CreateInputField(img2fgc_imageName, "imageNameInput", $"{LocalizedStr("inputfield_placeholder")}");
                    img2fgcInput.Text = FGTServiceManager.GetService<MediaService>().imgPath;
                    img2fgcInput.OnValueChanged += input => { FGTServiceManager.GetService<MediaService>().imgPath = input; };
                    UIFactory.SetLayoutElement(img2fgcInput.UIRoot, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

                    ButtonRef imagesFolderBtn = UIFactory.CreateButton(img2fgc_imageName, "openImgFolder", $"{LocalizedStr("gui_folder")}", null);
                    imagesFolderBtn.OnClick += () => { Application.OpenURL(Plugin.ImgDir); };
                    //UIFactory.SetLayoutElement(imagesFolderBtn.GameObject, minHeight: 25, flexibleHeight: 0);
                    UIFactory.SetLayoutElement(imagesFolderBtn.Component.gameObject, minWidth: 100, minHeight: 25, flexibleWidth: 0, flexibleHeight: 0);

                    //width height area
                    GameObject img2fgc_imagescale = UIFactory.CreateHorizontalGroup(FGTImg2FGCGUI, "imageNameGroup", false, true, true, true, 2, new Vector4(2, 2, 2, 2));
                    UIFactory.SetLayoutElement(img2fgc_imagescale, minHeight: 25, flexibleHeight: 0);
                    Text img2fgc_wh = UIFactory.CreateLabel(img2fgc_imagescale, "imgWhHint", $"{LocalizedStr("gui_img2fgc_wh")}", TextAnchor.UpperLeft, default, true, 14);
                    UIFactory.SetLayoutElement(img2fgc_wh.gameObject, minWidth: 110, flexibleWidth: 0);
                    InputFieldRef img2fgc_width = UIFactory.CreateInputField(img2fgc_imagescale, "imageNameInput", $"{LocalizedStr("inputfield_placeholder")}");
                    img2fgc_width.Text = imgWidth;
                    img2fgc_width.Component.characterLimit = 5;
                    img2fgc_width.OnValueChanged += input => { imgWidth = input; };
                    UIFactory.SetLayoutElement(img2fgc_width.Component.gameObject, minWidth: 100, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);
                    InputFieldRef img2fgc_height = UIFactory.CreateInputField(img2fgc_imagescale, "imageNameInput", $"{LocalizedStr("inputfield_placeholder")}");
                    img2fgc_height.Text = imgHeight;
                    img2fgc_height.Component.characterLimit = 5;
                    img2fgc_height.OnValueChanged += input => { imgHeight = input; };
                    UIFactory.SetLayoutElement(img2fgc_height.Component.gameObject, minWidth: 170, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);

                    GameObject img2fgc_bools = UIFactory.CreateHorizontalGroup(FGTImg2FGCGUI, "img2fgcBools", false, true, true, true, 2, new Vector4(2, 2, 2, 2));
                    UIFactory.SetLayoutElement(img2fgc_bools, minHeight: 25, flexibleHeight: 0);
                    GameObject img2fgc_sdbt = UIFactory.CreateToggle(img2fgc_bools, "img2fgc_sdbt", out Toggle img2fgc_sdbtToggle, out Text img2fgc_sdbtToggle_t);
                    UIFactory.SetLayoutElement(img2fgc_sdbt.gameObject, minWidth: 170, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);
                    img2fgc_sdbtToggle_t.text = $"{LocalizedStr("gui_img2fgc_should_del_black")}";
                    img2fgc_sdbtToggle.isOn = false;
                    img2fgc_sdbtToggle.onValueChanged.AddListener((val) => { shouldDeleteBlackPixels = val; });
                    GameObject img2fgc_sdwt = UIFactory.CreateToggle(img2fgc_bools, "img2fgc_sdwt", out Toggle img2fgc_sdwtToggle, out Text img2fgc_sdwtToggle_t);
                    UIFactory.SetLayoutElement(img2fgc_sdwt, flexibleWidth: 9999);
                    img2fgc_sdwtToggle_t.text = $"{LocalizedStr("gui_img2fgc_should_del_white")}";
                    img2fgc_sdwtToggle.isOn = false;
                    img2fgc_sdwtToggle.onValueChanged.AddListener((val) => { shouldDeleteWhitePixels = val; });
                    GameObject img2fgc_dt = UIFactory.CreateToggle(img2fgc_bools, "img2fgc_dt", out Toggle img2fgc_dtToggle, out Text img2fgc_dtToggle_t);
                    UIFactory.SetLayoutElement(img2fgc_dt, flexibleWidth: 9999);
                    img2fgc_dtToggle_t.text = $"{LocalizedStr("gui_img2fgc_digital")}";
                    img2fgc_dtToggle.isOn = false;
                    img2fgc_dtToggle.onValueChanged.AddListener((val) => { isDigital = val; });

                    GameObject img2fgc_actions = UIFactory.CreateHorizontalGroup(FGTImg2FGCGUI, "img2fgcActions", false, true, true, true, 2, new Vector4(2, 2, 2, 2));
                    UIFactory.SetLayoutElement(img2fgc_actions, minHeight: 25, flexibleHeight: 0);
                    ButtonRef genLevel = UIFactory.CreateButton(img2fgc_actions, "genLevel", $"{LocalizedStr("gui_img2fgc_gen_level")}", null);
                    genLevel.OnClick += () => { img2fgcAlert(); };
                    UIFactory.SetLayoutElement(genLevel.GameObject, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);
                    ButtonRef repAll = UIFactory.CreateButton(img2fgc_actions, "repAll", $"{LocalizedStr("gui_img2fgc_replace")}", GUIRed);
                    repAll.OnClick += () =>
                    {
                        if (haveGeneratedLevel)
                        {
                            foreach (LevelBrowserTileViewModel LBTDVM in Resources.FindObjectsOfTypeAll<LevelBrowserTileViewModel>())
                            {
                                if (LBTDVM != null && LBTDVM.TileData != null && LBTDVM.TileData.level != null && LBTDVM.TileData.level._levelJSON != null && LBTDVM.TileData.level._levelJSON._url != null)
                                    LBTDVM.TileData.level._levelJSON._url = "file://" + Path.Combine(Application.persistentDataPath, "Img2FGC.json");
                            }
                        }
                        else
                            ErrorPopup($"{LocalizedStr("img2fgc_uhh")}");
                    };
                    UIFactory.SetLayoutElement(repAll.GameObject, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);
                    GameObject imageViewport = UIFactory.CreateVerticalGroup(FGTImg2FGCGUI, "ImageViewport", false, false, true, true, bgColor: new(1, 1, 1, 0), childAlignment: TextAnchor.MiddleCenter);
                    UIFactory.SetLayoutElement(imageViewport, flexibleWidth: 9999, flexibleHeight: 9999);

                    GameObject imageHolder = UIFactory.CreateUIObject("ImageHolder", imageViewport);
                    UnityEngine.UI.LayoutElement imageLayout = UIFactory.SetLayoutElement(imageHolder, 356, 170, 0, 0);

                    GameObject actualImageObj = UIFactory.CreateUIObject("ActualImage", imageHolder);
                    RectTransform actualRect = actualImageObj.GetComponent<RectTransform>();
                    actualRect.anchorMin = new(0, 0);
                    actualRect.anchorMax = new(1, 1);
                    actualImageObj.AddComponent<Image>().sprite = SetSpriteFromFile(Plugin.AssetsDir + "obedguyslore.png", 356, 170);
                    GameObject img2fgc_about = UIFactory.CreateLabel(FGTImg2FGCGUI, "img2fgc_about", $"{LocalizedStr("gui_img2fgc_desc")}\n{LocalizedStr("gui_img2fgc_credits")}", TextAnchor.LowerCenter, default, true, 14).gameObject;
                    UIFactory.SetLayoutElement(img2fgc_about.gameObject, preferredHeight: 1000, flexibleHeight: 9999, flexibleWidth: 9999);
                }));
            }

            Text statistics;
            public Text RoundHistoryTXT;
            public Text DisplayInfo;
            public GameObject roundHistory;
            public GameObject HistoryActions = null;
            public ButtonRef HistoryPlus;
            public ButtonRef HistoryMinus;
            Text autosaveInfo;
            ButtonRef toggleHistory;

            Dropdown langDropdown;

            Dropdown SavedLevels;
            Dropdown LevelSaves;
            void DrawFGCAutosaves()
            {
                var saves = FGTServiceManager.GetService<FGC_LocalSavesService>();
                FGTAutosavesGUI = UIFactory.CreateVerticalGroup(ContentRoot, "Autosaves", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(FGTAutosavesGUI, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);

                TryDrawUI(() => FGTTargetSettings.FGCLocalSaves, FGTAutosavesGUI, new(() =>
                {

                    var nosaves = UIFactory.CreateLabel(FGTAutosavesGUI, "nosaves", $"{LocalizedStr("gui_local_save_no_saves")}", TextAnchor.UpperCenter, default, true, 14);
                    UIFactory.SetLayoutElement(nosaves.gameObject, 0, 20);

                    GameObject dropdowns = UIFactory.CreateHorizontalGroup(FGTAutosavesGUI, "dropdowns", true, false, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);

                    //levels
                    GameObject savedLevels = UIFactory.CreateDropdown(dropdowns, "savedLevels", out SavedLevels, $"{LocalizedStr("dropdown_placeholder")}", 14, saves.OnLevelSelect, null);
                    UIFactory.SetLayoutElement(savedLevels, minHeight: 25, flexibleHeight: 0);

                    //saves
                    GameObject levelSaves = UIFactory.CreateDropdown(dropdowns, "levelSaves", out LevelSaves, $"{LocalizedStr("dropdown_placeholder")}", 14, saves.OnSaveSelect, null);
                    UIFactory.SetLayoutElement(levelSaves, minHeight: 25, flexibleHeight: 0);

                    UIFactory.SetLayoutElement(dropdowns, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                    GameObject loadBtnGroup = UIFactory.CreateHorizontalGroup(FGTAutosavesGUI, "btnGroup", true, false, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    var loadbtn = UIFactory.CreateButton(loadBtnGroup, "LoadSave", LocalizedStr("gui_local_save_load"));
                    loadbtn.OnClick = () => { saves.TryToLoadSelectedSave(); };
                    UIFactory.SetLayoutElement(loadbtn.GameObject, minHeight: 25, flexibleHeight: 0);
                    UIFactory.SetLayoutElement(loadBtnGroup, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);


                    GameObject SaveInfoGroup = UIFactory.CreateHorizontalGroup(FGTAutosavesGUI, "SaveInfoGroup", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);

                    GameObject hell = UIFactory.CreateScrollView(SaveInfoGroup, "showRounds", out GameObject content, out AutoSliderScrollbar scrollBar, new(0.1f, 0.1f, 0.1f));
                    UIFactory.SetLayoutElement(hell, flexibleHeight: 9999, minHeight: 120);
                    Transform settingsList = hell.GetComponent<ScrollRect>().content.transform;
                    Text showlist = UIFactory.CreateLabel(settingsList.gameObject, "saveInfo", $"{LocalizedStr("gui_local_save_info_placeholder")}", TextAnchor.LowerLeft, default, true, 14);

                    GameObject icoGrp = UIFactory.CreateVerticalGroup(SaveInfoGroup, "Image", false, false, true, true, 0, new Vector4(0, 0, 0, 0), childAlignment: TextAnchor.UpperLeft);
                    Image showIco = UIFactory.CreateUIObject("gradient", icoGrp).AddComponent<Image>();
                    UIFactory.SetLayoutElement(showIco.gameObject, minHeight: 170, preferredHeight: 170, flexibleHeight: 170, flexibleWidth: 270, preferredWidth: 270, minWidth: 270);

                    GameObject genericActions = UIFactory.CreateHorizontalGroup(FGTAutosavesGUI, "genericActions", true, false, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    var newSave = UIFactory.CreateButton(genericActions, "newSave", LocalizedStr("gui_local_save_create"));
                    newSave.OnClick = () => { saves.TryAutosaveLevel(); };
                    UIFactory.SetLayoutElement(newSave.GameObject, minHeight: 25, flexibleHeight: 0);

                    var delAll = UIFactory.CreateButton(genericActions, "delAll", LocalizedStr("gui_local_save_del_all"), GUIRed);
                    delAll.OnClick = () => { saves.TryDeleteEverything(); };
                    UIFactory.SetLayoutElement(delAll.GameObject, minHeight: 25, flexibleHeight: 0);

                    UIFactory.SetLayoutElement(genericActions, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                    GameObject saveActions = UIFactory.CreateHorizontalGroup(FGTAutosavesGUI, "saveActions", true, false, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    var delLevel = UIFactory.CreateButton(saveActions, "delLevel", LocalizedStr("gui_local_save_del_level"), GUIRed);
                    delLevel.OnClick = () => { saves.TryDeleteLevel(); };
                    UIFactory.SetLayoutElement(delLevel.GameObject, minHeight: 25, flexibleHeight: 0);

                    var delSave = UIFactory.CreateButton(saveActions, "delSave", LocalizedStr("gui_local_save_del_save"), GUIRed);
                    delSave.OnClick = () => { saves.TryDeleteSave(); };
                    UIFactory.SetLayoutElement(delSave.GameObject, minHeight: 25, flexibleHeight: 0);

                    UIFactory.SetLayoutElement(saveActions, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                    saves.SetUIReferences([SavedLevels,
                    LevelSaves,
                    showIco,
                    showlist,
                    SaveInfoGroup,
                    loadBtnGroup,
                    genericActions,
                    newSave.Component,
                    nosaves,
                    dropdowns,
                    saveActions,
                    delLevel.Component,
                    delSave.Component]);

                    GameObject placeholder42 = UIFactory.CreateLabel(FGTAutosavesGUI, "ShowLoaderDesc", $"{LocalizedStr("gui_fgc_local_autosaves_desc")}", TextAnchor.LowerCenter, default, true, 14).gameObject;
                    UIFactory.SetLayoutElement(placeholder42.gameObject, preferredHeight: 9999, flexibleHeight: 200, flexibleWidth: 200);
                }));
            }
            void DrawMisc()
            {
                FGTMiscGUI = UIFactory.CreateVerticalGroup(ContentRoot, "MiscGroup", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(FGTMiscGUI, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);

                GameObject scrollview = UIFactory.CreateScrollView(FGTMiscGUI, "MiscGUI", out GameObject MiscContent, out _, new(0.1f, 0.1f, 0.1f));
                MiscContent.GetComponent<VerticalLayoutGroup>().spacing = 2;

                #region THEMES
                var tS = FGTServiceManager.GetService<MenuThemeService>();
                Text themesTitle = UIFactory.CreateLabel(MiscContent, "ThemesTitle", LocalizedStr("themes_selector_title"), TextAnchor.UpperCenter);
                UIFactory.SetLayoutElement(themesTitle.gameObject, minHeight: 20);

                Text themesDisabled = UIFactory.CreateLabel(MiscContent, "ThemesTitle_Disabled", LocalizedStr("gui_only_in_menu"), TextAnchor.UpperCenter);
                UIFactory.SetLayoutElement(themesDisabled.gameObject, minHeight: 20);
                themesDisabled.gameObject.SetActive(false);

                var linearGraidentImage = Resources.FindObjectsOfTypeAll<Sprite>().ToList().Find(x => x.name == "UI_LinearGradient_Image");
                Theme theme = null;
                Color placeholderCol = Color.magenta;
                if (ConfigManager.InGameTheme.Value != LocalizedStr("gui_default"))
                    theme = tS.CurrentTheme;

                TryDrawUI(() => FGTTargetSettings.CustomThemes, MiscContent, new(() =>
                {
                    var holder = UIFactory.CreateUIObject("ThemeSwitcher", MiscContent);
                    var vert = holder.AddComponent<VerticalLayoutGroup>();
                    vert.spacing = 2;
                    vert.padding.bottom = 5;

                    AssignToGroups(holder, new()
                    {
                        { new GroupPolicy(ObjectGroup.Editor, GroupOperation.SetActive), () => false },
                        { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                        { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => false },
                        { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    }, new((ObjectGroup newGroup) =>
                    {
                        themesDisabled.gameObject.SetActive(newGroup != ObjectGroup.Menu);
                    }));

                    GameObject selectionRow = UIFactory.CreateHorizontalGroup(holder, "Selection Row", true, false, true, true, 2, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
                    UIFactory.CreateDropdown(selectionRow, "Themes", out var themesDropdown, "", 14, tS.PreviewTheme);
                    UIFactory.CreateDropdown(selectionRow, "WebThemes", out var webthemesDropdown, "", 14, tS.PickOnlineTheme);
                    UIFactory.SetLayoutElement(themesDropdown.gameObject, 150, 25, 150, 0, 99999);
                    UIFactory.SetLayoutElement(webthemesDropdown.gameObject, 150, 25, 150, 0, 99999);
                    webthemesDropdown.gameObject.SetActive(false);

                    ButtonRef selectButton = UIFactory.CreateButton(selectionRow, "Select Button", LocalizedStr("gui_select"));
                    UIFactory.SetLayoutElement(selectButton.Component.gameObject, 100, 25, 100, 0, 99999);

                    var selectWebTheme = UIFactory.CreateButton(selectionRow, "selectWebTheme", LocalizedStr("gui_download"));
                    UIFactory.SetLayoutElement(selectWebTheme.Component.gameObject, 100, 25, 100, 0, 99999);

                    ButtonRef catalogueHelp = UIFactory.CreateButton(selectionRow, "Help", "?");
                    UIFactory.SetLayoutElement(catalogueHelp.Component.gameObject, 30, 25, 30, 0, 30);
                    catalogueHelp.OnClick = () => { DoModal(LocalizedStr("gui_themes_catalogue_faq_title"), LocalizedStr("gui_themes_catalogue_faq_desc"), UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Default, btnOkStr: LocalizedStr("gui_btn_got_it"), hideGUI: ModalHideGUIType.KeepHiddenForThisModal); };

                    selectButton.OnClick += () => { FGTServiceManager.GetService<MenuThemeService>().SelectTheme(true); };
                    selectWebTheme.OnClick += tS.startDownloading;
                    selectWebTheme.Component.gameObject.SetActive(false);
                    catalogueHelp.GameObject.SetActive(false);

                    Il2CppSystem.Collections.Generic.List<string> webThemes = new();
                    List<string> WebThemeIds = new();
                    webThemes.Add(LocalizedStr("dropdown_placeholder"));

                    if (FGTTargetSettings.ThemeBrowserOnline)
                    {
                        if (OnlineCheck.FGTContent.ThemeData != null && OnlineCheck.FGTContent.ThemeData.Count > 0)
                        {
                            foreach (var webtheme in OnlineCheck.FGTContent.ThemeData)
                            {
                                WebThemeIds.Add(webtheme.Value.Id);

                                if (webtheme.Value.DisplayName != null)
                                    webThemes.Add(webtheme.Value.DisplayName);
                                else
                                    webThemes.Add(webtheme.Value.FolderName);
                            }
                        }
                        else
                            webThemes.Add("gui_themes_catalogue_empty");
                    }

                    webthemesDropdown.AddOptions(webThemes);

                    GameObject previewTheme = UIFactory.CreateVerticalGroup(holder, "ThemePreview", false, false, true, true, 0, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
                    previewTheme.AddComponent<RectMask2D>();

                    Color rowCol = theme != null ? new Color(theme.UpperGradientRGBA[0], theme.UpperGradientRGBA[1], theme.UpperGradientRGBA[2], theme.UpperGradientRGBA[3]) : placeholderCol;

                    var upperGradient = UIFactory.CreateUIObject("UpperGradient", previewTheme).AddComponent<Image>();
                    var rt = upperGradient.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;

                    var le = upperGradient.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                    le.ignoreLayout = true;


                    var circles = UIFactory.CreateUIObject("Circles", previewTheme).AddComponent<Image>();
                    UIFactory.SetLayoutElement(circles.gameObject, ignoreLayout: true);

                    circles.raycastTarget = false;
                    circles.color = Color.white;
                    circles.material = MenuThemeService.BackgroudMaterial;

                    var rt2 = circles.GetComponent<RectTransform>();
                    rt2.anchorMin = Vector2.zero;
                    rt2.anchorMax = Vector2.one;
                    rt2.offsetMin = Vector2.zero;
                    rt2.offsetMax = Vector2.zero;
                    rt2.sizeDelta = new(0, 500);

                    UIFactory.SetLayoutElement(circles.gameObject, minHeight: 180, preferredHeight: 180, flexibleWidth: 99999);

                    var lowerGradient = UIFactory.CreateUIObject("LowerGradient", previewTheme).AddComponent<Image>();

                    UIFactory.SetLayoutElement(lowerGradient.gameObject, minHeight: 180, preferredHeight: 180, flexibleWidth: 99999);
                    lowerGradient.sprite = linearGraidentImage;
                    Color gradCol;

                    if (theme != null)
                        gradCol = new Color(theme.LowerGradientRGBA[0], theme.LowerGradientRGBA[1], theme.LowerGradientRGBA[2], theme.LowerGradientRGBA[3]);
                    else
                        gradCol = placeholderCol;

                    lowerGradient.color = gradCol;

                    //upperGradient.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;

                    var themeActions = UIFactory.CreateHorizontalGroup(holder, "previewTheme", true, false, true, true, 2, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
                    ButtonRef openDirBtn = UIFactory.CreateButton(themeActions, "openDirBtn", LocalizedStr("gui_theme_dir"));
                    UIFactory.SetLayoutElement(openDirBtn.Component.gameObject, 100, 25, 0, 0);
                    openDirBtn.OnClick += () =>
                    {
                        var target = Plugin.ThemesDir + tS.ThemeOnPreviewPath.Split('\\')[0];
                        Application.OpenURL(target);
                    };

                    var delThemeDirBtn = UIFactory.CreateButton(themeActions, "delThemeDirBtn", LocalizedStr("gui_theme_del"), GUIRed);
                    UIFactory.SetLayoutElement(delThemeDirBtn.Component.gameObject, 100, 25, 0, 0);
                    delThemeDirBtn.OnClick += tS.DeleteThemeAction;

                    string info = $"{LocalizedStr("gui_catalogue_theme_info_0")}\n{LocalizedStr("gui_catalogue_theme_info_1")}\n\n{LocalizedStr("gui_catalogue_theme_info_2")}\n{LocalizedStr("gui_catalogue_theme_info_3")}";
                    GameObject onlineThemeInfo = UIFactory.CreateHorizontalGroup(holder, "onlineThemeInfo", false, false, true, true, 0, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
                    onlineThemeInfo.gameObject.GetComponent<Image>().enabled = false;
                    var txt = UIFactory.CreateLabel(onlineThemeInfo, "infoString", info, TextAnchor.MiddleCenter);
                    UIFactory.SetLayoutElement(txt.gameObject, minHeight: 180, preferredHeight: 180, flexibleWidth: 99999);
                    onlineThemeInfo.gameObject.SetActive(false);

                    var themesInfoString = UIFactory.CreateLabel(holder, "langTitle", LocalizedStr("gui_localization"), TextAnchor.MiddleCenter);

                    GameObject toggleRow = UIFactory.CreateHorizontalGroup(holder, "ToggleRow", true, false, true, true, 2, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
                    toggleRow.gameObject.GetComponent<Image>().enabled = false;
                    UIFactory.CreateToggle(toggleRow, "catalogueToggle", out Toggle catalogueToggle, out Text catalogueToggleTxt);

                    Text inftxt = UIFactory.CreateLabel(toggleRow, "viewingInfo", LocalizedStr("gui_viewing") + ": " + LocalizedStr("gui_local_themes"), TextAnchor.LowerRight);
                    UIFactory.SetLayoutElement(inftxt.gameObject, minHeight: 20, preferredHeight: 20);

                    catalogueToggleTxt.text = LocalizedStr("gui_view_themes_catalogue");
                    catalogueToggle.isOn = false;
                    catalogueToggle.onValueChanged.AddListener((val) =>
                    {
                        if (val)
                        {
                            if (!FGTTargetSettings.ThemeBrowserOnline)
                                return;

                            themesDropdown.gameObject.SetActive(false);
                            selectButton.GameObject.SetActive(false);
                            previewTheme.gameObject.SetActive(false);
                            themeActions.gameObject.SetActive(false);

                            selectWebTheme.Component.gameObject.SetActive(true);
                            webthemesDropdown.gameObject.SetActive(true);
                            onlineThemeInfo.gameObject.SetActive(true);
                            catalogueHelp.GameObject.SetActive(true);
                            inftxt.text = LocalizedStr("gui_viewing") + ": " + LocalizedStr("gui_web_themes");
                        }
                        else
                        {
                            if (!FGTTargetSettings.ThemeBrowserLocal)
                                return;

                            selectWebTheme.Component.gameObject.SetActive(false);
                            webthemesDropdown.gameObject.SetActive(false);
                            onlineThemeInfo.gameObject.SetActive(false);
                            catalogueHelp.GameObject.SetActive(false);

                            themeActions.SetActive(!tS.IsOnDefaultTheme);
                            inftxt.text = LocalizedStr("gui_viewing") + ": " + LocalizedStr("gui_local_themes");
                            themesDropdown.gameObject.SetActive(true);
                            selectButton.GameObject.SetActive(true);
                            previewTheme.gameObject.SetActive(true);
                        }
                    });
                    tS.SetUIReferences([
                        selectWebTheme,
                        themeActions,
                        txt,
                        delThemeDirBtn,
                        linearGraidentImage,
                        themesDropdown,
                        upperGradient,
                        webthemesDropdown,
                        lowerGradient,
                        themesInfoString,
                        WebThemeIds,
                        circles,
                        selectButton
                    ]);
                }));
                #endregion

                //LOCALIZATION
                Text langTitle = UIFactory.CreateLabel(MiscContent, "langTitle", LocalizedStr("gui_localization"), TextAnchor.UpperCenter);

                TryDrawUI(() => FGTTargetSettings.LangSwitcher, MiscContent, new(() =>
                {
                    GameObject langRow = UIFactory.CreateHorizontalGroup(MiscContent, "Selection Row", false, false, true, true, 2, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
                    UIFactory.CreateDropdown(langRow, "Languages", out langDropdown, "", 14, PickLanguage);
                    UIFactory.SetLayoutElement(langDropdown.gameObject, 150, 25, 150, 0, 99999);

                    LoadLangDropdown();

                    ButtonRef selectLangButton = UIFactory.CreateButton(langRow, "Select Button", LocalizedStr("gui_select"));
                    UIFactory.SetLayoutElement(selectLangButton.Component.gameObject, 100, 25, 100, 0, 99999);

                    ButtonRef localizationHelp = UIFactory.CreateButton(langRow, "Help", "?");
                    UIFactory.SetLayoutElement(localizationHelp.Component.gameObject, 30, 25, 30, 0, 30);
                    localizationHelp.OnClick = () => { DoModal(LocalizedStr("gui_localization_faq_title"), LocalizedStr("gui_localization_faq_desc"), UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Default, btnOkStr: LocalizedStr("gui_btn_got_it"), hideGUI: ModalHideGUIType.KeepHiddenForThisModal); };

                    GameObject langDir = UIFactory.CreateHorizontalGroup(MiscContent, "Lang Dir Row", false, false, true, true, 2, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
                    ButtonRef langDirBtn = UIFactory.CreateButton(langDir, "Dir Button", LocalizedStr("gui_localization_lang_dir"));
                    UIFactory.SetLayoutElement(langDirBtn.Component.gameObject, 100, 25, 100, 0, 99999);
                    langDirBtn.OnClick += () => { Application.OpenURL(Plugin.LocalizationDir + selectedLang); };

                    langAuthor = UIFactory.CreateLabel(MiscContent, "langAuthor", LocalizedStr("gui_localization_author_v2"), TextAnchor.UpperCenter);
                    UIFactory.SetLayoutElement(langAuthor.gameObject, minHeight: 20);
                    langAuthor.gameObject.SetActive(false);
                    selectLangButton.OnClick += TryToSetLang;
                    var warn = UIFactory.CreateLabel(MiscContent, "warn", OnlineCheck.FGTContent.LocaleConfig.ParseOfficialLanguagesStr(LocalizedStr("gui_localization_note")), TextAnchor.LowerCenter);
                    warn.fontSize = 12;
                    warn.fontStyle = FontStyle.Italic;
                    UIFactory.SetLayoutElement(warn.gameObject, minHeight: 20);
                }));

                //STATS
                Text statisticsTitle = UIFactory.CreateLabel(MiscContent, "StatisticsTitle", LocalizedStr("statistics_title"), TextAnchor.UpperCenter);
                UIFactory.SetLayoutElement(statisticsTitle.gameObject, minHeight: 20);

                TryDrawUI(() => FGTTargetSettings.Statistics, MiscContent, new(() =>
                {
                    var statsJson = FGTServiceManager.GetService<StatisticsService>().currentStats;
                    statistics = UIFactory.CreateLabel(MiscContent, "stats", "0", TextAnchor.MiddleLeft);
                    toggleHistory = UIFactory.CreateButton(MiscContent, "ToggleHistory", $"{LocalizedStr("gui_open_rhistory")} ▼", new Color(0.2f, 0.3f, 0.2f));
                    UIFactory.SetLayoutElement(toggleHistory.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
                    toggleHistory.OnClick += () =>
                    {
                        if (!roundHistory.gameObject.activeSelf)
                        {
                            toggleHistory.ButtonText.text = LocalizedStr("gui_close_rhistory") + " ▲";
                            roundHistory.gameObject.SetActive(true);
                            if (FGTServiceManager.GetService<StatisticsService>().HistoryPages.Count > 0)
                                HistoryActions.SetActive(true);

                        }
                        else
                        {
                            toggleHistory.ButtonText.text = LocalizedStr("gui_open_rhistory") + " ▼";
                            roundHistory.gameObject.SetActive(false);
                            HistoryActions.SetActive(false);
                        }
                        FGTServiceManager.GetService<StatisticsService>().LoadPage();
                    };

                    roundHistory = UIFactory.CreateScrollView(MiscContent, "roundHistory", out GameObject historycontent, out _, new(0.1f, 0.1f, 0.1f));
                    UIFactory.SetLayoutElement(roundHistory, minHeight: 102, preferredHeight: 102);

                    historycontent.GetComponent<VerticalLayoutGroup>().spacing = 2;
                    RoundHistoryTXT = UIFactory.CreateLabel(historycontent, "roundHistory", "Not parsed yet", TextAnchor.UpperLeft);
                    roundHistory.gameObject.SetActive(false);
                    RoundHistoryTXT.fontSize = 10;

                    HistoryActions = UIFactory.CreateHorizontalGroup(MiscContent, "HistoryActions", false, false, true, true, 2, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
                    HistoryActions.gameObject.GetComponent<Image>().enabled = false;
                    HistoryMinus = UIFactory.CreateButton(HistoryActions, "Help", "<--");
                    HistoryMinus.OnClick = () => { FGTServiceManager.GetService<StatisticsService>().HistoryNavBack(); };
                    UIFactory.SetLayoutElement(HistoryMinus.Component.gameObject, 50, 25, 50, 0, 50);

                    DisplayInfo = UIFactory.CreateLabel(HistoryActions, "DisplayInfo", "Displaying 102 elements out of 999", TextAnchor.MiddleCenter);
                    UIFactory.SetLayoutElement(DisplayInfo.gameObject, 100, 25, 100, 0, 99999);

                    HistoryPlus = UIFactory.CreateButton(HistoryActions, "Help", "-->");
                    HistoryPlus.OnClick = () => { FGTServiceManager.GetService<StatisticsService>().HistoryNavForward(); };
                    UIFactory.SetLayoutElement(HistoryPlus.Component.gameObject, 50, 25, 50, 0, 50);
                    HistoryActions.gameObject.SetActive(false);

                    ButtonRef saveStats = UIFactory.CreateButton(MiscContent, "SaveStats", $"{LocalizedStr("gui_save_stats")}", new Color(0.2f, 0.3f, 0.2f));
                    UIFactory.SetLayoutElement(saveStats.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
                    saveStats.OnClick += FGTServiceManager.GetService<StatisticsService>().Save;
                    ButtonRef clearStats = UIFactory.CreateButton(MiscContent, "DelStats", $"{LocalizedStr("gui_delete_stats")}", GUIRed);
                    UIFactory.SetLayoutElement(clearStats.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
                    clearStats.OnClick += () =>
                    {
                        void pop(bool wasok)
                        {
                            if (wasok)
                                FGTServiceManager.GetService<StatisticsService>().Init(true);
                        }
                        DoModal(LocalizedStr("stats_del_title"), LocalizedStr("stats_del_desc"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Disruptive, new Action<bool>(pop), hideGUI: ModalHideGUIType.ShowOnCancel);
                    };
                    Text statsDisclaimer = UIFactory.CreateLabel(MiscContent, "statsDisclaimer", $"{LocalizedStr("gui_stats_info_01")}\n{LocalizedStr("gui_stats_info_02")}\n", TextAnchor.MiddleLeft);
                    autosaveInfo = UIFactory.CreateLabel(MiscContent, "autosaveInfo", $"0", TextAnchor.MiddleLeft);
                }));

                //CONFIG
                Text actionsTitle = UIFactory.CreateLabel(MiscContent, "СonfigActionsTitle", LocalizedStr("gui_edit_config_title"), TextAnchor.UpperCenter);
                UIFactory.SetLayoutElement(actionsTitle.gameObject, minHeight: 20);
                ButtonRef saveBtn2 = UIFactory.CreateButton(MiscContent, "Refresh", $"{LocalizedStr("gui_refresh_config")}", new Color(0.2f, 0.3f, 0.2f));
                Text refreshDesc = UIFactory.CreateLabel(MiscContent, "СonfigActionsTitle", LocalizedStr("gui_config_about_0"), TextAnchor.MiddleLeft);
                UIFactory.SetLayoutElement(saveBtn2.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
                saveBtn2.OnClick += () => { ConfigManager.CFG.Reload(); };
                ButtonRef saveBtn3 = UIFactory.CreateButton(MiscContent, "RefreshGUI", $"{LocalizedStr("gui_refresh_gui")}", new Color(0.2f, 0.3f, 0.2f));
                Text refreshGUIDesc = UIFactory.CreateLabel(MiscContent, "СonfigActionsTitle", LocalizedStr("gui_config_about_1"), TextAnchor.MiddleLeft);
                UIFactory.SetLayoutElement(saveBtn3.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
                saveBtn3.OnClick += () =>
                {
                    RefreshEverything();
                };
                ButtonRef saveBtn4 = UIFactory.CreateButton(MiscContent, "OpenConfig", $"{LocalizedStr("gui_open_config")}", new Color(0.2f, 0.3f, 0.2f));
                Text openCfgDesc = UIFactory.CreateLabel(MiscContent, "СonfigActionsTitle", LocalizedStr("gui_config_about_2"), TextAnchor.MiddleLeft);
                UIFactory.SetLayoutElement(saveBtn4.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
                saveBtn4.OnClick += () => { Application.OpenURL(ConfigManager.CFG.ConfigFilePath); };
                ButtonRef saveBtn5 = UIFactory.CreateButton(MiscContent, "ClearEvents", $"{LocalizedStr("gui_clear_evt")}", new Color(0.2f, 0.3f, 0.2f));
                Text clearEventsDesc = UIFactory.CreateLabel(MiscContent, "СonfigActionsTitle", LocalizedStr("gui_config_about_4"), TextAnchor.MiddleLeft);
                UIFactory.SetLayoutElement(saveBtn5.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
                saveBtn5.OnClick += () => { FGTServiceManager.GetService<EventService>().RegisterEvents(true); };
                //Text configAbout = UIFactory.CreateLabel(creditscontent, "configAbout", $"{LocalizedStr("gui_config_about")}\n{LocalizedStr("gui_config_about_0")}\n{LocalizedStr("gui_config_about_1")}\n{LocalizedStr("gui_config_about_2")}\n{LocalizedStr("gui_config_about_4")}\n\n{LocalizedStr("gui_config_about_3")}", TextAnchor.LowerLeft, default, true, 14);

                //UIFactory.SetLayoutElement(configAbout.previewTheme, preferredHeight: 1000, flexibleHeight: 9999, flexibleWidth: 9999);

            }

            void LoadLangDropdown()
            {
                var target = new Il2CppSystem.Collections.Generic.List<string>();
                target.Add(LocalizedStr("dropdown_placeholder"));
                foreach (var str in OnlineCheck.FGTContent.LocaleConfig)
                {
                    string lang = OnlineCheck.FGTContent.LangCodes.ReturnLang(str.ForLang);

                    if (lang != null)
                        target.Add(lang);
                    else
                        target.Add($"{str}");
                }
                langDropdown.AddOptions(target);
                int currLangIndx = KnownLocales.IndexOf(ConfigManager.LangFileName.Value.ToUpper());
                langDropdown.value = currLangIndx + 1;
            }

            void PickLanguage(int index)
            {
                if (index > 0)
                {
                    selectedLang = OnlineCheck.FGTContent.LocaleConfig[index - 1].ForLang.ToLower();
                    if (langAuthor != null)
                    {
                        langAuthor.gameObject.SetActive(true);
                        langAuthor.text = $"{LocalizedStr("gui_localization_author_v2", [OnlineCheck.FGTContent.LocaleConfig.ReturnTranslateAuthor(selectedLang), OnlineCheck.FGTContent.LocaleConfig.ReturnLastUpdate(selectedLang)])}";
                    }
                }
                else
                    langAuthor.gameObject.SetActive(false);
            }

            void DrawCredits()
            {
                FGTCreditsGUI = UIFactory.CreateVerticalGroup(ContentRoot, "Credits", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(FGTCreditsGUI, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);
                GameObject scrollview = UIFactory.CreateScrollView(FGTCreditsGUI, "creditsGUI", out _, out _, new(0.1f, 0.1f, 0.1f));
                UIFactory.SetLayoutElement(scrollview, preferredHeight: 310, flexibleHeight: 9999, flexibleWidth: 9999);
          
                StringBuilder strBuilder = new();

                foreach (var credit in OnlineCheck.FGTContent.Credits)
                {
                    strBuilder.AppendLine($"<b>{LocalizedStr(credit.Value.Id).ToUpper()}</b>\n");

                    foreach (var actualCredit in credit.Value.Credits)
                    {
                        if (actualCredit.Perfom != string.Empty)
                            strBuilder.AppendLine($"{actualCredit.Subject} - {actualCredit.Perfom}");
                        else
                            strBuilder.AppendLine($"{actualCredit.Subject}");
                    }

                    strBuilder.AppendLine();
                }

                Text credits = UIFactory.CreateLabel(FGTCreditsGUI, "creditsInfo", $"{strBuilder}", TextAnchor.LowerCenter, default, true, 14);
                credits.transform.parent = scrollview.GetComponent<ScrollRect>().content;
                Text bottomLine = UIFactory.CreateLabel(FGTCreditsGUI, "creditsInfo_2", $"{Plugin.DisplayName} V{Plugin.BuildInfo.Version} {Description[Description.IndexOf("by")..]}", TextAnchor.LowerCenter, default, true, 14);
                UIFactory.SetLayoutElement(bottomLine.gameObject, minHeight: 5);
            }

            void DrawPresetSelector()
            {
                var pS = FGTServiceManager.GetService<PresetsService>();

                FGTPresetsGUI = UIFactory.CreateVerticalGroup(ContentRoot, "Presets", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(FGTPresetsGUI, minHeight: 25, flexibleHeight: 0);

                TryDrawUI(() => FGTTargetSettings.CosmeticPresets, FGTPresetsGUI, new(() =>
                {
                    GameObject presetsDropGroup = UIFactory.CreateHorizontalGroup(FGTPresetsGUI, "presetsDropGroup", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(presetsDropGroup, minHeight: 30, flexibleHeight: 0);
                    GameObject presetsDrop = UIFactory.CreateDropdown(presetsDropGroup, "presetsDrop", out Dropdown presetDrop, $"{LocalizedStr("dropdown_placeholder")}", 14, pS.OnPresetsDropSelect, null);
                    UIFactory.SetLayoutElement(presetsDropGroup, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);

                    GameObject presetBtns = UIFactory.CreateHorizontalGroup(FGTPresetsGUI, "presetBtns", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    UIFactory.SetLayoutElement(presetBtns, minHeight: 25, flexibleHeight: 0);
                    ButtonRef usePresetBtn = UIFactory.CreateButton(presetBtns, "usePresetBtn", $"{LocalizedStr("gui_use_preset")}", null);
                    usePresetBtn.OnClick += pS.TryUsePreset;
                    UIFactory.SetLayoutElement(usePresetBtn.GameObject, 30, 20, null, 0, null, null, null);
                    ButtonRef newPresetBtn = UIFactory.CreateButton(presetBtns, "newPresetBtn", $"{LocalizedStr("gui_new_preset")}", new Color(0.2f, 0.3f, 0.2f));
                    newPresetBtn.OnClick += pS.MakeNewPresetPopup;
                    UIFactory.SetLayoutElement(usePresetBtn.GameObject, 30, 20, null, 0, null, null, null);
                    ButtonRef delPresetBtn = UIFactory.CreateButton(presetBtns, "delPresetBtn", $"{LocalizedStr("gui_delete_preset")}", GUIRed);
                    delPresetBtn.OnClick += pS.TryDeletePreset;
                    UIFactory.SetLayoutElement(delPresetBtn.GameObject, 30, 20, null, 0, null, null, null);

                    GameObject presetInfoZone = UIFactory.CreateHorizontalGroup(FGTPresetsGUI, "presetInfoZone", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                    GameObject hell = UIFactory.CreateScrollView(presetInfoZone, "PRESETINFO", out GameObject content, out AutoSliderScrollbar scrollBar, new(0.1f, 0.1f, 0.1f));
                    UIFactory.SetLayoutElement(hell, flexibleHeight: 9999, minHeight: 250);
                    Transform settingsList = hell.GetComponent<ScrollRect>().content.transform;
                    Text presetInfo = UIFactory.CreateLabel(settingsList.gameObject, "presetInfo", $"{LocalizedStr("gui_presets_desc")}", TextAnchor.LowerLeft, default, true, 14);

                    pS.SetUIReferences([presetDrop,
                    presetInfo]);

                    Text bottomLine = UIFactory.CreateLabel(FGTPresetsGUI, "creditsInfo_2", $"{LocalizedStr("gui_presets_desc")}", TextAnchor.LowerCenter, default, true, 14);
                    UIFactory.SetLayoutElement(bottomLine.gameObject, preferredHeight: 1000, flexibleHeight: 9999, flexibleWidth: 9999);
                }));


            }

            void RefreshEverything(bool onlyCleanup = false)
            {
                if (!onlyCleanup)
                    FGTLog(LogLevel.Info, GetType(), "Trying to refresh everything");
                try
                {
                    foreach (GameObject obj in fgc_history)
                        UnityEngine.Object.Destroy(obj);

                    fgc_history.Clear();
                    FGTServiceManager.OnGUIRefresh();

                    if (!onlyCleanup)
                    {

                    }

                    LoadFGCHistory();
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
                    TitleBar.transform.GetChild(0).GetComponent<Text>().text = $"{Plugin.DisplayName} V{Plugin.BuildInfo.Version} > {title}";
            }

            internal void GoToTab(Tab selectedTab, SubLevel tabLevel, bool shouldChangeTitle = true, bool enableExtraStuff = true, bool silent = false)
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


                FGTRoundLoaderTabs.gameObject.SetActive(selectedTab == Tab.RoundLoader || tabLevel == SubLevel.RoundLoader);

                PreviousTab = CurrentTab.Copy();
                CurrentTab = new()
                {
                    SubLevel = tabLevel,
                    Tab = selectedTab
                };


                if (enableExtraStuff)
                {
                    if (toggleHistory == null || roundHistory == null)
                        return;

                    if (CurrentTab.Tab != Tab.Misc && roundHistory.gameObject.activeSelf)
                    {
                        toggleHistory.ButtonText.text = LocalizedStr("gui_open_rhistory") + " ▼";
                        roundHistory.SetActive(false);
                        HistoryActions.gameObject.SetActive(false);
                    }
                }

            }

            Dictionary<string, SetSwitcher.SwitchableSetMapping> AvailableVariations;

            void PrepareVariations()
            {
                //todo: rewrite this trash
                try
                {
                    if (AvailableVariations == null)
                        AvailableVariations = new Dictionary<string, SetSwitcher.SwitchableSetMapping>();
                    else
                        AvailableVariations.Clear();

                    AvailableVariations.Add(LocalizedStr("dropdown_placeholder"), new SetSwitcher.SwitchableSetMapping());

                    foreach (var Switcher in Resources.FindObjectsOfTypeAll<SetSwitcher>())
                    {
                        foreach (var mapping in Switcher.SwitchableSetMappings)
                        {
                            if (mapping.SwitchableSetHolder != null)
                                AvailableVariations.Add($"<color=grey>{Switcher.name}:</color> {mapping.CMSKey}", mapping);
                        }
                    }

                    VariationsDropdown.ClearOptions();
                    var keysList = new Il2CppSystem.Collections.Generic.List<string>();
                    foreach (string key in AvailableVariations.Keys)
                        keysList.Add(key);
                    VariationsDropdown.AddOptions(keysList);
                }
                catch
                {

                }
            }

            void SaveVariants()
            {
                string sceneName = SceneManager.GetActiveScene().name;
                string startTag = $"[{sceneName} START]";
                string endTag = $"[{sceneName} END]";

                List<string> lines = new List<string>(File.ReadAllLines(Plugin.VariantData));

                int startTagIndex = lines.IndexOf(startTag);
                int endTagIndex = lines.IndexOf(endTag);

                if (startTagIndex != -1 && endTagIndex != -1)
                {
                    lines.RemoveRange(startTagIndex, endTagIndex - startTagIndex + 1);
                }

                List<string> newLines = new List<string>(lines)
            {
                startTag
            };

                foreach (var Switcher in Resources.FindObjectsOfTypeAll<SetSwitcher>())
                {
                    foreach (var mapping in Switcher.SwitchableSetMappings)
                    {
                        if (mapping.SwitchableSetHolder != null && mapping.IsEnabled)
                        {
                            newLines.Add(Switcher.name + "=" + mapping.CMSKey);
                        }
                    }
                }

                newLines.Add(endTag);
                File.WriteAllLines(Plugin.VariantData, newLines.ToArray());
            }
            void SetVariants()
            {
                string[] data = File.ReadAllLines(Plugin.VariantData);
                var sceneName = SceneManager.GetActiveScene().name;
                string startTag = $"[{sceneName} START]";
                string endTag = $"[{sceneName} END]";

                if (!(data.Contains(startTag) && data.Contains(endTag)))
                {
                    FGTLog(LogLevel.Info, GetType(), "No variants saved for this scene");
                    return;
                }

                foreach (var Switcher in Resources.FindObjectsOfTypeAll<SetSwitcher>())
                {
                    if (Switcher != null && Switcher.SwitchableSetMappings != null)
                    {
                        foreach (var mapping in Switcher.SwitchableSetMappings)
                        {
                            if (mapping != null && mapping.SwitchableSetHolder != null)
                                mapping.SwitchableSetHolder.SetActive(false);
                        }
                    }

                }

                bool insideSection = false;
                foreach (var line in data)
                {
                    if (line.Equals($"[{sceneName} START]"))
                    {
                        insideSection = true;
                        continue;
                    }
                    else if (line.Equals($"[{sceneName} END]"))
                    {
                        insideSection = false;
                        break;
                    }

                    if (insideSection && line.Contains('='))
                    {
                        foreach (var Switcher in Resources.FindObjectsOfTypeAll<SetSwitcher>())
                        {
                            if (Switcher != null && Switcher.SwitchableSetMappings != null)
                            {
                                if (Switcher.name == line.Split('=')[0].Trim())
                                {
                                    foreach (var mapping in Switcher.SwitchableSetMappings)
                                    {
                                        if (mapping != null && mapping.SwitchableSetHolder != null && mapping.CMSKey == line.Split('=')[1].Trim())
                                            mapping.SwitchableSetHolder.gameObject.SetActive(true);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            void OnStateChange(FGTStateManager.FGTState state)
            {

                GoToTab(Tab.RoundLoader_Main, SubLevel.RoundLoader, silent: true);

                switch (state)
                {
                    case FGTStateManager.FGTState.Menu:
                        ToggleGroup(ObjectGroup.Menu);
                        break;
                    case FGTStateManager.FGTState.Results:
                        ToggleGroup(ObjectGroup.Results);
                        break;
                    case FGTStateManager.FGTState.RoundLoading:
                    case FGTStateManager.FGTState.GPFGCLoading:
                    case FGTStateManager.FGTState.RoundIntro:
                        ToggleGroup(ObjectGroup.Loading);
                        break;
                    case FGTStateManager.FGTState.GameActive:
                    case FGTStateManager.FGTState.FGCGameActive:
                        PrepareVariations();
                        ToggleGroup(ObjectGroup.Gameplay);
                        if (StateManager.ExploreState != null)
                            ToggleGroup(ObjectGroup.Explore);
                        break;
                    case FGTStateManager.FGTState.InCreative:
                        ToggleGroup(ObjectGroup.Editor);
                        break;
                }
            }

            public void GUIController()
            {
                try
                {
                    if (TabHover)
                        TabHoverText.transform.position = Input.mousePosition + (Vector3.up * 15);

                    if (fgc_desc != null)
                    {
                        if (OnlineCheck == null || OnlineCheck.ExploreCodes == null)
                            ExploreRoundsCount = LocalizedStr("gui_explore_error_desc");

                        else if (OnlineCheck.ExploreCodes != null)
                            ExploreRoundsCount = OnlineCheck.ExploreCodes.Count.ToString();

                        fgc_desc.text = $"{LocalizedStr("gui_creative_loader_desc")}\n{LocalizedStr("gui_creative_loader_warning")}\n\n{LocalizedStr("gui_fgc_explore_0")}: {ExploreRoundsCount}\n{LocalizedStr("gui_fgc_explore_1_v2")}";
                    }

                    if (CurrentTab.Tab == Tab.Misc)
                    {
                        var themeService = FGTServiceManager.GetService<MenuThemeService>();
                        themeService.UpdateInfo();

                        var statsJson = FGTServiceManager.GetService<StatisticsService>().currentStats;
                        TimeSpan ingame = TimeSpan.FromSeconds(statsJson.TimeInGame);
                        TimeSpan inmenu = TimeSpan.FromSeconds(statsJson.TimeInMenu);
                        TimeSpan infgc = TimeSpan.FromSeconds(statsJson.TimeInFGC);
                        statistics.text = $"{LocalizedStr("stat_time_in_game")}: {ingame:dd':'hh':'mm':'ss}" +
                            $"\n{LocalizedStr("stat_time_inmenu")}: {inmenu:dd':'hh':'mm':'ss}" +
                            $"\n{LocalizedStr("stat_total_rounds")}: {statsJson.TotalRoundsLoaded}" +
                            $"\n{LocalizedStr("stat_launch_times")}: {statsJson.GameLaunchedTimes}" +
                            $"\n{LocalizedStr("stat_sp_att_total")}: {statsJson.TotalAttemptsSp}" +
                            $"\n{LocalizedStr("stats_total_time_fgc")}: {infgc:dd':'hh':'mm':'ss}" +
                            $"\n{LocalizedStr("stats_collectables_collected")}: {statsJson.CollectablePickup}" +
                            $"\n{LocalizedStr("stats_qual_times")}: {statsJson.QualTotal}" +
                            $"\n{LocalizedStr("stats_elim_times")}: {statsJson.ElimTotal}" +
                            $"\n{LocalizedStr("stats_win_times")}: {statsJson.WinTotal}";
                        if (statsJson.RoundHistory != null && !roundHistory.gameObject.activeSelf)
                        {
                            //List<string> sortedList = new([.. ServiceManagerFGT.GetService<StatisticsService>().currentStats.RoundHistoryTXT]);
                            //sortedList.Reverse();
                            //var arr = sortedList.ToArray();
                            //RoundHistoryTXT.text = string.Join("\n", arr.Select((line, index) => $"{index + 1}. | {line}"));
                        }
                        else if (statsJson.RoundHistory == null || statsJson.RoundHistory.Count == 0)
                            RoundHistoryTXT.text = LocalizedStr("gui_nothing2see");

                        autosaveInfo.text = $"{LocalizedStr("stats_save_time_remain")}: {TimeSpan.FromSeconds(FGTServiceManager.GetService<StatisticsService>().SaveTime - FGTServiceManager.GetService<StatisticsService>().timeElapsed):mm':'ss}";
                    }

                    if (CurrentTab.Tab == Tab.MediaLoader)
                    {
                        if (!FGTServiceManager.GetService<MediaService>().imgLoadNearFG && mediaFGPosGrp != null && !mediaFGPosGrp.activeSelf)
                            mediaFGPosGrp.gameObject.SetActive(true);
                        else if (FGTServiceManager.GetService<MediaService>().imgLoadNearFG && mediaFGPosGrp != null && mediaFGPosGrp.activeSelf)
                            mediaFGPosGrp.gameObject.SetActive(false);

                        if (FGTServiceManager.GetService<MediaService>().urlLoad && imgRemoteLoad.GameObject != null && imgLocalLoad.GameObject.activeSelf)
                        {
                            imgLocalLoad.GameObject.SetActive(false);
                            imgRemoteLoad.GameObject.SetActive(true);
                        }
                        else if (!FGTServiceManager.GetService<MediaService>().urlLoad && imgLocalLoad.GameObject != null && imgRemoteLoad.GameObject.activeSelf)
                        {
                            imgLocalLoad.GameObject.SetActive(true);
                            imgRemoteLoad.GameObject.SetActive(false);
                        }

                        if (FGTServiceManager.GetService<MediaService>().followFGPos && FallGuyBehaviour._instance != null && FallGuyBehaviour._instance.FallGuy != null)
                        {
                            var x = FallGuyBehaviour._instance.FallGuy.transform.position.x;
                            var y = FallGuyBehaviour._instance.FallGuy.transform.position.y;
                            var z = FallGuyBehaviour._instance.FallGuy.transform.position.z;
                            FGTServiceManager.GetService<MediaService>().transX = x;
                            posX.Text = x.ToString();
                            FGTServiceManager.GetService<MediaService>().transY = y;
                            posY.Text = y.ToString();
                            FGTServiceManager.GetService<MediaService>().transZ = z;
                            posZ.Text = z.ToString();
                        }
                    }
                }
                catch
                {

                }
            }
        }
    }
}
