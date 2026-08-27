using FGClient;
using FGClient.UI;
using FGTools.Content;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using FGTools.UI.Tabs.Logic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.Services.MenuThemeService;
using static FGTools.UI.NewGUI;

namespace FGTools.UI.Tabs
{
    internal class MiscTab : UITab
    {
        public MiscTab() : base(Tab.Misc)
        {

        }

        Dropdown _langDropdown;
        string _selectedLang = null;
        Text _langAuthor;
        Text _statistics;
        Text autosaveInfo;

        internal override void Draw(GameObject root)
        {
            ControlledObject = UIFactory.CreateVerticalGroup(root, $"Tab_{Tab}", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(ControlledObject, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);

            GameObject scrollview = UIFactory.CreateScrollView(ControlledObject, "MiscGUI", out GameObject MiscContent, out _, new(0.1f, 0.1f, 0.1f));
            MiscContent.GetComponent<VerticalLayoutGroup>().spacing = 2;

            #region THEMES
            var themeService = FGTBase.FGTServiceManager.GetService<MenuThemeService>();
            Text themesTitle = UIFactory.CreateLabel(MiscContent, "ThemesTitle", LocalizedStr("themes_selector_title"), TextAnchor.UpperCenter);
            UIFactory.SetLayoutElement(themesTitle.gameObject, minHeight: 20);

            Text themesDisabled = UIFactory.CreateLabel(MiscContent, "ThemesTitle_Disabled", LocalizedStr("gui_only_in_menu"), TextAnchor.UpperCenter);
            UIFactory.SetLayoutElement(themesDisabled.gameObject, minHeight: 20);
            themesDisabled.gameObject.SetActive(false);

            var linearGraidentImage = Resources.FindObjectsOfTypeAll<Sprite>().ToList().Find(x => x.name == "UI_LinearGradient_Image");
            Theme theme = null;
            Color placeholderCol = Color.magenta;
            if (Config.Config.InGameTheme.Value != LocalizedStr("gui_default"))
                theme = themeService.CurrentTheme;

            NewGUI.Instance.TryDrawUI(() => FGTTargetSettings.CustomThemes, MiscContent, new(() =>
            {
                var holder = UIFactory.CreateUIObject("ThemeSwitcher", MiscContent);
                var vert = holder.AddComponent<VerticalLayoutGroup>();
                vert.spacing = 2;
                vert.padding.bottom = 5;

                NewGUI.Instance.AssignToGroups(holder, new()
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
                UIFactory.CreateDropdown(selectionRow, "Themes", out var themesDropdown, "", 14, themeService.PreviewTheme);
                UIFactory.CreateDropdown(selectionRow, "WebThemes", out var webthemesDropdown, "", 14, themeService.PickOnlineTheme);
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
                selectWebTheme.OnClick += themeService.startDownloading;
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
                    var target = Launcher.ThemesDir + themeService.ThemeOnPreviewPath.Split('\\')[0];
                    Application.OpenURL(target);
                };

                var delThemeDirBtn = UIFactory.CreateButton(themeActions, "delThemeDirBtn", LocalizedStr("gui_theme_del"), GUIRed);
                UIFactory.SetLayoutElement(delThemeDirBtn.Component.gameObject, 100, 25, 0, 0);
                delThemeDirBtn.OnClick += themeService.DeleteThemeAction;

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
                catalogueToggle.onValueChanged.AddListener(new Action<bool>((val) =>
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

                        themeActions.SetActive(!themeService.IsOnDefaultTheme);
                        inftxt.text = LocalizedStr("gui_viewing") + ": " + LocalizedStr("gui_local_themes");
                        themesDropdown.gameObject.SetActive(true);
                        selectButton.GameObject.SetActive(true);
                        previewTheme.gameObject.SetActive(true);
                    }
                }));

                themeService.SetUIReferences([
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

            NewGUI.Instance.TryDrawUI(() => FGTTargetSettings.LangSwitcher, MiscContent, new(() =>
            {
                GameObject langRow = UIFactory.CreateHorizontalGroup(MiscContent, "Selection Row", false, false, true, true, 2, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
                UIFactory.CreateDropdown(langRow, "Languages", out _langDropdown, "", 14, PickLanguage);
                UIFactory.SetLayoutElement(_langDropdown.gameObject, 150, 25, 150, 0, 99999);

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
                langDirBtn.OnClick += () => { Application.OpenURL(Launcher.LocalizationDir + _selectedLang); };

                _langAuthor = UIFactory.CreateLabel(MiscContent, "langAuthor", LocalizedStr("gui_localization_author_v2"), TextAnchor.UpperCenter);
                UIFactory.SetLayoutElement(_langAuthor.gameObject, minHeight: 20);
                _langAuthor.gameObject.SetActive(false);
                selectLangButton.OnClick += TryToSetLang;
                var warn = UIFactory.CreateLabel(MiscContent, "warn", OnlineCheck.FGTContent.LocaleConfig.ParseOfficialLanguagesStr(LocalizedStr("gui_localization_note")), TextAnchor.LowerCenter);
                warn.fontSize = 12;
                warn.fontStyle = FontStyle.Italic;
                UIFactory.SetLayoutElement(warn.gameObject, minHeight: 20);
            }));

            //STATS
            Text statisticsTitle = UIFactory.CreateLabel(MiscContent, "StatisticsTitle", LocalizedStr("statistics_title"), TextAnchor.UpperCenter);
            UIFactory.SetLayoutElement(statisticsTitle.gameObject, minHeight: 20);

            Text roundHistoryText = null;
            ButtonRef historyMinus = null;
            Text displayInfo = null;
            ButtonRef historyPlus = null;
            ButtonRef toggleHistory = null;
            GameObject roundHistory = null;
            GameObject historyActions = null;

            NewGUI.Instance.TryDrawUI(() => FGTTargetSettings.Statistics, MiscContent, new(() =>
            {
                var statService = FGTServiceManager.GetService<StatisticsService>();
       
                _statistics = UIFactory.CreateLabel(MiscContent, "stats", "0", TextAnchor.MiddleLeft);
                toggleHistory = UIFactory.CreateButton(MiscContent, "ToggleHistory", $"{LocalizedStr("gui_open_rhistory")} ▼", new Color(0.2f, 0.3f, 0.2f));
                UIFactory.SetLayoutElement(toggleHistory.Component.gameObject, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);
                toggleHistory.OnClick += () =>
                {
                    if (!roundHistory.gameObject.activeSelf)
                    {
                        toggleHistory.ButtonText.text = LocalizedStr("gui_close_rhistory") + " ▲";
                        roundHistory.gameObject.SetActive(true);

                        if (FGTServiceManager.GetService<StatisticsService>().HistoryPages.Count > 0)
                            historyActions.SetActive(true);
                    }
                    else
                    {
                        toggleHistory.ButtonText.text = LocalizedStr("gui_open_rhistory") + " ▼";
                        roundHistory.gameObject.SetActive(false);
                        historyActions.SetActive(false);
                    }
                    FGTBase.FGTServiceManager.GetService<StatisticsService>().LoadPage();
                };

                roundHistory = UIFactory.CreateScrollView(MiscContent, "roundHistory", out GameObject historycontent, out _, new(0.1f, 0.1f, 0.1f));
                UIFactory.SetLayoutElement(roundHistory, minHeight: 102, preferredHeight: 102);

                historycontent.GetComponent<VerticalLayoutGroup>().spacing = 2;
                roundHistoryText = UIFactory.CreateLabel(historycontent, "roundHistory", "Not parsed yet", TextAnchor.UpperLeft);
                roundHistory.gameObject.SetActive(false);
                roundHistoryText.fontSize = 10;

                historyActions = UIFactory.CreateHorizontalGroup(MiscContent, "HistoryActions", false, false, true, true, 2, bgColor: new Color(0.07f, 0.07f, 0.07f, 1));
                historyActions.gameObject.GetComponent<Image>().enabled = false;
                historyMinus = UIFactory.CreateButton(historyActions, "Help", "<--");
                historyMinus.OnClick = () => { FGTBase.FGTServiceManager.GetService<StatisticsService>().HistoryNavBack(); };
                UIFactory.SetLayoutElement(historyMinus.Component.gameObject, 50, 25, 50, 0, 50);

                displayInfo = UIFactory.CreateLabel(historyActions, "DisplayInfo", "Displaying 102 elements out of 999", TextAnchor.MiddleCenter);
                UIFactory.SetLayoutElement(displayInfo.gameObject, 100, 25, 100, 0, 99999);

                historyPlus = UIFactory.CreateButton(historyActions, "Help", "-->");
                historyPlus.OnClick = () => { FGTBase.FGTServiceManager.GetService<StatisticsService>().HistoryNavForward(); };
                UIFactory.SetLayoutElement(historyPlus.Component.gameObject, 50, 25, 50, 0, 50);
                historyActions.gameObject.SetActive(false);

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

                statService.SetUIReferences(
                [
                    roundHistoryText,
                    historyMinus,
                    displayInfo,
                    historyPlus,
                    toggleHistory,
                    roundHistory,
                    historyActions
                ]);
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
                NewGUI.Instance.RefreshEverything();
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

        internal override void Update()
        {
            var themeService = FGTBase.FGTServiceManager.GetService<MenuThemeService>();
            themeService.UpdateInfo();

            var statsJson = FGTBase.FGTServiceManager.GetService<StatisticsService>().CurrentStats;

            var ingame = TimeSpan.FromSeconds(statsJson.TimeInGame);
            var inmenu = TimeSpan.FromSeconds(statsJson.TimeInMenu);
            var infgc = TimeSpan.FromSeconds(statsJson.TimeInFGC);

            _statistics.text =
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


            autosaveInfo.text = $"{LocalizedStr("stats_save_time_remain")}: {TimeSpan.FromSeconds(FGTServiceManager.GetService<StatisticsService>().SaveTime - FGTServiceManager.GetService<StatisticsService>().TimeElapsed):mm':'ss}";
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
            _langDropdown.AddOptions(target);
            int currLangIndx = KnownLocales.IndexOf(Config.Config.LangFileName.Value.ToUpper());
            _langDropdown.value = currLangIndx + 1;
        }

        void PickLanguage(int index)
        {
            if (index > 0)
            {
                _selectedLang = OnlineCheck.FGTContent.LocaleConfig[index - 1].ForLang.ToLower();
                if (_langAuthor != null)
                {
                    _langAuthor.gameObject.SetActive(true);
                    _langAuthor.text = $"{LocalizedStr("gui_localization_author_v2", [OnlineCheck.FGTContent.LocaleConfig.ReturnTranslateAuthor(_selectedLang), OnlineCheck.FGTContent.LocaleConfig.ReturnLastUpdate(_selectedLang)])}";
                }
            }
            else
                _langAuthor.gameObject.SetActive(false);
        }

        void TryToSetLang()
        {
            if (_selectedLang != null && Config.Config.LangFileName.Value != _selectedLang)
            {
                if (SceneManager.GetActiveScene().name == "MainMenu" && FGTBase.StateManager.FGTCurrentState == FGTStateManager.ToolsState.Menu)
                {
                    DoModal(new(LocalizedStr("gui_localization_act0"), LocalizedStr("gui_localization_act1"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Positive, new Action<bool>((bool wasok) =>
                    {
                        if (wasok)
                        {
                            OnlineCheck.DownloadNewLang(_selectedLang, new(() =>
                            {
                                NewGUI.Instance.RefreshEverything();
                                FGTServiceManager.OnGUIDestroyed();

                                NewGUI.Instance.UIRoot.hideFlags = HideFlags.HideAndDontSave;
                                NewGUI.Instance.UIRoot.transform.SetParent(null);
                                NewGUI.Instance.UIRoot.gameObject.SetActive(false);
                                NewGUI.Instance = null;

                                StateManager.LoggedInBefore = false;
                                FGTServiceManager.GetService<LocalizationService>().SetupLocalization(Path.Combine(Launcher.LocalizationDir, _selectedLang, "locale.json"));

                                Config.Config.LangFileName.Value = _selectedLang;
                                GlobalGameStateClient.Instance._mainMenuManager.ShowMainMenu(true, true, false);
                            }));
                        }
                    }), hideLvl: ModalHideGUIType.KeepHiddenForThisModal));
                }
                else
                    DoModal(new(LocalizedStr("gui_unavailable"), LocalizedStr("gui_localization_act2"), UIModalMessage.ModalType.MT_OK, UIModalMessage.OKButtonType.Default, hideLvl: ModalHideGUIType.KeepHidden));

            }
        }

        internal override void Refresh()
        {
        }
    }
}
