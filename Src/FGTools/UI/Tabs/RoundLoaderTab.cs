using BepInEx.Logging;
using FG.Common;
using FG.Common.CMS;
using FGClient;
using FGClient.UI;
using FGTools.Content;
using FGTools.Internal.Behaviours;
using FGTools.Services;
using FGTools.States;
using FGTools.States.Logic;
using FGTools.UI.Tabs.Logic;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Widgets;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.UI.NewGUI;

namespace FGTools.UI.Tabs
{
    internal class RoundLoaderTab : UITab<RoundLoaderService>
    {
        public RoundLoaderTab() : base(Tab.RoundLoader, FGTServiceManager.GetService<RoundLoaderService>())
        {
        }

        GameObject FGTRoundLoaderTabs;
        GameObject unityLoaderGUI;
        GameObject fgcGUI;
        GameObject gameplayGUI;
        GameObject loadOptionsGUI;
        GameObject loadOptionsGUI_Content;
        GameObject FGCLevelInfo;
        GameObject FGCHistory;
        GameObject fgcHistoryPrefab;
        ButtonRef randt;
        string ExploreRoundsCount = "0";
        Dropdown VariationsDropdown;

        void OnTabChange(TabMeta newTab)
        {
            FGTRoundLoaderTabs.gameObject.SetActive(newTab.Tab == Tab.RoundLoader || newTab.SubLevel == SubLevel.RoundLoader);
        }

        void OnStateChange(FGTStateManager.ToolsState state)
        {
            if (state == FGTStateManager.ToolsState.GameActive)
                PrepareVariations();
        }

        internal override void Update()
        {
            if (fgc_desc != null)
            {
                if (OnlineCheck == null || OnlineCheck.ExploreCodes == null)
                    ExploreRoundsCount = LocalizedStr("gui_explore_error_desc");

                else if (OnlineCheck.ExploreCodes != null)
                    ExploreRoundsCount = OnlineCheck.ExploreCodes.Count.ToString();

                fgc_desc.text = $"{LocalizedStr("gui_creative_loader_desc")}\n{LocalizedStr("gui_creative_loader_warning")}\n\n{LocalizedStr("gui_fgc_explore_0")}: {ExploreRoundsCount}\n{LocalizedStr("gui_fgc_explore_1_v2")}";
            }
        }

        internal override void Draw(GameObject root)
        {
            #region ROUND LOADER - SETUP
            NewGUI.Instance.OnTabChanged += OnTabChange;
            NewGUI.Instance.OnStateChange += OnStateChange;

            ControlledObject = UIFactory.CreateVerticalGroup(root, $"Tab_{Tab}", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(ControlledObject, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);

            FGTRoundLoaderTabs = UIFactory.CreateHorizontalGroup(root, "FGTRoundLoaderTabs", true, false, true, false, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(FGTRoundLoaderTabs, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

            var mainTab = NewGUI.Instance.CreateTab(Tab.RoundLoader_Main, SubLevel.RoundLoader, FGTRoundLoaderTabs, () => unityLoaderGUI, "gui_loader");
            var fgcTab = NewGUI.Instance.CreateTab(Tab.RoundLoader_FGC, SubLevel.RoundLoader, FGTRoundLoaderTabs, () => fgcGUI, "gui_creative_loader");

            NewGUI.Instance.AssignToGroups(fgcTab.Component, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.Interactable), () => true },
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.Interactable), () => false },
                });

