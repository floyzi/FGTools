using BepInEx.Logging;
using Catapult.Modules.Items.Protocol.Dtos;
using Coffee.UIParticleInternal;
using DG.Tweening;
using Epic.OnlineServices;
using Events;
using FallGuys.Player.Protocol.Client.Cosmetics;
using FG.Common;
using FG.Common.CMS;
using FG.Common.Definition;
using FGClient;
using FGClient.CatapultServices;
using FGClient.Customiser;
using FGClient.UI;
using FGTools.Internal.Behaviours;
using FGTools.Internal.Extensions;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using FGTools.UI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using static FGTools.Config.Config;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static Il2CppSystem.Globalization.TimeSpanFormat;
namespace FGTools.Services
{
    //this whole class was terribly written, i'll rework this someday
    //at least it works
    internal class CosmeticsService : FGTService
    {
        public enum RequestType
        {
            Locker,
            List
        }

        Il2CppSystem.Collections.Generic.List<ColourSchemeDto> UserColors = new();
        readonly Il2CppSystem.Collections.Generic.List<ColourSchemeDto> AllColors = new();

        Il2CppSystem.Collections.Generic.List<FaceplateDto> UserFaceplates = new();
        readonly Il2CppSystem.Collections.Generic.List<FaceplateDto> AllFaceplates = new();

        Il2CppSystem.Collections.Generic.List<PatternDto> UserPatterns = new();
        readonly Il2CppSystem.Collections.Generic.List<PatternDto> AllPatterns = new();

        Il2CppSystem.Collections.Generic.List<UpperCostumePieceDto> UserUppers = new();
        readonly Il2CppSystem.Collections.Generic.List<UpperCostumePieceDto> AllUppers = new();

        Il2CppSystem.Collections.Generic.List<LowerCostumePieceDto> UserLowers = new();
        readonly Il2CppSystem.Collections.Generic.List<LowerCostumePieceDto> AllLowers = new();

        Il2CppSystem.Collections.Generic.List<EmoteDto> UserEmotes = new();
        readonly Il2CppSystem.Collections.Generic.List<EmoteDto> AllEmotes = new();

        Il2CppSystem.Collections.Generic.List<NameplateDto> UserNameplates = new();
        readonly Il2CppSystem.Collections.Generic.List<NameplateDto> AllNameplates = new();

        Il2CppSystem.Collections.Generic.List<PunchlineDto> UserPunchlines = new();
        readonly Il2CppSystem.Collections.Generic.List<PunchlineDto> AllPunchlines = new();

        Il2CppSystem.Collections.Generic.List<NicknameDto> UserNicknames = new();
        readonly Il2CppSystem.Collections.Generic.List<NicknameDto> AllNicknames = new();

        Il2CppSystem.Collections.Generic.List<EmoticonDto> UserEmoticons = new();
        readonly Il2CppSystem.Collections.Generic.List<EmoticonDto> AllEmoticons = new();

        Il2CppSystem.Collections.Generic.List<PhraseDto> UserPhrases = new();
        readonly Il2CppSystem.Collections.Generic.List<PhraseDto> AllPhrases = new();

        CustomiserColourSection ColorSect;
        CustomiserFaceplateSection FaceSect;
        CustomiserPatternsSection PatternsSect;
        CustomiserUpperCostumeSection UpperCostumeSect;
        CustomiserLowerCostumeSection LowerCostumeSect;
        CustomiserEmotesSection EmotesSect;
        CustomiserNameplateSection NameplateSect;
        CustomiserNicknameSection NicknameSect;
        CustomiserVictorySection VictorySect;
        CustomiserEmoticonsSection EmoticonsSect;
        CustomiserPhrasesSection PhrasesSect;

        internal Dictionary<CustomiserMenuViewModel, CustomiserSubScreenViewModel> Screens = [];

        OutfitMenuViewModel OutfitMenuViewModel;
        InterfaceMenuViewModel InterfaceMenuViewModel;
        TheatricsMenuViewModel TheatricsMenuViewModel;

        OutfitMenuFocusableViewModel OutfitMenuFocusable;
        InterfaceMenuFocusableViewModel InterfaceMenuFocusable;
        TheatricsMenuFocusableViewModel TheatricsMenuFocusable;

        internal static HashSet<string> FavList { get; set; }
        GameObject _searchPrefab;
        bool Loaded;
        string _recentQuery;
        public static bool _searchActive = false;

        public override void RegisterService()
        {

        }

        void ResetFavList()
        {
            FGTLog(LogLevel.Warning, base.GetType(), $"Failed to recover fav list.");

            if (File.Exists(Launcher.CustomFavList))
                File.Delete(Launcher.CustomFavList);

            FavList = [];
            var stats = JsonSerializer.Serialize(FavList);
            File.WriteAllText(Launcher.CustomFavList, stats);
        }

