using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using FG.Common.CMS;
using FGTools.Content;
using FGTools.Services.Logic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UniverseLib;
using UniverseLib.UI.Models;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Internal.Extensions.FLZ_UIExtensions;
using static FGTools.Services.LocalizationService;
using static FGTools.UI.ReadyPopups;

namespace FGTools.Services
{
    internal class ShowLoaderService : FGTService, IFGTGUIHelper
    {

        Dictionary<string, string> ShowID2Meta = new();
        string ShowToLoad = "show_";

        Dropdown showListDrop;
        string showimglink;
        GameObject ShowInfo;
        GameObject showRoundList;
        public Text showInfo;
        public ButtonRef randomShow;
        public string selectedShow;
        Text placeholder;
        Text showlist;
        Image showIco;

        public override void RegisterService()
        {
        }

        public void SetUIReferences(object[] data)
        {
            if (!FGTTargetSettings.ShowLoader)
                return;

            showListDrop = (Dropdown)data[0];
            showListDrop.onValueChanged.AddListener(OnShowsDropSelected);
            var search = (InputFieldRef)data[1];
            search.OnValueChanged += SearchForShow;
            showRoundList = (GameObject)data[2];
            showIco = (Image)data[3];
            randomShow = (ButtonRef)data[4];
            randomShow.OnClick = () => { FGTRoundLoader.LoadRandomCms(); };
            placeholder = (Text)data[5];
            showInfo = (Text)data[6];
            ShowInfo = (GameObject)data[7];
            showlist = (Text)data[8]; 
            showlist.fontSize = 10;
            var openImg = (ButtonRef)data[9];
            openImg.OnClick = () => { Application.OpenURL(showimglink); };
            var playBtn = (ButtonRef)data[10];
            playBtn.OnClick = () => { LoadLatestShow(); };

            RefreshShowsInfo();
            PopulateShows();
        }

        void PopulateShows()
        {
            InsertDefaultOption(showListDrop);

            if (ShowID2Meta == null || ShowID2Meta.Count == 0)
            {
                FGTLog(LogLevel.Warning, GetType(), "Tried to create show list while show info not loaded.");
                return;
            }

            Il2CppSystem.Collections.Generic.List<string> completeShowList = new Il2CppSystem.Collections.Generic.List<string>();
            foreach (var show in ShowID2Meta)
            {
                completeShowList.Add($"{show.Value} | <color=grey><i>{show.Key}</i></color>");
            }

            showListDrop.AddOptions(completeShowList);
        }

        void OnShowsDropSelected(int value)
        {
            string str = showListDrop.options[value].text;

            if (!IsValidString(str))
                return;

            var showId = Regex.Replace(str.Substring(str.IndexOf('<')).Trim(), "<.*?>", "");
            LoadShowInfoViaID(showId);
            ShowToLoad = showId;
        }

        void SearchForShow(string request)
        {
            Il2CppSystem.Collections.Generic.List<string> result = new Il2CppSystem.Collections.Generic.List<string>();
            request = request.ToLower();
            ShowToLoad = string.Empty;

            showListDrop.ClearOptions();

            InsertDefaultOption(showListDrop);

            foreach (var elem in ShowID2Meta)
            {
                if (elem.Key.ToLower().Contains(request) || elem.Value.ToLower().Contains(request))
                    result.Add($"{elem.Value} | <color=grey><i>{elem.Key}</i></color>");
            }

            ForceHideDropdown(showListDrop);

            result.Sort();
            showListDrop.AddOptions(result);
        }

        public void LoadLatestShow()
        {
            if (IsValidString(ShowToLoad) && ShowToLoad != "show_")
                StateManager.TryJoinShowGameplay(ShowToLoad);
            else
                ErrorPopup(LocalizedStr("gui_default_load"));
        }

