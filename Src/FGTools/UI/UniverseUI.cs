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
        Text langAuthor;

        
      
   
        //GameObject loadingBtnsFGC;

        public string imgHeight = "75";
        public string imgWidth = "75";
        public bool shouldDeleteWhitePixels = false;
        public bool shouldDeleteBlackPixels = false;
        public bool isDigital = false;

        string selectedLang = null;
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

                DrawIMG2FGC();
                DrawMisc();
                DrawFGCAutosaves();
                DrawCredits();
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

        void TryToSetLang()
        {
            if (selectedLang != null && Config.Config.LangFileName.Value != selectedLang)
            {
                if (SceneManager.GetActiveScene().name == "MainMenu" && FGTBase.StateManager.FGTCurrentState == FGTStateManager.ToolsState.Menu)
                {
                    DoModal(new(LocalizedStr("gui_localization_act0"), LocalizedStr("gui_localization_act1"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, new Action<bool>((bool wasok) =>
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
                                FGTBase.StateManager.LoggedInBefore = false;
                                FGTBase.FGTServiceManager.GetService<LocalizationService>().SetupLocalization(Path.Combine(Launcher.LocalizationDir, selectedLang, "locale.json"));
                                Config.Config.LangFileName.Value = selectedLang;
                                GlobalGameStateClient.Instance._mainMenuManager.ShowMainMenu(true, true, false);
                            }));
                        }
                    }), hideLvl: ModalHideGUIType.KeepHiddenForThisModal));
                }
                else
                    DoModal(new(LocalizedStr("gui_unavailable"), LocalizedStr("gui_localization_act2"), UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Default, hideLvl: ModalHideGUIType.KeepHidden));

            }
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
                img2fgcInput.Text = FGTBase.FGTServiceManager.GetService<MediaService>().imgPath;
                img2fgcInput.OnValueChanged += input => { FGTBase.FGTServiceManager.GetService<MediaService>().imgPath = input; };
                UIFactory.SetLayoutElement(img2fgcInput.UIRoot, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

                ButtonRef imagesFolderBtn = UIFactory.CreateButton(img2fgc_imageName, "openImgFolder", $"{LocalizedStr("gui_folder")}", null);
                imagesFolderBtn.OnClick += () => { Application.OpenURL(Launcher.ImgDir); };
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
                genLevel.OnClick += () => { IMG2FGCAlert(); };
                UIFactory.SetLayoutElement(genLevel.GameObject, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);
                ButtonRef repAll = UIFactory.CreateButton(img2fgc_actions, "repAll", $"{LocalizedStr("gui_img2fgc_replace")}", GUIRed);
                repAll.OnClick += () =>
                {

                    var tiles = Resources.FindObjectsOfTypeAll<LevelBrowserTileViewModel>().ToList().FindAll(x => x.name.Contains("Clone"));
                    var lvl = Path.Combine(Application.persistentDataPath, "Img2FGC.json");

                    if (!File.Exists(lvl))
                    {
                        ErrorPopup($"{LocalizedStr("img2fgc_uhh")}");
                        return;
                    }

                    if (tiles == null || tiles.Count == 0)
                    {
                        ErrorPopup($"{LocalizedStr("img2fgc_no_levels")}");
                        return;
                    }

                    foreach (var tile in tiles)
                    {
                        if (tile != null && tile.TileData != null && tile.TileData.level != null && tile.TileData.level._levelJSON != null && tile.TileData.level._levelJSON._url != null)
                            tile.TileData.level._levelJSON._url = $"file://{lvl}";
                    }

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
                actualImageObj.AddComponent<Image>().sprite = GetSpriteFromFile(Launcher.AssetsDir + "obedguyslore.png", 356, 170);
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
            var saves = FGTBase.FGTServiceManager.GetService<FGC_LocalSavesService>();
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
            var tS = FGTBase.FGTServiceManager.GetService<MenuThemeService>();
            Text themesTitle = UIFactory.CreateLabel(MiscContent, "ThemesTitle", LocalizedStr("themes_selector_title"), TextAnchor.UpperCenter);
            UIFactory.SetLayoutElement(themesTitle.gameObject, minHeight: 20);

            Text themesDisabled = UIFactory.CreateLabel(MiscContent, "ThemesTitle_Disabled", LocalizedStr("gui_only_in_menu"), TextAnchor.UpperCenter);
            UIFactory.SetLayoutElement(themesDisabled.gameObject, minHeight: 20);
            themesDisabled.gameObject.SetActive(false);

            var linearGraidentImage = Resources.FindObjectsOfTypeAll<Sprite>().ToList().Find(x => x.name == "UI_LinearGradient_Image");
            Theme theme = null;
            Color placeholderCol = Color.magenta;
            if (Config.Config.InGameTheme.Value != LocalizedStr("gui_default"))
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
                }, new(newGroup =>
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
                catalogueHelp.OnClick = () => { DoModal(new(LocalizedStr("gui_themes_catalogue_faq_title"), LocalizedStr("gui_themes_catalogue_faq_desc"), UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Default, okStrOverride: LocalizedStr("gui_btn_got_it"), hideLvl: ModalHideGUIType.KeepHiddenForThisModal)); };

                selectButton.OnClick += () => { FGTBase.FGTServiceManager.GetService<MenuThemeService>().SelectTheme(true); };
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
                    var target = Launcher.ThemesDir + tS.ThemeOnPreviewPath.Split('\\')[0];
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

                //TODO update
                //ButtonRef localizationHelp = UIFactory.CreateButton(langRow, "Help", "?");
                //UIFactory.SetLayoutElement(localizationHelp.Component.gameObject, 30, 25, 30, 0, 30);
                //localizationHelp.OnClick = () => { DoModal(new(LocalizedStr("gui_localization_faq_title"), $"<size=70%>{LocalizedStr("gui_localization_faq_desc")}</size>", UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Default, okStrOverride: LocalizedStr("gui_btn_got_it"), hideLvl: ModalHideGUIType.KeepHiddenForThisModal)); };

                GameObject langDir = UIFactory.CreateHorizontalGroup(MiscContent, "Lang Dir Row", false, false, true, true, 2, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
                ButtonRef langDirBtn = UIFactory.CreateButton(langDir, "Dir Button", LocalizedStr("gui_localization_lang_dir"));
                UIFactory.SetLayoutElement(langDirBtn.Component.gameObject, 100, 25, 100, 0, 99999);
                langDirBtn.OnClick += () => { Application.OpenURL(Launcher.LocalizationDir + selectedLang); };

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
                var statsJson = FGTBase.FGTServiceManager.GetService<StatisticsService>().currentStats;
                statistics = UIFactory.CreateLabel(MiscContent, "stats", "0", TextAnchor.MiddleLeft);
                toggleHistory = UIFactory.CreateButton(MiscContent, "ToggleHistory", $"{LocalizedStr("gui_open_rhistory")} ▼", new Color(0.2f, 0.3f, 0.2f));
                UIFactory.SetLayoutElement(toggleHistory.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
                toggleHistory.OnClick += () =>
                {
                    if (!roundHistory.gameObject.activeSelf)
                    {
                        toggleHistory.ButtonText.text = LocalizedStr("gui_close_rhistory") + " ▲";
                        roundHistory.gameObject.SetActive(true);
                        if (FGTBase.FGTServiceManager.GetService<StatisticsService>().HistoryPages.Count > 0)
                            HistoryActions.SetActive(true);

                    }
                    else
                    {
                        toggleHistory.ButtonText.text = LocalizedStr("gui_open_rhistory") + " ▼";
                        roundHistory.gameObject.SetActive(false);
                        HistoryActions.SetActive(false);
                    }
                    FGTBase.FGTServiceManager.GetService<StatisticsService>().LoadPage();
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
                HistoryMinus.OnClick = () => { FGTBase.FGTServiceManager.GetService<StatisticsService>().HistoryNavBack(); };
                UIFactory.SetLayoutElement(HistoryMinus.Component.gameObject, 50, 25, 50, 0, 50);

                DisplayInfo = UIFactory.CreateLabel(HistoryActions, "DisplayInfo", "Displaying 102 elements out of 999", TextAnchor.MiddleCenter);
                UIFactory.SetLayoutElement(DisplayInfo.gameObject, 100, 25, 100, 0, 99999);

                HistoryPlus = UIFactory.CreateButton(HistoryActions, "Help", "-->");
                HistoryPlus.OnClick = () => { FGTBase.FGTServiceManager.GetService<StatisticsService>().HistoryNavForward(); };
                UIFactory.SetLayoutElement(HistoryPlus.Component.gameObject, 50, 25, 50, 0, 50);
                HistoryActions.gameObject.SetActive(false);

                ButtonRef saveStats = UIFactory.CreateButton(MiscContent, "SaveStats", $"{LocalizedStr("gui_save_stats")}", new Color(0.2f, 0.3f, 0.2f));
                UIFactory.SetLayoutElement(saveStats.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
                saveStats.OnClick += FGTBase.FGTServiceManager.GetService<StatisticsService>().Save;
                ButtonRef clearStats = UIFactory.CreateButton(MiscContent, "DelStats", $"{LocalizedStr("gui_delete_stats")}", GUIRed);
                UIFactory.SetLayoutElement(clearStats.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
                clearStats.OnClick += () =>
                {
                    DoModal(new(LocalizedStr("stats_del_title"), LocalizedStr("stats_del_desc"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Disruptive, new Action<bool>(wasok =>
                    {
                        if (wasok)
                            FGTBase.FGTServiceManager.GetService<StatisticsService>().Init(true);
                    }), hideLvl: ModalHideGUIType.ShowOnCancel));
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
            saveBtn2.OnClick += () => { Config.Config.CFG.Reload(); };
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
            saveBtn4.OnClick += () => { Application.OpenURL(Config.Config.CFG.ConfigFilePath); };
            ButtonRef saveBtn5 = UIFactory.CreateButton(MiscContent, "ClearEvents", $"{LocalizedStr("gui_clear_evt")}", new Color(0.2f, 0.3f, 0.2f));
            Text clearEventsDesc = UIFactory.CreateLabel(MiscContent, "СonfigActionsTitle", LocalizedStr("gui_config_about_4"), TextAnchor.MiddleLeft);
            UIFactory.SetLayoutElement(saveBtn5.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
            saveBtn5.OnClick += () => { FGTBase.FGTServiceManager.GetService<EventService>().RegisterEvents(true); };
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
            int currLangIndx = KnownLocales.IndexOf(Config.Config.LangFileName.Value.ToUpper());
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

        void DrawCredits()
        {
            FGTCreditsGUI = UIFactory.CreateVerticalGroup(ContentRoot, "Credits", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(FGTCreditsGUI, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);
            GameObject scrollview = UIFactory.CreateScrollView(FGTCreditsGUI, "creditsGUI", out _, out _, new(0.1f, 0.1f, 0.1f));
            UIFactory.SetLayoutElement(scrollview, preferredHeight: 310, flexibleHeight: 9999, flexibleWidth: 9999);

            var sb = new StringBuilder();

            foreach (var credit in OnlineCheck.FGTContent.Credits)
            {
                sb.AppendLine($"<b>{LocalizedStr(credit.Value.Id).ToUpper()}</b>\n");

                foreach (var actualCredit in credit.Value.Credits)
                {
                    if (actualCredit.Perfom != string.Empty)
                        sb.AppendLine($"{actualCredit.Subject} - {actualCredit.Perfom}");
                    else
                        sb.AppendLine($"{actualCredit.Subject}");
                }

                sb.AppendLine();
            }

            Text credits = UIFactory.CreateLabel(FGTCreditsGUI, "creditsInfo", $"{sb.ToString().Trim()}", TextAnchor.LowerCenter, default, true, 14);
            credits.transform.parent = scrollview.GetComponent<ScrollRect>().content;
            Text bottomLine = UIFactory.CreateLabel(FGTCreditsGUI, "creditsInfo_2", $"{Launcher.DisplayName} V{Launcher.BuildInfo.UI_Version} {Description[Description.IndexOf("by")..]}", TextAnchor.LowerCenter, default, true, 14);
            UIFactory.SetLayoutElement(bottomLine.gameObject, minHeight: 5);
        }

        void RefreshEverything(bool onlyCleanup = false)
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

            PreviousTab = CurrentTab.Copy();
            CurrentTab = new()
            {
                SubLevel = tabLevel,
                Tab = selectedTab,
                TabObject = _tabMap.FirstOrDefault(x => x.Tab == selectedTab)
            };

            OnTabChanged.Invoke(CurrentTab);

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
                    var themeService = FGTBase.FGTServiceManager.GetService<MenuThemeService>();
                    themeService.UpdateInfo();

                    var statsJson = FGTBase.FGTServiceManager.GetService<StatisticsService>().currentStats;

                    var ingame = TimeSpan.FromSeconds(statsJson.TimeInGame);
                    var inmenu = TimeSpan.FromSeconds(statsJson.TimeInMenu);
                    var infgc = TimeSpan.FromSeconds(statsJson.TimeInFGC);

                    statistics.text = 
                        $"{LocalizedStr("stat_time_in_game")}: {ingame:dd':'hh':'mm':'ss}" +
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

                    autosaveInfo.text = $"{LocalizedStr("stats_save_time_remain")}: {TimeSpan.FromSeconds(FGTBase.FGTServiceManager.GetService<StatisticsService>().SaveTime - FGTBase.FGTServiceManager.GetService<StatisticsService>().timeElapsed):mm':'ss}";
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
