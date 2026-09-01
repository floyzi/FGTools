using BepInEx.Logging;
using Catapult.Modules.Items.Protocol.Dtos;
using Events;
using FallGuys.Player.Protocol.Client.Cosmetics;
using FG.Common;
using FG.Common.CMS;
using FGClient.CatapultServices;
using FGClient.Customiser;
using FGTools.Internal.Behaviours;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using static FGTools.Config.Config;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
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

                UserColors = cos.ColourSchemes;
                foreach (var res in PushCosmList("costumes_colour_schemes", UserColors, def => def.CMSData, ItemDtoToColourSchemeDto, dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllColors.Add(res);

                UserPatterns = cos.Patterns;
                foreach (var res in PushCosmList("costumes_patterns", UserPatterns, def => def.CMSData, ItemDtoToPatternDto, dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllPatterns.Add(res);

                UserFaceplates = cos.Faceplates;
                foreach (var res in PushCosmList("costumes_faceplates", UserFaceplates, def => def.CMSData, ItemDtoToFaceplateDto, dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllFaceplates.Add(res);

                UserUppers = cos.UpperCostumePieces;
                foreach (var res in PushCosmList("costumes_upper", UserUppers, def => def.CMSData, ItemDtoToCostumeUpperDto, dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllUppers.Add(res);

                UserLowers = cos.LowerCostumePieces;
                foreach (var res in PushCosmList("costumes_lower", UserLowers, def => def.CMSData, ItemDtoToCostumeLowerDto, dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllLowers.Add(res);

                UserNameplates = cos.Nameplates;
                foreach (var res in PushCosmList("cosmetics_nameplates", UserNameplates, def => def.CMSData, ItemDtoToNameplateDto, dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllNameplates.Add(res);

                UserEmotes = cos.Emotes;
                foreach (var res in PushCosmList("cosmetics_emotes", UserEmotes, def => def.CMSData, ItemDtoToEmoteDto, dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllEmotes.Add(res);

                UserPunchlines = cos.Punchlines;
                foreach (var res in PushCosmList("cosmetics_punchlines", UserPunchlines, def => def.CMSData, ItemDtoToVictoryDto, dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllPunchlines.Add(res);

                UserNicknames = cos.Nicknames;
                foreach (var res in PushCosmList("cosmetics_nicknames", UserNicknames, def => def.CMSData, ItemDtoToNicknameDto, dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllNicknames.Add(res);

                UserEmoticons = cos.Emoticons;
                foreach (var res in PushCosmList("cosmetics_emoticons", UserEmoticons, def => def.CMSData, ItemDtoToEmoticonDto, dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllEmoticons.Add(res);

                UserPhrases = cos.Phrases;
                foreach (var res in PushCosmList("cosmetics_phrases", UserPhrases, def => def.CMSData, ItemDtoToPhraseDto, dto => dto.Item, favIds, dto => dto.IsFavourite, (dto, val) => dto.IsFavourite = val))
                    AllPhrases.Add(res);

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
                        DoSearch(req, AllCosmetics.Value ? AllColors : UserColors, CommonConfig.GetAssetsWithGroup("costumes_colour_schemes"), dto => dto.Item, ItemDtoToColourSchemeDto, list => cos.ColourSchemes = list);
                        break;
                    case "pattern":
                        DoSearch(req, AllCosmetics.Value ? AllPatterns : UserPatterns, CommonConfig.GetAssetsWithGroup("costumes_patterns"), dto => dto.Item, ItemDtoToPatternDto, list => cos.Patterns = list);
                        break;
                    case "face":
                        DoSearch(req, AllCosmetics.Value ? AllFaceplates : UserFaceplates, CommonConfig.GetAssetsWithGroup("costumes_faceplates"), dto => dto.Item, ItemDtoToFaceplateDto, list => cos.Faceplates = list);
                        break;
                    case "upper":
                        DoSearch(req, AllCosmetics.Value ? AllUppers : UserUppers, CommonConfig.GetAssetsWithGroup("costumes_upper"), dto => dto.Item, ItemDtoToCostumeUpperDto, list => cos.UpperCostumePieces = list);
                        break;
                    case "lower":
                        DoSearch(req, AllCosmetics.Value ? AllLowers : UserLowers, CommonConfig.GetAssetsWithGroup("costumes_lower"), dto => dto.Item, ItemDtoToCostumeLowerDto, res => cos.LowerCostumePieces = res);
                        break;
                    case "emotes":
                        DoSearch(req, AllCosmetics.Value ? AllEmotes : UserEmotes, CommonConfig.GetAssetsWithGroup("cosmetics_emotes"), dto => dto.Item, ItemDtoToEmoteDto, res => cos.Emotes = res);
                        break;
                    case "victory":
                        DoSearch(req, AllCosmetics.Value ? AllPunchlines : UserPunchlines, CommonConfig.GetAssetsWithGroup("cosmetics_punchlines"), dto => dto.Item, ItemDtoToVictoryDto, res => cos.Punchlines = res);
                        break;
                    case "banner":
                        DoSearch(req, AllCosmetics.Value ? AllNameplates : UserNameplates, CommonConfig.GetAssetsWithGroup("cosmetics_nameplates"), dto => dto.Item, ItemDtoToNameplateDto, res => cos.Nameplates = res);
                        break;
                    case "nickname":
                        DoSearch(req, AllCosmetics.Value ? AllNicknames : UserNicknames, CommonConfig.GetAssetsWithGroup("cosmetics_nicknames"), dto => dto.Item, ItemDtoToNicknameDto, res => cos.Nicknames = res);
                        break;
                    case "emoticons":
                        DoSearch(req, AllCosmetics.Value ? AllEmoticons : UserEmoticons, CommonConfig.GetAssetsWithGroup("cosmetics_emoticons"), dto => dto.Item, ItemDtoToEmoticonDto, res => cos.Emoticons = res);
                        break;
                    case "phrases":
                        DoSearch(req, AllCosmetics.Value ? AllPhrases : UserPhrases, CommonConfig.GetAssetsWithGroup("cosmetics_phrases"), dto => dto.Item, ItemDtoToPhraseDto, res => cos.Phrases = res);
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


        static ColourSchemeDto ItemDtoToColourSchemeDto(ItemDto itemDto)
        {
            ColourSchemeDto cosmeticDto = new()
            {
                EarnedAt = Il2CppSystem.DateTime.Now,
                Item = itemDto,
                IsFavourite = false
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

        static void DoSearch<TDto>(string term, Il2CppSystem.Collections.Generic.List<TDto> lookupList, Il2CppSystem.Collections.Generic.List<IItemDefinition> resList, Func<TDto, ItemDto> getItmDto, Func<ItemDto, TDto> pushDto, Action<Il2CppSystem.Collections.Generic.List<TDto>> setRes) where TDto : Il2CppSystem.Object
        {
            HashSet<string> ids = [];
            HashSet<string> fav_ids = [];
            HashSet<string> foundIds = [];

            var r = new Il2CppSystem.Collections.Generic.List<TDto>();

            foreach (var targ in lookupList)
                ids.Add(getItmDto(targ).ContentId);

            foreach (var res in resList)
            {
                var good = IsValidTerm(term, res.DisplayName, res.ItemId, ids);

                if (!good) continue;

                foundIds.Add(res.ItemId);
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
                if (res.CMSData != null && foundIds.Contains(res.ItemId.ToLower()))
                {
                    dynamic newdto = pushDto(CMSDefinitionToItemDto(res.CMSData));
                    if (!fav_ids.Contains(newdto.Item.Id.Split('.')[1].ToLower()))
                        r.Add(newdto);
                }
            }

            setRes(r);
        }

        static List<TDto> PushCosmList<TDto>(string group, Il2CppSystem.Collections.Generic.List<TDto> uList, Func<IItemDefinition, CMSItemDefinition> getCMSItm, Func<ItemDto, TDto> getItemDto, Func<TDto, ItemDto> getItem, HashSet<string> favIds, Func<TDto, bool> favGetter, Action<TDto, bool> favSetter) where TDto : Il2CppSystem.Object
        {
            var blank = CommonConfig.GetAssetsWithGroup(group).ToArray()
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