            var gpTab = NewGUI.Instance.CreateTab(Tab.RoundLoader_InGame, SubLevel.RoundLoader, FGTRoundLoaderTabs, () => gameplayGUI, "gui_ingame_tab");
            NewGUI.Instance.AssignToGroups(gpTab.GameObject, new()
                {
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                });
            var optionsTab = NewGUI.Instance.CreateTab(Tab.RoundLoader_Options, SubLevel.RoundLoader, FGTRoundLoaderTabs, () => loadOptionsGUI, "gui_round_options");
            NewGUI.Instance.AssignToGroups(optionsTab.Component, new()
                {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.Interactable), () => true },
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.Interactable), () => false },
                });
            FGTRoundLoaderTabs.transform.SetSiblingIndex(2);

            unityLoaderGUI = UIFactory.CreateVerticalGroup(ControlledObject, "Loader_GUI", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(unityLoaderGUI, minHeight: 25, flexibleHeight: 0);

            fgcGUI = UIFactory.CreateVerticalGroup(ControlledObject, "FGC_GUI", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(fgcGUI, minHeight: 25, flexibleHeight: 0);
            fgcGUI.gameObject.SetActive(false);

            gameplayGUI = UIFactory.CreateScrollView(ControlledObject, "Gameplay_GUI", out var gameplayGUI_Content, out _, new(0.1f, 0.1f, 0.1f));
            gameplayGUI.transform.SetParent(ControlledObject.transform, false);
            gameplayGUI.gameObject.SetActive(false);

            loadOptionsGUI = UIFactory.CreateScrollView(ControlledObject, "Gameplay_GUI", out loadOptionsGUI_Content, out _, new(0.1f, 0.1f, 0.1f));
            loadOptionsGUI.transform.SetParent(ControlledObject.transform, false);
            loadOptionsGUI.gameObject.SetActive(false);
            #endregion

            #region ROUND LOADER - DEFAULT

            Text stats = null;
            Dropdown roundNamesDrop = null;
            Dropdown roundVariantsDrop = null;
            InputFieldRef inputFieldRef = null;
            Text levelInfo = null;

            NewGUI.Instance.TryDrawUI(() => FGTTargetSettings.RoundLoader, unityLoaderGUI, new(() =>
            {
                var searchbarGroup = UIFactory.CreateHorizontalGroup(unityLoaderGUI, "Search", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                NewGUI.Instance.AssignToGroups(searchbarGroup, new()
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
                printCms.OnClick += () => { Service.GenerateCMSList(); };
                printCms.GameObject.SetActive(false);
                ButtonRef delList = UIFactory.CreateButton(searchbarGroup, "Advanced_DelList", $"{LocalizedStr("gui_del_list")}", GUIRed);
                delList.OnClick += () =>
                {
                    if (File.Exists(Launcher.CMSRounds))
                        File.Delete(Launcher.CMSRounds);
                };
                delList.GameObject.SetActive(false);

                var roundNamesGroup = UIFactory.CreateHorizontalGroup(unityLoaderGUI, "Dropdowns", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                NewGUI.Instance.AssignToGroups(roundNamesGroup, new()
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
                advRoundInput.OnValueChanged += input => { Service.RoundToLoad = input; };
                UIFactory.SetLayoutElement(advRoundInput.GameObject, minHeight: 25, flexibleHeight: 0);
                advRoundInput.GameObject.gameObject.SetActive(false);
                UIFactory.SetLayoutElement(roundNamesGroup, minHeight: 25, flexibleHeight: 0);

                var loadingBtns = UIFactory.CreateHorizontalGroup(unityLoaderGUI, "LoadButtons", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                NewGUI.Instance.AssignToGroups(loadingBtns, new()
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
                NewGUI.Instance.AssignToGroups(singleButton.GameObject, new()
            {
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true }
            });
                singleButton.OnClick += () =>
                {
                    Service.LoadLatestRound(LoadSceneMode.Single);
                };

                UIFactory.SetLayoutElement(singleButton.GameObject, 30, 20, null, 0, null, null, null);
                var additiveButton = UIFactory.CreateButton(loadingBtns, "additive", $"{LocalizedStr("gui_additive_load")}", null);
                NewGUI.Instance.AssignToGroups(additiveButton.GameObject, new()
            {
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true }
            });
                additiveButton.OnClick += () =>
                {
                    Service.LoadLatestRound(LoadSceneMode.Additive);
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
                NewGUI.Instance.AssignToGroups(UnityExploreBtn.GameObject, new()
            {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false }
            });

                UnityExploreBtn.OnClick += () => { FGTBase.StateManager.TryJoinExplore(UltimatePartyState.JoinPolicy.Random); };
                UIFactory.SetLayoutElement(UnityExploreBtn.GameObject, 30, 20, null, 0, null, null, null);

                var EndlessExploreBtn = UIFactory.CreateButton(mainTools, "EndlessExploreBtn", $"{LocalizedStr("gui_play_explore_endless")}", null);
                NewGUI.Instance.AssignToGroups(EndlessExploreBtn.GameObject, new()
            {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false }
            });

                EndlessExploreBtn.OnClick += () =>
                {
                    if (FGTStateManager.IsInIllegalState) return;
                    DoModal(new(LocalizedStr("gui_explore_endless_title"), LocalizedStr("gui_explore_endless_desc") + "\n\n" + LocalizedStr("gui_explore_desc_base"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, new Action<bool>((bool wasok) =>
                    {
                        if (wasok)
                            FGTBase.StateManager.TryJoinExplore(UltimatePartyState.JoinPolicy.Endless);
                    }), hideLvl: ModalHideGUIType.ShowOnCancel));
                };
                UIFactory.SetLayoutElement(EndlessExploreBtn.GameObject, 30, 20, null, 0, null, null, null);

                var RequestRandomRound = UIFactory.CreateButton(mainTools, "NormalExploreBtn", $"{LocalizedStr("gui_random_round")}", null);
                NewGUI.Instance.AssignToGroups(RequestRandomRound.GameObject, new()
            {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
            });

                RequestRandomRound.OnClick += () =>
                {
                    FGTBase.StateManager.ExploreState.RequestNewRound();
                };

                UIFactory.SetLayoutElement(EndlessExploreBtn.GameObject, 30, 20, null, 0, null, null, null);
                RequestRandomRound.GameObject.SetActive(false);

                var upPromptTxt = UIFactory.CreateHorizontalGroup(unityLoaderGUI, "upPromptTxt", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                NewGUI.Instance.AssignToGroups(upPromptTxt.gameObject, new()
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
                NewGUI.Instance.AssignToGroups(advMode.gameObject, new()
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
            NewGUI.Instance.TryDrawUI(() => FGTTargetSettings.RoundLoader, gameplayGUI, new(() =>
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
                            mapping.SwitchableSetHolder?.gameObject.SetActive(toggled);
                        }
                    }
                };
                UIFactory.SetLayoutElement(toggleBtn.GameObject, minWidth: 60, minHeight: 25, preferredWidth: 170);
            }));
            #endregion

            #region ROUND LOADER - CREATIVE
            NewGUI.Instance.TryDrawUI(() => FGTTargetSettings.RoundLoader, fgcGUI, new(() =>
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
                        //FGTBase.StateManager.InternalState.shouldSkipErrors = true;
                        levelInfo.text = LocalizedStr("fgc_level_load");
                        Service.GetOnlyLevelDto(x);
                    }
                });
                UIFactory.SetLayoutElement(fgcInputfield.Component.gameObject, minWidth: 100, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);

                //InputFieldRef lvlVersion = UIFactory.CreateInputField(fgcInput, "ver", $"{0}");
                //lvlVersion.Text = "0";
                //lvlVersion.Component.characterLimit = 5;
                //lvlVersion.OnValueChanged += input => { lvlVersion = int.Parse(input); };
                //UIFactory.SetLayoutElement(lvlVersion.Component.previewTheme, minWidth: 20, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);

                var loadingBtnsFGC = UIFactory.CreateHorizontalGroup(basicGroup, "loadingBtnsFGC", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                NewGUI.Instance.AssignToGroups(loadingBtnsFGC, new()
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
                    Service.LoadFGCRound(code, null, true);
                };

                ButtonRef explorePlay = UIFactory.CreateButton(loadingBtnsFGC, "explore", $"{LocalizedStr("gui_play_explore")}");
                explorePlay.OnClick += () =>
                {
                    if (OnlineCheck != null && OnlineCheck.ExploreCodes != null && OnlineCheck.ExploreCodes.Count > 0)
                    {
                        if (FGTStateManager.IsInIllegalState) return;
                        DoModal(new(LocalizedStr("explore_start_title"), LocalizedStr("explore_start_desc") + "\n\n" + LocalizedStr("gui_explore_desc_base"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, new Action<bool>(wasok =>
                        {
                            if (wasok)
                                StateManager.TryJoinExplore(UltimatePartyState.JoinPolicy.FGC);
                        }), hideLvl: ModalHideGUIType.ShowOnCancel));
                    }
                    else
                        DoModal(new(LocalizedStr("gui_explore_error_title"), LocalizedStr("gui_explore_error_desc"), UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Default, hideLvl: ModalHideGUIType.KeepHiddenForThisModal));
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
            NewGUI.Instance.TryDrawUI(() => FGTTargetSettings.RoundRules, loadOptionsGUI_Content, new(() =>
            {
                var latestOptions = FGTBase.FGTServiceManager.GetService<RoundOptionsService>().ReturnLatestOptions();

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
                    FGTBase.FGTServiceManager.GetService<RoundOptionsService>().WriteData();
                };
                UIFactory.SetLayoutElement(loadOptions_SaveBtn.GameObject, minWidth: 120, preferredWidth: 9999, preferredHeight: 20, minHeight: 20);
            }));
            #endregion

            Service.SetUIReferences([
                roundNamesDrop,
                    roundVariantsDrop,
                    inputFieldRef,
                    stats,
                    levelInfo
                ]);
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

            List<string> lines = new List<string>(File.ReadAllLines(Launcher.VariantData));

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
            File.WriteAllLines(Launcher.VariantData, newLines.ToArray());
        }
        void SetVariants()
        {
            string[] data = File.ReadAllLines(Launcher.VariantData);
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

        Text fgc_desc;
        List<GameObject> fgc_history = new();
        void LoadFGCHistory()
        {
            if (FGTBase.FGTServiceManager.GetService<StatisticsService>().currentStats != null && FGTBase.FGTServiceManager.GetService<StatisticsService>().currentStats.FGCSearchHistory != null)
            {
                foreach (string code in FGTBase.FGTServiceManager.GetService<StatisticsService>().currentStats.FGCSearchHistory)
                {
                    var a = UnityEngine.Object.Instantiate(fgcHistoryPrefab);
                    a.transform.GetChild(0).gameObject.GetComponent<Text>().text = code;
                    void b() => Service.LoadFGCRound(code, null, true);
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
            void b() => Service.LoadFGCRound(newLevel, null, true);
            a.transform.GetChild(1).gameObject.GetComponent<Button>().onClick.AddListener(b);
            a.transform.SetParent(FGCHistory.transform);
            a.transform.SetSiblingIndex(0);
            fgc_history.Add(a);
        }

        internal override void Refresh()
        {
            foreach (GameObject obj in fgc_history)
                UnityEngine.Object.Destroy(obj);

            fgc_history.Clear();

            LoadFGCHistory();
        }
    }
}