        public void Load()
        {
            if (Loaded)
            {
                FGTLog(LogLevel.Info, base.GetType(), "Already loaded, finishing.");
                EndLoad();
                return;
            }

            FGTLog(LogLevel.Info, base.GetType(), "Load");
            var favIds = new HashSet<string>();

            try
            {
                try
                {
                    if (!File.Exists(Launcher.CustomFavList))
                    {
                        var stats = JsonSerializer.Serialize(FavList);
                        File.WriteAllText(Launcher.CustomFavList, stats);
                    }
                    else
                        FavList = JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(Launcher.CustomFavList));
                }
                catch
                {
                    ResetFavList();
                }

                if (FavList == null)
                    ResetFavList();

                var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;

                //colors
                UserColors = cos.ColourSchemes;

                foreach (var res in PushCosmList<ColourOption, ColourSchemeDto>(UserColors, def => def.CMSData, itemDto => ItemDtoToColourSchemeDto(itemDto), dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllColors.Add(res);

                //patterns
                UserPatterns = cos.Patterns;

                foreach (var res in PushCosmList<SkinPatternOption, PatternDto>(UserPatterns, def => def.CMSData, itemDto => ItemDtoToPatternDto(itemDto), dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllPatterns.Add(res);

                //faces
                UserFaceplates = cos.Faceplates;

                foreach (var res in PushCosmList<FaceplateOption, FaceplateDto>(UserFaceplates, def => def.CMSData, itemDto => ItemDtoToFaceplateDto(itemDto), dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllFaceplates.Add(res);

                //uppers
                UserUppers = cos.UpperCostumePieces;

                foreach (var res in PushCosmList<CostumeOption, UpperCostumePieceDto>(UserUppers, def => def.CMSData, itemDto => ItemDtoToCostumeUpperDto(itemDto), dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllUppers.Add(res);

                //lowers
                UserLowers = cos.LowerCostumePieces;

                foreach (var res in PushCosmList<CostumeOption, LowerCostumePieceDto>(UserLowers, def => def.CMSData, itemDto => ItemDtoToCostumeLowerDto(itemDto), dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllLowers.Add(res);


                //nameplates
                UserNameplates = cos.Nameplates;

                foreach (var res in PushCosmList<NameplateOption, NameplateDto>(UserNameplates, def => def.CMSData, itemDto => ItemDtoToNameplateDto(itemDto), dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllNameplates.Add(res);

                //emotes
                UserEmotes = cos.Emotes;

                foreach (var res in PushCosmList<EmotesOption, EmoteDto>(UserEmotes, def => def.CMSData, itemDto => ItemDtoToEmoteDto(itemDto), dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllEmotes.Add(res);


                //victory poses
                UserPunchlines = cos.Punchlines;

                foreach (var res in PushCosmList<VictoryOption, PunchlineDto>(UserPunchlines, def => def.CMSData, itemDto => ItemDtoToVictoryDto(itemDto), dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllPunchlines.Add(res);

                //nicknames
                UserNicknames = cos.Nicknames;

                BuildCosmList(UserNicknames, AllNicknames, dto => dto.Item, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val, () => Resources.FindObjectsOfTypeAll<NicknamesSO>().FirstOrDefault(), so =>
                {
                    var list = new List<Nickname>();
                    foreach (var pair in so.Nicknames)
                        list.Add(pair.Value);
                    return list;
                }, obj => (CMSItemDefinition)(Il2CppSystem.Object)obj, itemDto => ItemDtoToNicknameDto(itemDto), favIds, FavList);

                //emoticons
                UserEmoticons = cos.Emoticons;

                BuildCosmList(UserEmoticons, AllEmoticons, dto => dto.Item, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val, () => Resources.FindObjectsOfTypeAll<CosmeticsEmoticonsSO>().FirstOrDefault(), so =>
                {
                    var list = new List<CustomiserEmoticons>();
                    foreach (var pair in so.Emoticons)
                        list.Add(pair.Value);
                    return list;
                }, obj => (CMSItemDefinition)(Il2CppSystem.Object)obj, itemDto => ItemDtoToEmoticonDto(itemDto), favIds, FavList);

                //phrases
                UserPhrases = cos.Phrases;

                BuildCosmList(UserPhrases, AllPhrases, dto => dto.Item, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val, () => Resources.FindObjectsOfTypeAll<CosmeticsPhrasesSO>().FirstOrDefault(), so =>
                {
                    var list = new List<CustomiserPhrases>();
                    foreach (var pair in so.Phrases)
                        list.Add(pair.Value);
                    return list;
                }, obj => (CMSItemDefinition)(Il2CppSystem.Object)obj, itemDto => ItemDtoToPhraseDto(itemDto), favIds, FavList);

                EndLoad();

                Loaded = true;

                if (AllCosmetics.Value)
                    GrantAllCosmetics();
                else
                    RemoveAllCosmetics();
            }
            catch (Exception e) { FGTLog(LogLevel.Error, GetType(), e); }
        }

        void EndLoad()
        {
            ColorSect = Resources.FindObjectsOfTypeAll<CustomiserColourSection>().FirstOrDefault();
            PatternsSect = Resources.FindObjectsOfTypeAll<CustomiserPatternsSection>().FirstOrDefault();
            FaceSect = Resources.FindObjectsOfTypeAll<CustomiserFaceplateSection>().FirstOrDefault();
            UpperCostumeSect = Resources.FindObjectsOfTypeAll<CustomiserUpperCostumeSection>().FirstOrDefault();
            LowerCostumeSect = Resources.FindObjectsOfTypeAll<CustomiserLowerCostumeSection>().FirstOrDefault();
            NameplateSect = Resources.FindObjectsOfTypeAll<CustomiserNameplateSection>().FirstOrDefault();
            VictorySect = Resources.FindObjectsOfTypeAll<CustomiserVictorySection>().FirstOrDefault();
            EmotesSect = Resources.FindObjectsOfTypeAll<CustomiserEmotesSection>().FirstOrDefault();
            NicknameSect = Resources.FindObjectsOfTypeAll<CustomiserNicknameSection>().FirstOrDefault();
            EmoticonsSect = Resources.FindObjectsOfTypeAll<CustomiserEmoticonsSection>().FirstOrDefault();
            PhrasesSect = Resources.FindObjectsOfTypeAll<CustomiserPhrasesSection>().FirstOrDefault();

            OutfitMenuViewModel = Resources.FindObjectsOfTypeAll<OutfitMenuViewModel>().FirstOrDefault();
            InterfaceMenuViewModel = Resources.FindObjectsOfTypeAll<InterfaceMenuViewModel>().FirstOrDefault();
            TheatricsMenuViewModel = Resources.FindObjectsOfTypeAll<TheatricsMenuViewModel>().FirstOrDefault();

            OutfitMenuFocusable = OutfitMenuViewModel.gameObject.GetComponent<OutfitMenuFocusableViewModel>();
            InterfaceMenuFocusable = InterfaceMenuViewModel.gameObject.GetComponent<InterfaceMenuFocusableViewModel>();
            TheatricsMenuFocusable = TheatricsMenuViewModel.gameObject.GetComponent<TheatricsMenuFocusableViewModel>();

            Screens.Clear();
            Screens.Add(OutfitMenuViewModel, OutfitMenuFocusable);
            Screens.Add(InterfaceMenuViewModel, InterfaceMenuFocusable);
            Screens.Add(TheatricsMenuViewModel, TheatricsMenuFocusable);

            OutfitMenuViewModel.gameObject.SetActive(false);
            InterfaceMenuViewModel.gameObject.SetActive(false);
            TheatricsMenuViewModel.gameObject.SetActive(false);

            MakeUI();
        }

        void MakeUI()
        {
            if (_searchPrefab == null)
            {
                var inputField = Resources.FindObjectsOfTypeAll<UICustomTMP_InputField>().FirstOrDefault(x => x.transform.parent?.parent?.name == "CodeInputField");
                _searchPrefab = GameObject.Instantiate(inputField.transform.parent.parent.gameObject);
                var input = _searchPrefab.GetComponentInChildren<UICustomTMP_InputField>();
                input.contentType = TMP_InputField.ContentType.Standard;
                var placeholder = input.placeholder.GetComponent<TextMeshProUGUI>();
                placeholder.color = new(1, 1, 1, 0.3f);
                placeholder.text = LocalizedStr("inputfield_placeholder");
                placeholder.fontStyle = FontStyles.Normal;
                _searchPrefab.hideFlags = HideFlags.HideAndDontSave;
                GameObject.DontDestroyOnLoad(_searchPrefab);
            }

            foreach (var screen in Screens)
            {
                var initTest = screen.Value.transform.GetChild(0).transform;
                var searchBar = GameObject.Instantiate(_searchPrefab, initTest);

                var rt = searchBar.gameObject.GetComponent<RectTransform>();
                rt.anchorMax = new(0.4f, 1);
                rt.anchorMin = new(0, 1);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -30);

                searchBar.AddComponent<CosmeticSearchBar>();
            }
        }

        void OnGUI()
        {
            //if (FGTCurrentState == FGTState.Menu)
            //{
            //    if (IsSomeScreenActive())
            //    {
            //        GUI.Label(new Rect(15f, 80f, 240f, 20f), output);
            //        searchTerm = GUI.TextField(new Rect(15f, 100f, 240f, 40f), searchTerm);
            //        if (GUI.Button(new Rect(15f, 140f, 240f, 40f), "Search"))
            //            Search(searchTerm, GetSection());
            //        nav = GUI.Toggle(new Rect(15f, 80f, 240f, 20f), nav, "Disable navigation");
            //    }
            //}
        }

        public override void UpdateService()
        {
            if (StateManager.FGTCurrentState == FGTStateManager.ToolsState.Menu)
            {
                if (_searchActive)
                    Broadcaster.Instance.RaiseEvent(new NavPromptChanged(new Il2CppSystem.Collections.Generic.Dictionary<NavPrompt, Il2CppSystem.Action>()));
            }
        }

        public void SearchStart()
        {
            _searchActive = true;
            foreach (var pair in Screens)
            {
                if (pair.Value.gameObject.activeSelf)
                {
                    if (pair.Value is OutfitMenuFocusableViewModel outfitMenuFocusable)
                        outfitMenuFocusable.enabled = false;
                    else if (pair.Value is InterfaceMenuFocusableViewModel interfaceMenuFocusable)
                        interfaceMenuFocusable.enabled = false;
                    else if (pair.Value is TheatricsMenuFocusableViewModel theatricsMenuFocusable)
                        theatricsMenuFocusable.enabled = false;
                }
            }
        }

        public void SearchEnd(bool resetTerm)
        {
            try
            {
                _searchActive = false;
                var a = Resources.FindObjectsOfTypeAll<CustomiserScreenViewModel>().FirstOrDefault();
                a?.OnGainFocus();
                foreach (var pair in Screens)
                {
                    if (pair.Value.gameObject.activeSelf)
                    {
                        if (pair.Value is OutfitMenuFocusableViewModel outfitMenuFocusable)
                            outfitMenuFocusable.enabled = true;
                        else if (pair.Value is InterfaceMenuFocusableViewModel interfaceMenuFocusable)
                            interfaceMenuFocusable.enabled = true;
                        else if (pair.Value is TheatricsMenuFocusableViewModel theatricsMenuFocusable)
                            theatricsMenuFocusable.enabled = true;
                    }
                }
                if (resetTerm)
                {
                    if (AllCosmetics.Value)
                        GrantAllCosmetics();
                    else
                        RemoveAllCosmetics();
                }   
            }
            catch
            {
                if (AllCosmetics.Value)
                    GrantAllCosmetics();
                else
                    RemoveAllCosmetics();
            }
        }

        public string GetSection()
        {
            if (OutfitMenuViewModel.gameObject.activeSelf)
                return OutfitMenuViewModel.CurrentSectionText;
            else if (TheatricsMenuViewModel.gameObject.activeSelf)
                return TheatricsMenuViewModel.CurrentSectionText;
            else if (InterfaceMenuViewModel.gameObject.activeSelf)
                return InterfaceMenuViewModel.CurrentSectionText;

            return null;
        }


        internal void ResumeSearch()
        {
            if (string.IsNullOrEmpty(_recentQuery)) return;
            Search(_recentQuery, GetSection(), RequestType.Locker, Config.Config.AllCosmetics.Value);
        }

        internal void ResolveSections()
        {
            var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;

            if (cos.ColourSchemes == null || cos.ColourSchemes.Count == 0)
                cos.ColourSchemes = AllCosmetics.Value ? AllColors : UserColors;

            if (cos.Patterns == null || cos.Patterns.Count == 0)
                cos.Patterns = AllCosmetics.Value ? AllPatterns : UserPatterns;

            if (cos.Faceplates == null || cos.Faceplates.Count == 0)
                cos.Faceplates = AllCosmetics.Value ? AllFaceplates : UserFaceplates;

            if (cos.UpperCostumePieces == null || cos.UpperCostumePieces.Count == 0)
                cos.UpperCostumePieces = AllCosmetics.Value ? AllUppers : UserUppers;

            if (cos.LowerCostumePieces == null || cos.LowerCostumePieces.Count == 0)
                cos.LowerCostumePieces = AllCosmetics.Value ? AllLowers : UserLowers;

            if (cos.Emotes == null || cos.Emotes.Count == 0)
                cos.Emotes = AllCosmetics.Value ? AllEmotes : UserEmotes;

            if (cos.Punchlines == null || cos.Punchlines.Count == 0)
                cos.Punchlines = AllCosmetics.Value ? AllPunchlines : UserPunchlines;

            if (cos.Emoticons == null || cos.Emoticons.Count == 0)
                cos.Emoticons = AllCosmetics.Value ? AllEmoticons : UserEmoticons;

            if (cos.Phrases == null || cos.Phrases.Count == 0)
                cos.Phrases = AllCosmetics.Value ? AllPhrases : UserPhrases;

            if (cos.Nicknames == null || cos.Nicknames.Count == 0)
                cos.Nicknames = AllCosmetics.Value ? AllNicknames : UserNicknames;

            if (cos.Nameplates == null || cos.Nameplates.Count == 0)
                cos.Nameplates = AllCosmetics.Value ? AllNameplates : UserNameplates;
        }

        //i hate this actually
        public void Search(string req, string type, RequestType reqType, bool allCosmetics)
        {
            _recentQuery = req;

            if (string.IsNullOrEmpty(req))
            {
                if (AllCosmetics.Value)
                    GrantAllCosmetics();
                else
                    RemoveAllCosmetics();
                return;
            }

            var term = req.ToUpper();
            HashSet<string> ids = [];
            HashSet<string> fav_ids = [];
            HashSet<string> finalRes = [];
            HashSet<string> foundIds = [];
            List<object> listedRes = [];

            var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;

            try
            {
                switch (type)
                {
                    case "colour":
                        DoSearch<ColourSchemeDto, ColourOption>(term, reqType, allCosmetics, AllColors, UserColors, Resources.FindObjectsOfTypeAll<ColourOption>(), dto => dto.Item, targ => targ, itemDto => ItemDtoToColourSchemeDto(itemDto), list => cos.ColourSchemes = list, ref finalRes, ref listedRes);
                        break;
                    case "pattern":
                        DoSearch<PatternDto, SkinPatternOption>(term, reqType, allCosmetics, AllPatterns, UserPatterns, Resources.FindObjectsOfTypeAll<SkinPatternOption>(), dto => dto.Item, targ => targ, itemDto => ItemDtoToPatternDto(itemDto), list => cos.Patterns = list, ref finalRes, ref listedRes);
                        break;
                    case "face":
                        DoSearch<FaceplateDto, FaceplateOption>(term, reqType, allCosmetics, AllFaceplates, UserFaceplates, Resources.FindObjectsOfTypeAll<FaceplateOption>(), dto => dto.Item, targ => targ, itemDto => ItemDtoToFaceplateDto(itemDto), list => cos.Faceplates = list, ref finalRes, ref listedRes);
                        break;
                    case "upper":
                        DoSearch<UpperCostumePieceDto, CostumeOption>(term, reqType, allCosmetics, AllUppers, UserUppers, Resources.FindObjectsOfTypeAll<CostumeOption>(), dto => dto.Item, targ => targ, itemDto => ItemDtoToCostumeUpperDto(itemDto), list => cos.UpperCostumePieces = list, ref finalRes, ref listedRes);
                        break;
                    case "lower":
                        DoSearch<LowerCostumePieceDto, CostumeOption>(term, reqType, allCosmetics, AllLowers, UserLowers, Resources.FindObjectsOfTypeAll<CostumeOption>(), dto => dto.Item, targ => targ, itemDto => ItemDtoToCostumeLowerDto(itemDto), res => cos.LowerCostumePieces = res, ref finalRes, ref listedRes);
                        break;
                    case "emotes":
                        DoSearch<EmoteDto, EmotesOption>(term, reqType, allCosmetics, AllEmotes, UserEmotes, Resources.FindObjectsOfTypeAll<EmotesOption>(), dto => dto.Item, targ => targ, itemDto => ItemDtoToEmoteDto(itemDto), res => cos.Emotes = res, ref finalRes, ref listedRes);
                        break;
                    case "victory":
                        DoSearch<PunchlineDto, VictoryOption>(term, reqType, allCosmetics, AllPunchlines, UserPunchlines, Resources.FindObjectsOfTypeAll<VictoryOption>(), dto => dto.Item, targ => targ, itemDto => ItemDtoToVictoryDto(itemDto), res => cos.Punchlines = res, ref finalRes, ref listedRes);
                        break;
                    case "banner":
                        DoSearch<NameplateDto, NameplateOption>(term, reqType, allCosmetics, AllNameplates, UserNameplates, Resources.FindObjectsOfTypeAll<NameplateOption>(), dto => dto.Item, targ => targ, itemDto => ItemDtoToNameplateDto(itemDto), res => cos.Nameplates = res, ref finalRes, ref listedRes);
                        break;
                    case "nickname":
                        var nicknames = Resources.FindObjectsOfTypeAll<NicknamesSO>().FirstOrDefault().Nicknames.Values;
                        Il2CppSystem.Collections.Generic.List<NicknameDto> nickname_targetList = AllNicknames;
                        if (!allCosmetics)
                            nickname_targetList = UserNicknames;

                        var nickname_result = new Il2CppSystem.Collections.Generic.List<NicknameDto>();

                        foreach (var col in nickname_targetList)
                            ids.Add(col.Item.ContentId);

                        foreach (var name in nicknames)
                        {
                            if (term != string.Empty && name.Name != null && (name.Name.Text.ToUpper().Contains(term) || name.Id.ToUpper().Contains(term)) && ids.Contains(name.Id.ToLower()))
                            {
                                foundIds.Add(name.Id);
                                if (reqType == RequestType.List)
                                    listedRes.Add(name);
                            }
                        }

                        foreach (NicknameDto item in nickname_targetList)
                        {
                            var a = item.Item.Id.Split('.')[1].ToLower();
                            if ((item.IsFavourite || FavList.Contains(item.Item.Id)) && foundIds.Contains(a))
                            {
                                nickname_result.Add(item);
                                fav_ids.Add(a);
                            }
                        }

                        foreach (Nickname nickname in nicknames)
                        {
                            if (foundIds.Contains(nickname.Id.ToLower()))
                            {
                                var newdto = ItemDtoToNicknameDto(CMSDefinitionToItemDto(nickname));
                                if (!fav_ids.Contains(newdto.Item.Id.Split('.')[1].ToLower()))
                                    nickname_result.Add(newdto);
                            }
                        }

                        foreach (var itm in nickname_result)
                            finalRes.Add(itm.Item.Id);

                        if (reqType != RequestType.List)
                        {
                            cos.Nicknames = nickname_result;
                            if (nickname_result.Count < 0)
                                cos.Nicknames = AllNicknames;
                        }
                        break;
                    case "emoticons":
                        var emoticons = Resources.FindObjectsOfTypeAll<CosmeticsEmoticonsSO>().FirstOrDefault().Emoticons.Values;
                        Il2CppSystem.Collections.Generic.List<EmoticonDto> emoticons_targetList = AllEmoticons;
                        if (!allCosmetics)
                            emoticons_targetList = UserEmoticons;

                        var emoticons_result = new Il2CppSystem.Collections.Generic.List<EmoticonDto>();

                        foreach (var col in emoticons_targetList)
                            ids.Add(col.Item.ContentId);

                        foreach (var name in emoticons)
                        {
                            if (term != string.Empty && name.Name != null && (name.Name.Text.ToUpper().Contains(term) || name.Id.ToUpper().Contains(term)) && ids.Contains(name.Id.ToLower()))
                            {
                                foundIds.Add(name.Id);
                                if (reqType == RequestType.List)
                                    listedRes.Add(name);
                            }
                        }

                        foreach (var item in emoticons_targetList)
                        {
                            var a = item.Item.Id.Split('.')[1].ToLower();
                            if ((item.IsFavourite || FavList.Contains(item.Item.Id)) && foundIds.Contains(a))
                            {
                                emoticons_result.Add(item);
                                fav_ids.Add(a);
                            }
                        }

                        foreach (var nickname in emoticons)
                        {
                            if (foundIds.Contains(nickname.Id.ToLower()))
                            {
                                var newdto = ItemDtoToEmoticonDto(CMSDefinitionToItemDto(nickname));
                                if (!fav_ids.Contains(newdto.Item.Id.Split('.')[1].ToLower()))
                                    emoticons_result.Add(newdto);
                            }
                        }

                        foreach (var itm in emoticons_result)
                            finalRes.Add(itm.Item.Id);

                        cos.Emoticons = emoticons_result;
                        if (emoticons_result.Count < 0)
                            cos.Emoticons = AllEmoticons;
                        break;

                    case "phrases":
                        var phrases = Resources.FindObjectsOfTypeAll<CosmeticsPhrasesSO>().FirstOrDefault().Phrases.Values;
                        Il2CppSystem.Collections.Generic.List<PhraseDto> phrases_targetList = AllPhrases;
                        if (!allCosmetics)
                            phrases_targetList = UserPhrases;

                        var phrases_result = new Il2CppSystem.Collections.Generic.List<PhraseDto>();

                        foreach (var col in phrases_targetList)
                            ids.Add(col.Item.ContentId);

                        foreach (var name in phrases)
                        {
                            if (term != string.Empty && name.Name != null && (name.Name.Text.ToUpper().Contains(term) || name.Id.ToUpper().Contains(term)) && ids.Contains(name.Id.ToLower()))
                            {
                                foundIds.Add(name.Id);
                                if (reqType == RequestType.List)
                                    listedRes.Add(name);
                            }
                        }

                        foreach (PhraseDto item in phrases_targetList)
                        {
                            var a = item.Item.Id.Split('.')[1].ToLower();
                            if ((item.IsFavourite || FavList.Contains(item.Item.Id)) && foundIds.Contains(a))
                            {
                                phrases_result.Add(item);
                                fav_ids.Add(a);
                            }
                        }

                        foreach (CustomiserPhrases nickname in phrases)
                        {
                            if (foundIds.Contains(nickname.Id.ToLower()))
                            {
                                var newdto = ItemDtoToPhraseDto(CMSDefinitionToItemDto(nickname));
                                if (!fav_ids.Contains(newdto.Item.Id.Split('.')[1].ToLower()))
                                    phrases_result.Add(newdto);
                            }
                        }

                        foreach (var itm in phrases_result)
                            finalRes.Add(itm.Item.Id);

                        cos.Phrases = phrases_result;
                        if (phrases_result.Count < 0)
                            cos.Phrases = AllPhrases;
                        break;
                }

                Refresh();
            }
            catch (Exception ex)
            {
                FGTLog(LogLevel.Error, GetType(), ex);
            }

        }

        public void GrantAllCosmetics()
        {
            var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;

            if (cos != null && Loaded)
            {
                cos.ColourSchemes = AllColors;
                cos.Patterns = AllPatterns;
                cos.Faceplates = AllFaceplates;
                cos.UpperCostumePieces = AllUppers;
                cos.LowerCostumePieces = AllLowers;
                cos.Nameplates = AllNameplates;
                cos.Emotes = AllEmotes;
                cos.Punchlines = AllPunchlines;
                cos.Emoticons = AllEmoticons;
                cos.Phrases = AllPhrases;
                cos.Nicknames = AllNicknames;

                Refresh();
            }
        }

        public void RemoveAllCosmetics()
        {
            var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;

            if (cos != null && Loaded)
            {
                cos.ColourSchemes = UserColors;
                cos.Patterns = UserPatterns;
                cos.Faceplates = UserFaceplates;
                cos.UpperCostumePieces = UserUppers;
                cos.LowerCostumePieces = UserLowers;
                cos.Nameplates = UserNameplates;
                cos.Emotes = UserEmotes;
                cos.Punchlines = UserPunchlines;
                cos.Emoticons = UserEmoticons;
                cos.Phrases = UserPhrases;
                cos.Nicknames = UserNicknames;

                Refresh();
            }
        }

        public void Refresh()
        {
            var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;

            var hasColors = cos.ColourSchemes.Count > 0;
            ColorSect._gridOptionSelection._contentRect.gameObject.SetActive(hasColors);
            if (hasColors) ColorSect.RefreshSectionData();

            var hasPatterns = cos.Patterns.Count > 0;
            PatternsSect._gridOptionSelection._contentRect.gameObject.SetActive(hasPatterns);
            if (hasPatterns) PatternsSect.RefreshSectionData();

            var hasEmotes = cos.Emotes.Count > 0;
            EmotesSect._gridOptionSelection._contentRect.gameObject.SetActive(hasEmotes);
            if (hasEmotes) EmotesSect.RefreshSectionData();

            var hasFaceplates = cos.Faceplates.Count > 0;
            FaceSect._gridOptionSelection._contentRect.gameObject.SetActive(hasFaceplates);
            if (hasFaceplates) FaceSect.RefreshSectionData();

            var hasUppers = cos.UpperCostumePieces.Count > 0;
            UpperCostumeSect._gridOptionSelection._contentRect.gameObject.SetActive(hasUppers);
            if (hasUppers) UpperCostumeSect.RefreshSectionData();

            var hasLowers = cos.LowerCostumePieces.Count > 0;
            LowerCostumeSect._gridOptionSelection._contentRect.gameObject.SetActive(hasLowers);
            if (hasLowers) LowerCostumeSect.RefreshSectionData();

            var hasNameplates = cos.Nameplates.Count > 0;
            NameplateSect._gridOptionSelection._contentRect.gameObject.SetActive(hasNameplates);
            if (hasNameplates) NameplateSect.RefreshSectionData();

            var hasPunchlines = cos.Punchlines.Count > 0;
            VictorySect._gridOptionSelection._contentRect.gameObject.SetActive(hasPunchlines);
            if (hasPunchlines) VictorySect.RefreshSectionData();

            var hasNicknames = cos.Nicknames.Count > 0;
            NicknameSect._gridOptionSelection._contentRect.gameObject.SetActive(hasNicknames);
            if (hasNicknames) NicknameSect.RefreshSectionData();

            var hasEmoticons = cos.Emoticons.Count > 0;
            EmoticonsSect._gridOptionSelection._contentRect.gameObject.SetActive(hasEmoticons);
            if (hasEmoticons) EmoticonsSect.RefreshSectionData();

            var hasPhrases = cos.Phrases.Count > 0;
            PhrasesSect._gridOptionSelection._contentRect.gameObject.SetActive(hasPhrases);
            if (hasPhrases) PhrasesSect.RefreshSectionData();

        }

        static ItemDto CMSDefinitionToItemDto(CMSItemDefinition itemDefinition)
        {
            ItemDto itemDto = new()
            {
                ContentId = itemDefinition.Id,
                Id = itemDefinition.FullItemId,
                ContentType = itemDefinition.GroupId,
                Quantity = 1
            };
            return itemDto;
        }

        static ColourSchemeDto ItemDtoToColourSchemeDto(ItemDto itemDto, bool isFav = false)
        {
            ColourSchemeDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = isFav
            };
            return cosmeticDto;
        }

        static PatternDto ItemDtoToPatternDto(ItemDto itemDto)
        {
            PatternDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = false
            };
            return cosmeticDto;
        }

        static FaceplateDto ItemDtoToFaceplateDto(ItemDto itemDto)
        {
            FaceplateDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = false
            };
            return cosmeticDto;
        }

        static LowerCostumePieceDto ItemDtoToCostumeLowerDto(ItemDto itemDto)
        {
            LowerCostumePieceDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = false
            };
            return cosmeticDto;
        }

        static UpperCostumePieceDto ItemDtoToCostumeUpperDto(ItemDto itemDto)
        {
            UpperCostumePieceDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = false
            };
            return cosmeticDto;
        }

        static EmoteDto ItemDtoToEmoteDto(ItemDto itemDto)
        {
            EmoteDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = false
            };
            return cosmeticDto;
        }

        static NameplateDto ItemDtoToNameplateDto(ItemDto itemDto)
        {
            NameplateDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = false
            };
            return cosmeticDto;
        }

        static PunchlineDto ItemDtoToVictoryDto(ItemDto itemDto)
        {
            PunchlineDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = false
            };
            return cosmeticDto;
        }

        static NicknameDto ItemDtoToNicknameDto(ItemDto itemDto)
        {
            NicknameDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = false
            };
            return cosmeticDto;
        }

        static EmoticonDto ItemDtoToEmoticonDto(ItemDto itemDto)
        {
            EmoticonDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = false
            };

            return cosmeticDto;
        }

        static PhraseDto ItemDtoToPhraseDto(ItemDto itemDto)
        {
            PhraseDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = false
            };

            return cosmeticDto;
        }

        static void DoSearch<TDto, TResource>(string term, RequestType reqType, bool allCosmetics, Il2CppSystem.Collections.Generic.List<TDto> allList, Il2CppSystem.Collections.Generic.List<TDto> uList, TResource[] resources, Func<TDto, ItemDto> getItmDto, Func<TResource, TResource> getItm, Func<ItemDto, TDto> pushDto, Action<Il2CppSystem.Collections.Generic.List<TDto>> setRes, ref HashSet<string> fRes, ref List<object> listResult) where TDto : Il2CppSystem.Object
        {
            HashSet<string> ids = [];
            HashSet<string> fav_ids = [];
            HashSet<string> foundIds = [];
            var r = new Il2CppSystem.Collections.Generic.List<TDto>();
            var targetList = allCosmetics ? allList : uList;

            foreach (var targ in targetList)
                ids.Add(getItmDto(targ).ContentId);

            foreach (var res in resources)
            {
                var item = getItm(res) as ItemDefinitionSO;
                var potentialCostume = item as CostumeOption;

                var good = !string.IsNullOrEmpty(term) && item.DisplayName != null && (item.DisplayName.ToUpper().Contains(term) || item.ItemId.ToUpper().Contains(term)) && ids.Contains(item._itemId.ToLower());

                if (!good)
                    continue;

                if (potentialCostume == null)
                {
                    if (good)
                    {
                        foundIds.Add(item.ItemId);
                        if (reqType == RequestType.List)
                            listResult.Add(res);
                    }
                }
                else
                {
                    switch (potentialCostume.CostumeType)
                    {
                        case CostumeType.Top:
                            if (good && typeof(TDto) == typeof(UpperCostumePieceDto))
                            {
                                foundIds.Add(item.ItemId);
                                if (reqType == RequestType.List)
                                    listResult.Add(res);
                            }
                            break;
                        case CostumeType.Bottom:
                            if (good && typeof(TDto) == typeof(LowerCostumePieceDto))
                            {
                                foundIds.Add(item.ItemId);
                                if (reqType == RequestType.List)
                                    listResult.Add(res);
                            }
                            break;
                    }
                }
            }

            foreach (var item in targetList)
            {
                var a = getItmDto(item).Id.Split('.')[1].ToLower();
                dynamic dItm = item;

                if ((dItm.IsFavourite || FavList.Contains(getItmDto(item).Id)) && foundIds.Contains(a))
                {
                    r.Add(item);
                    fav_ids.Add(a);
                }
            }

            foreach (var res in resources)
            {
                var item = getItm(res) as ItemDefinitionSO;

                if (item.CMSData != null && foundIds.Contains(item.ItemId.ToLower()))
                {
                    dynamic newdto = pushDto(CMSDefinitionToItemDto(item.CMSData));
                    if (!fav_ids.Contains(newdto.Item.Id.Split('.')[1].ToLower()))
                        r.Add(newdto);
                }
            }

            setRes(r);

            if (r.Count < 0)
                setRes(allList);

            foreach (var dto in r)
                fRes.Add(getItmDto(dto).Id);
        }

        static List<TDto> PushCosmList<TDefinition, TDto>(Il2CppSystem.Collections.Generic.List<TDto> uList, Func<TDefinition, CMSItemDefinition> getCMSItm, Func<ItemDto, TDto> getItemDto, Func<TDto, ItemDto> getItem, HashSet<string> favIds, Func<TDto, bool> favGetter, Action<TDto, bool> favSetter) where TDefinition : UnityEngine.Object where TDto : Il2CppSystem.Object
        {
            var blank = Resources.FindObjectsOfTypeAll<TDefinition>()
                .Select(def => getCMSItm(def))
                .Where(cms => cms != null)
                .Select(cms => getItemDto(CMSDefinitionToItemDto(cms)))
                .Distinct()
                .ToList();

            foreach (var item in uList)
            {
                if (favGetter(item))
                    favIds.Add(getItem(item).Id);
            }

            return
            [
                .. blank
                                .Where(item => favGetter(item) || FavList.Contains(getItem(item).Id) || favIds.Contains(getItem(item).Id))
                                .Select(item =>
                                {
                                    favSetter(item, true);
                                    favIds.Add(getItem(item).Id);
                                    return item;
                                })
,
                .. blank.Where(item => !favIds.Contains(getItem(item).Id)),
            ];
        }

        static void BuildCosmList<TDto, TSO, TSource>(Il2CppSystem.Collections.Generic.List<TDto> uList, Il2CppSystem.Collections.Generic.List<TDto> targetList, Func<TDto, ItemDto> getItm, Func<TDto, bool> favGetter, Action<TDto, bool> favSetter, Func<TSO> getScriptableObject, Func<TSO, IEnumerable<TSource>> getSrc, Func<TSource, CMSItemDefinition> getCMSDef, Func<ItemDto, TDto> convertDto, HashSet<string> favIds, HashSet<string> globalFavList) where TDto : Il2CppSystem.Object where TSO : UnityEngine.ScriptableObject
        {
            var blank = new List<TDto>();

            foreach (var item in uList)
            {
                if (favGetter(item))
                    favIds.Add(getItm(item).Id);
            }

            var ogSO = getScriptableObject();
            if (ogSO == null)
                return;

            foreach (var src in getSrc(ogSO))
            {
                var def = getCMSDef(src);
                if (def != null)
                {
                    var newDto = convertDto(CMSDefinitionToItemDto(def));
                    if (!blank.Contains(newDto))
                        blank.Add(newDto);
                }
            }

            foreach (var item in blank)
            {
                if (favGetter(item) || globalFavList.Contains(getItm(item).Id) || favIds.Contains(getItm(item).Id))
                {
                    favSetter(item, true);
                    favIds.Add(getItm(item).Id);
                    targetList.Add(item);
                }
            }

            foreach (var item in blank)
            {
                if (!favIds.Contains(getItm(item).Id))
                    targetList.Add(item);
            }
        }

        internal static void ResolveFavIds(CustomiserMenuViewModel screen)
        {
            foreach (var id in screen._favouriteIdsAdded)
            {
                if (!FavList.Contains(id))
                    FavList.Add(id);
            }

            foreach (var id in screen._favouriteIdsRemoved)
            {
                if (FavList.Contains(id))
                    FavList.Remove(id);
            }
        }

        public override void DrawGUI()
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