        void RefreshShowsInfo()
        {
            ShowID2Meta.Clear();

            foreach (var show in CMSLoader.Instance.CMSData.Shows)
            {
                if (show.Value.ShowName != null && !show.key.Contains("ugc") && !show.key.Contains("wle") && !ShowID2Meta.ContainsValue(show.key))
                {
                    string showName = show.value.ShowName.Text;

                    if (showName.IsNullOrEmpty())
                        showName = LocalizedStr("gui_show_unnamed").ToUpper();

                    if (showName.Contains("<br>"))
                        showName = CleanStr(showName);

                    ShowID2Meta.Add(show.Value.Id, showName);
                }
            }
        }

        public string loadedShow;

        void LoadShowInfoViaID(string cmsid)
        {
            List<string> ShowRounds = new();
            selectedShow = string.Empty;

            var show = CMSLoader.Instance.CMSData.Shows[cmsid];

            if (show == null || show.ShowName == null)
                return;

            ShowRounds.Clear();
            selectedShow = show.Id;
            showimglink = show.ShowImageCMS.DlcItem.Base + show.ShowImageCMS.DlcItem.Path;
            CoroutineRunner.Instance.StartCoroutine(LoadShowIco(showimglink).WrapToIl2Cpp());
            ShowInfo.SetActive(true);
            showRoundList.SetActive(true);

            if (show.DefaultEpisode != null)
            {
                int roundNum = 1;
                foreach (var stage in show.DefaultEpisode.DefaultRoundPool._stages)
                {
                    if (stage.Round != null)
                    {
                        string onlyStages = null;
                        if (stage.CanOnlyBeOnTheseStages.Count > 0)
                            onlyStages = string.Join(", ", stage.CanOnlyBeOnTheseStages);

                        string cannotStages = null;
                        if (stage.CannotBeOnTheseStages.Count > 0)
                            cannotStages = string.Join(", ", stage.CannotBeOnTheseStages);

                        string onlyStagesComplete = onlyStages == null ? string.Empty : $"| {LocalizedStr("gui_show_onlyon")}: {onlyStages}";
                        string cannotStagesComplete = cannotStages == null ? string.Empty : $"| {LocalizedStr("gui_show_cannotbe")}: {string.Join(", ", cannotStages)}";

                        if (!stage.Round.IsUGC())
                        {
                            string parsedName = stage.Round.DisplayName.Text;
                            if (parsedName.Contains("<br>"))
                                parsedName = CleanStr(parsedName);

                            ShowRounds.Add($"{roundNum}. | <color=grey><i>{stage.Round.Id}</i></color> | {parsedName.ToUpper()} | <color={stage.Round.Archetype.TagColour}>{stage.Round.Archetype.Name.ToUpper()}</color> {onlyStagesComplete} {cannotStagesComplete}");
                        }
                        else
                            ShowRounds.Add($"{roundNum}. | <color=grey><i>{stage.Round.Id}</i></color> | {stage.Round.SceneData.DlcLevel.ShareCode} {onlyStagesComplete} {cannotStagesComplete}");

                        roundNum++;
                    }
                }
            }

            placeholder.text = $"{LocalizedStr("gui_desc")} " + show.ShowDescription;
            showInfo.text = $"{LocalizedStr("gui_loaded_show")}: {show.ShowName.Text}";

            showlist.text = string.Join("\n", ShowRounds.ToArray());

        }

        IEnumerator LoadShowIco(string url)
        {
            UnityWebRequest translatorsRequest = UnityWebRequest.Get(url);

            yield return translatorsRequest.SendWebRequest();

            if (translatorsRequest.result != UnityWebRequest.Result.Success)
            {
                FGTLog(LogLevel.Warning, GetType(), "Unable to load show icon");
            }
            else
            {
                showIco.sprite = SetSpriteFromBytes(translatorsRequest.downloadHandler.data, 168, 170);
            }
        }

        public override void DrawGUI()
        {
        }

        public void RefreshUI()
        {
            RefreshShowsInfo();
            PopulateShows();
        }

        public override void UpdateService()
        {
        }

        public void OnUIDestroy()
        {
        }

        public void OnUICreated()
        {
        }

        public override void OnAppFocus(bool focus)
        {
        }

        public override void OnAppQuit()
        {
        }
    }
}
