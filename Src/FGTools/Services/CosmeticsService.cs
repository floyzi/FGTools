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
using Il2CppSystem.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using UniverseLib.UI;
using static FG.Benchmarking.Boot;
using static FG.Common.Messages.GameMessageClientRequestGeneric;
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
        internal static bool _searchActive = false;
        internal string CurrentSection;
        internal string PreviousSection;
        internal string InputString;

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
            ColorSect.gameObject.AddComponent<CanvasGroup>();

            PatternsSect = Resources.FindObjectsOfTypeAll<CustomiserPatternsSection>().FirstOrDefault();
            PatternsSect.gameObject.AddComponent<CanvasGroup>();

            FaceSect = Resources.FindObjectsOfTypeAll<CustomiserFaceplateSection>().FirstOrDefault();
            FaceSect.gameObject.AddComponent<CanvasGroup>();

            UpperCostumeSect = Resources.FindObjectsOfTypeAll<CustomiserUpperCostumeSection>().FirstOrDefault();
            UpperCostumeSect.gameObject.AddComponent<CanvasGroup>();

            LowerCostumeSect = Resources.FindObjectsOfTypeAll<CustomiserLowerCostumeSection>().FirstOrDefault();
            LowerCostumeSect.gameObject.AddComponent<CanvasGroup>();

            NameplateSect = Resources.FindObjectsOfTypeAll<CustomiserNameplateSection>().FirstOrDefault();
            NameplateSect.gameObject.AddComponent<CanvasGroup>();

            VictorySect = Resources.FindObjectsOfTypeAll<CustomiserVictorySection>().FirstOrDefault();
            VictorySect.gameObject.AddComponent<CanvasGroup>();

            EmotesSect = Resources.FindObjectsOfTypeAll<CustomiserEmotesSection>().FirstOrDefault();
            EmotesSect.gameObject.AddComponent<CanvasGroup>();

            NicknameSect = Resources.FindObjectsOfTypeAll<CustomiserNicknameSection>().FirstOrDefault();
            NicknameSect.gameObject.AddComponent<CanvasGroup>();

            EmoticonsSect = Resources.FindObjectsOfTypeAll<CustomiserEmoticonsSection>().FirstOrDefault();
            EmoticonsSect.gameObject.AddComponent<CanvasGroup>();

            PhrasesSect = Resources.FindObjectsOfTypeAll<CustomiserPhrasesSection>().FirstOrDefault();
            PhrasesSect.gameObject.AddComponent<CanvasGroup>();

            OutfitMenuViewModel = Resources.FindObjectsOfTypeAll<OutfitMenuViewModel>().FirstOrDefault();
            OutfitMenuViewModel.gameObject.AddComponent<CanvasGroup>();

            InterfaceMenuViewModel = Resources.FindObjectsOfTypeAll<InterfaceMenuViewModel>().FirstOrDefault();
            InterfaceMenuViewModel.gameObject.AddComponent<CanvasGroup>();

            TheatricsMenuViewModel = Resources.FindObjectsOfTypeAll<TheatricsMenuViewModel>().FirstOrDefault();
            TheatricsMenuViewModel.gameObject.AddComponent<CanvasGroup>();

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
                placeholder.text = LocalizedStr("gui_cosmetics_search");
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

        internal string GetState()
        {
            var sect = GetSection();
            var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;

            return sect switch
            {
                "colour" => LocalizeState(cos.ColourSchemes.Count),
                "pattern" => LocalizeState(cos.Patterns.Count),
                "face" => LocalizeState(cos.Faceplates.Count),
                "upper" => LocalizeState(cos.UpperCostumePieces.Count),
                "lower" => LocalizeState(cos.LowerCostumePieces.Count),
                "emotes" => LocalizeState(cos.Emotes.Count),
                "victory" => LocalizeState(cos.Punchlines.Count),
                "banner" => LocalizeState(cos.Nameplates.Count),
                "nickname" => LocalizeState(cos.Nicknames.Count),
                "emoticons" => LocalizeState(cos.Emoticons.Count),
                "phrases" => LocalizeState(cos.Phrases.Count),
                _ => "Unsupported!!1 " + sect,
            };
        }

        string LocalizeState(int count) => !string.IsNullOrEmpty(InputString) ? LocalizedStr("gui_cosmetics_found", [count]) : LocalizedStr("gui_cosmetics_total", [count]);

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
            string sect = "";

            if (OutfitMenuViewModel != null && OutfitMenuViewModel.gameObject.activeSelf)
                sect = OutfitMenuViewModel.CurrentSectionText;
            else if (TheatricsMenuViewModel != null && TheatricsMenuViewModel.gameObject.activeSelf)
                sect = TheatricsMenuViewModel.CurrentSectionText;
            else if (InterfaceMenuViewModel != null && InterfaceMenuViewModel.gameObject.activeSelf)
                sect = InterfaceMenuViewModel.CurrentSectionText;

            return sect;
        }


        internal void ResumeSearch()
        {
            Refresh();
            if (string.IsNullOrEmpty(_recentQuery)) return;
            Search(_recentQuery, GetSection());
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
        public void Search(string req, string type)
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

            HashSet<string> ids = [];
            HashSet<string> fav_ids = [];
            HashSet<string> foundIds = [];

            var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;
 
            try
            {
                switch (type)
                {
                    case "colour":
                        DoSearch(req, AllCosmetics.Value ? AllColors : UserColors, CommonConfig.GetAssetsWithGroup("costumes_colour_schemes"), dto => dto.Item, targ => targ, itemDto => ItemDtoToColourSchemeDto(itemDto), list => cos.ColourSchemes = list);
                        break;
                    case "pattern":
                        DoSearch(req, AllCosmetics.Value ? AllPatterns : UserPatterns, CommonConfig.GetAssetsWithGroup("costumes_patterns"), dto => dto.Item, targ => targ, itemDto => ItemDtoToPatternDto(itemDto), list => cos.Patterns = list);
                        break;
                    case "face":
                        DoSearch(req, AllCosmetics.Value ? AllFaceplates : UserFaceplates, CommonConfig.GetAssetsWithGroup("costumes_faceplates"), dto => dto.Item, targ => targ, itemDto => ItemDtoToFaceplateDto(itemDto), list => cos.Faceplates = list);
                        break;
                    case "upper":
                        DoSearch(req, AllCosmetics.Value ? AllUppers : UserUppers, CommonConfig.GetAssetsWithGroup("costumes_upper"), dto => dto.Item, targ => targ, itemDto => ItemDtoToCostumeUpperDto(itemDto), list => cos.UpperCostumePieces = list);
                        break;
                    case "lower":
                        DoSearch(req, AllCosmetics.Value ? AllLowers : UserLowers, CommonConfig.GetAssetsWithGroup("costumes_lower"), dto => dto.Item, targ => targ, itemDto => ItemDtoToCostumeLowerDto(itemDto), res => cos.LowerCostumePieces = res);
                        break;
                    case "emotes":
                        DoSearch(req, AllCosmetics.Value ? AllEmotes : UserEmotes, CommonConfig.GetAssetsWithGroup("cosmetics_emotes"), dto => dto.Item, targ => targ, itemDto => ItemDtoToEmoteDto(itemDto), res => cos.Emotes = res);
                        break;
                    case "victory":
                        DoSearch(req, AllCosmetics.Value ? AllPunchlines : UserPunchlines, CommonConfig.GetAssetsWithGroup("cosmetics_punchlines"), dto => dto.Item, targ => targ, itemDto => ItemDtoToVictoryDto(itemDto), res => cos.Punchlines = res);
                        break;
                    case "banner":
                        DoSearch(req, AllCosmetics.Value ? AllNameplates : UserNameplates, CommonConfig.GetAssetsWithGroup("cosmetics_nameplates"), dto => dto.Item, targ => targ, itemDto => ItemDtoToNameplateDto(itemDto), res => cos.Nameplates = res);
                        break;
                    case "nickname":
                        DoSearch(req, AllCosmetics.Value ? AllNicknames : UserNicknames, CommonConfig.GetAssetsWithGroup("cosmetics_nicknames"), x => x.DisplayName, x => x.ItemId, x => ItemDtoToNicknameDto(CMSDefinitionToItemDto(x)), x => x.Item.ContentId, x => x.Item.Id, x => x.IsFavourite, res => cos.Nicknames = res);
                        break;
                    case "emoticons":
                        DoSearch(req, AllCosmetics.Value ? AllEmoticons : UserEmoticons, CommonConfig.GetAssetsWithGroup("cosmetics_emoticons"), x => x.DisplayName, x => x.ItemId, x => ItemDtoToEmoticonDto(CMSDefinitionToItemDto(x)), x => x.Item.ContentId, x => x.Item.Id, x => x.IsFavourite, res => cos.Emoticons = res);
                        break;
                    case "phrases":
                        DoSearch(req, AllCosmetics.Value ? AllPhrases : UserPhrases, CommonConfig.GetAssetsWithGroup("cosmetics_phrases"), x => x.DisplayName, x => x.ItemId, x => ItemDtoToPhraseDto(CMSDefinitionToItemDto(x)), x => x.Item.ContentId, x => x.Item.Id, x => x.IsFavourite, res => cos.Phrases = res);
                        break;
                }

                Refresh();
            }
            catch (Exception ex)
            {
                FGTLog(LogLevel.Error, GetType(), ex);
            }
        }

        static void DoSearch<TDto>(string req, Il2CppSystem.Collections.Generic.List<TDto> lookupList, Il2CppSystem.Collections.Generic.List<IItemDefinition> resList, Func<IItemDefinition, string> getName, Func<IItemDefinition, string> getId, Func<IItemDefinition, TDto> convert, Func<TDto, string> getContentId, Func<TDto, string> getItmId, Func<TDto, bool> getFav, Action<Il2CppSystem.Collections.Generic.List<TDto>> setRes)
        {
            var ids = new HashSet<string>();
            var foundIds = new HashSet<string>();
            var fav_ids = new HashSet<string>();

            foreach (var col in lookupList)
                ids.Add(getContentId(col));

            foreach (var name in resList)
                if (IsValidTerm(req, getName(name), getId(name), ids))
                    foundIds.Add(getId(name));

            var r = new Il2CppSystem.Collections.Generic.List<TDto>();

            foreach (var item in lookupList)
            {
                var a = getItmId(item).Split('.')[1].ToLower();

                if ((getFav(item) || FavList.Contains(getItmId(item))) && foundIds.Contains(a))
                {
                    r.Add(item);
                    fav_ids.Add(a);
                }
            }

            foreach (var item in resList)
            {
                if (foundIds.Contains(getId(item).ToLower()))
                {
                    var newdto = convert(item);

                    if (!fav_ids.Contains(getItmId(newdto).Split('.')[1].ToLower()))
                    {
                        r.Add(newdto);
                        fav_ids.Add(getItmId(newdto).Split('.')[1].ToLower());
                    }
                }
            }

            setRes(r);
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

        void ResolveSection<TItemDefinition, TItemDto>(CustomiserSectionBase<TItemDefinition, TItemDto> sect, bool valid)
        {
            var canvas = sect.gameObject.GetComponent<CanvasGroup>();
            sect._customiserMenu.transform.GetChild(2).gameObject.SetActive(valid);
            sect._customiserMenu.CurrentSectionText = GetSection();

            if (valid)
            {
                canvas.alpha = 1;
                canvas.blocksRaycasts = true;
                canvas.interactable = true;

                sect.RefreshSectionData();
                return;
            }
            else
            {
                canvas.alpha = 0;
                canvas.blocksRaycasts = false;
                canvas.interactable = false;
            }
        }

        public void Refresh()
        {
            var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;
            var sect = GetSection();

            var sections = new Dictionary<string, Action>
            {
                ["colour"] = () => ResolveSection(ColorSect, cos.ColourSchemes.Count > 0),
                ["pattern"] = () => ResolveSection(PatternsSect, cos.Patterns.Count > 0),
                ["face"] = () => ResolveSection(FaceSect, cos.Faceplates.Count > 0),
                ["upper"] = () => ResolveSection(UpperCostumeSect, cos.UpperCostumePieces.Count > 0),
                ["lower"] = () => ResolveSection(LowerCostumeSect, cos.LowerCostumePieces.Count > 0),
                ["emotes"] = () => ResolveSection(EmotesSect, cos.Emotes.Count > 0),
                ["victory"] = () => ResolveSection(VictorySect, cos.Punchlines.Count > 0),
                ["banner"] = () => ResolveSection(NameplateSect, cos.Nameplates.Count > 0),
                ["nickname"] = () => ResolveSection(NicknameSect, cos.Nicknames.Count > 0),
                ["emoticons"] = () => ResolveSection(EmoticonsSect, cos.Emoticons.Count > 0),
                ["phrases"] = () => ResolveSection(PhrasesSect, cos.Phrases.Count > 0)
            };

            if (string.IsNullOrEmpty(sect))
            {
                foreach (var reslv1 in sections.Values)
                    reslv1();

                return;
            }
            
            if (sections.TryGetValue(sect, out var reslv2))
                reslv2();
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

        static ItemDto CMSDefinitionToItemDto(IItemDefinition itemDefinition)
        {
            ItemDto itemDto = new()
            {
                ContentId = itemDefinition.ItemId,
                Id = itemDefinition.FullItemId,
                ContentType = itemDefinition.CMSGroupID,
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

        static bool IsValidTerm(string term, string displayName, string itemId, HashSet<string> ids)
        {
            return !string.IsNullOrEmpty(term) &&
                !string.IsNullOrEmpty(displayName) &&
                (displayName.Contains(term, Il2CppSystem.StringComparison.CurrentCultureIgnoreCase) ||
                (AllowSearchById.Value && itemId.Contains(term, Il2CppSystem.StringComparison.CurrentCultureIgnoreCase))) &&
                ids.Contains(itemId.ToLower());
        }

        static void DoSearch<TDto>(string term, Il2CppSystem.Collections.Generic.List<TDto> lookupList, Il2CppSystem.Collections.Generic.List<IItemDefinition> resList, Func<TDto, ItemDto> getItmDto, Func<IItemDefinition, IItemDefinition> getItm, Func<ItemDto, TDto> pushDto, Action<Il2CppSystem.Collections.Generic.List<TDto>> setRes) where TDto : Il2CppSystem.Object
        {
            HashSet<string> ids = [];
            HashSet<string> fav_ids = [];
            HashSet<string> foundIds = [];

            var r = new Il2CppSystem.Collections.Generic.List<TDto>();

            foreach (var targ in lookupList)
                ids.Add(getItmDto(targ).ContentId);

            foreach (var res in resList)
            {
                var item = getItm(res);

                var good = IsValidTerm(term, item.DisplayName, item.ItemId, ids);

                if (!good) continue;

                foundIds.Add(item.ItemId);
            }

            foreach (var item in lookupList)
            {
                var a = getItmDto(item).Id.Split('.')[1].ToLower();
                dynamic dItm = item;

                if ((dItm.IsFavourite || FavList.Contains(getItmDto(item).Id)) && foundIds.Contains(a))
                {
                    r.Add(item);
                    fav_ids.Add(a);
                }
            }

            foreach (var res in resList)
            {
                var item = getItm(res);

                if (item.CMSData != null && foundIds.Contains(item.ItemId.ToLower()))
                {
                    dynamic newdto = pushDto(CMSDefinitionToItemDto(item.CMSData));
                    if (!fav_ids.Contains(newdto.Item.Id.Split('.')[1].ToLower()))
                        r.Add(newdto);
                }
            }

            setRes(r);
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
                .. blank.Where(item => favGetter(item) || FavList.Contains(getItem(item).Id) || favIds.Contains(getItem(item).Id))
                .Select(item =>
                {
                    favSetter(item, true);
                    favIds.Add(getItem(item).Id);
                    return item;
                }), .. blank.Where(item => !favIds.Contains(getItem(item).Id)),
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
