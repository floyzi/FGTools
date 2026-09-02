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
using System.Reflection;
using System.Text.Json;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using static FGTools.Config.Config;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;

namespace FGTools.Services
{
    internal class CosmeticsService : FGTService
    {
        internal interface IItemCollection
        {
            internal void GrantAll();
            internal void GrantUser();
            internal void Search(string req);
            internal void ResolveSection();
            internal void RefreshSection();
            internal void StartTyping();
            internal void MakeUI();
            internal void StopTyping();
        }
        internal class ItemCollection<TCollection, TDefinition, TDto> : IItemCollection where TDto : Il2CppSystem.Object where TCollection : CustomiserSectionBase<TDefinition, TDto>
        {
            internal static HashSet<string> FavIds = [];
            internal Il2CppSystem.Collections.Generic.List<IItemDefinition> Items;
            internal ItemCollection(string group)
            {
                Items = CommonConfig.GetAssetsWithGroup(group);
                UserItems = GetList();
                SetAll();
            }

            TCollection _collection;
            CustomiserSubScreenViewModel _focusable;
            internal TCollection Collection 
            { 
                get 
                {
                    if (_collection == null)
                    {
                        _collection = Resources.FindObjectsOfTypeAll<TCollection>().FirstOrDefault();
                        _collection.gameObject.AddComponent<CanvasGroup>();
                        _collection._customiserMenu.gameObject.AddComponent<CanvasGroup>();
                        _focusable = _collection._customiserMenu.GetComponent<CustomiserSubScreenViewModel>();
                    }

                    return _collection; 
                } 
            }
            internal Il2CppSystem.Collections.Generic.List<TDto> UserItems;
            internal Il2CppSystem.Collections.Generic.List<TDto> AllItems;

            static void AssignList(Il2CppSystem.Collections.Generic.List<TDto> items)
            {
                var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;

                var props = cos.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var prop = props.FirstOrDefault(x => x.PropertyType == typeof(Il2CppSystem.Collections.Generic.List<TDto>));
                prop.SetValue(cos, items);
            }

            static Il2CppSystem.Collections.Generic.List<TDto> GetList()
            {
                var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;

                var props = cos.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var prop = props.FirstOrDefault(x => x.PropertyType == typeof(Il2CppSystem.Collections.Generic.List<TDto>));
                return prop.GetValue(cos) as Il2CppSystem.Collections.Generic.List<TDto>;
            }

            void SetAll()
            {
                var blank = Items.ToArray().Select(x => x.CMSData).Where(cms => cms != null).Select(cms => ItemDtoTo<TDto>(CMSDefinitionToItemDto(cms))).Distinct().ToList();

                foreach (var item in UserItems)
                {
                    var actualItem = item.Cast<IOwnedCosmeticDto>();
                    if (actualItem.IsFavourite)
                        FavIds.Add(actualItem.Item.Id);
                }


                var owned = blank.Select(x => (Dto: x, Owned: x.Cast<IOwnedCosmeticDto>())).ToList();
                var res = new Il2CppSystem.Collections.Generic.List<TDto>();

                foreach (var (dto, item) in owned)
                {
                    var id = item.Item.Id;

                    if (FavList.Contains(id) || FavIds.Contains(id))
                    {
                        item.IsFavourite = true;

                        if (!FavIds.Contains(id))
                            FavIds.Add(id);

                        res.Add(dto);
                    }
                }

                foreach (var (dto, item) in owned)
                {
                    if (!FavIds.Contains(item.Item.Id))
                        res.Add(item.Cast<TDto>());
                }

                AllItems = res;
            }

            void IItemCollection.Search(string req)
            {
                HashSet<string> ids = [];
                HashSet<string> fav_ids = [];
                HashSet<string> foundIds = [];

                var r = new Il2CppSystem.Collections.Generic.List<TDto>();

                var lookupList = AllCosmetics.Value ? AllItems : UserItems;

                foreach (var targ in lookupList)
                    ids.Add(targ.Cast<IOwnedCosmeticDto>().Item.ContentId);

                foreach (var res in Items)
                {
                    var good = IsValidTerm(req, res.DisplayName, res.ItemId, ids);

                    if (!good) continue;

                    foundIds.Add(res.ItemId);
                }

                foreach (var item in lookupList)
                {
                    var actualItem = item.Cast<IOwnedCosmeticDto>();
                    var a = actualItem.Item.Id.Split('.')[1].ToLower();
                    if ((actualItem.IsFavourite || FavList.Contains(actualItem.Item.Id)) && foundIds.Contains(a))
                    {
                        r.Add(item);
                        fav_ids.Add(a);
                    }
                }

                foreach (var res in Items)
                {
                    if (res.CMSData != null && foundIds.Contains(res.ItemId.ToLower()))
                    {
                        var newdto = ItemDtoTo<TDto>(CMSDefinitionToItemDto(res.CMSData));
                        var dtoItem = newdto.Cast<IOwnedCosmeticDto>();

                        if (!fav_ids.Contains(dtoItem.Item.Id.Split('.')[1].ToLower()))
                            r.Add(newdto);
                    }
                }

                AssignList(r);
            }

            void IItemCollection.GrantAll()
            {
                AssignList(AllItems);
            }

            void IItemCollection.GrantUser()
            {
                AssignList(UserItems);
            }

            void IItemCollection.ResolveSection()
            {
                var list = GetList();
                if (list == null || list.Count == 0)
                    AssignList(AllCosmetics.Value ? AllItems : UserItems);
            }

            void IItemCollection.RefreshSection()
            {
                var items = GetList();
                var valid = items != null && items.Count > 0;
                var canvas = Collection.gameObject.GetComponent<CanvasGroup>();
                Collection._customiserMenu.transform.GetChild(2).gameObject.SetActive(valid);
                Collection._customiserMenu.CurrentSectionText = Collection._customiserMenu.CurrentSectionText;

                if (valid)
                {
                    canvas.alpha = 1;
                    canvas.blocksRaycasts = true;
                    canvas.interactable = true;

                    Collection.RefreshSectionData();

                    return;
                }
                else
                {
                    canvas.alpha = 0;
                    canvas.blocksRaycasts = false;
                    canvas.interactable = false;
                }
            }

            void IItemCollection.StartTyping()
            {
                _focusable.enabled = false;
                Broadcaster.Instance.RaiseEvent(new NavPromptChanged(new Il2CppSystem.Collections.Generic.Dictionary<NavPrompt, Il2CppSystem.Action>()));
            }

            void IItemCollection.StopTyping()
            {
                _focusable.enabled = true;
                _focusable.OnGainFocus();
            }

            void IItemCollection.MakeUI()
            {
                if (Collection == null) return;

                var initTest = _focusable.transform.GetChild(0).transform;
                if (initTest.GetComponent<CosmeticSearchBar>() != null) return;

                var searchBar = GameObject.Instantiate(FGTServiceManager.Instance.GetService<CosmeticsService>()._searchPrefab, initTest);

                var rt = searchBar.gameObject.GetComponent<RectTransform>();
                rt.anchorMax = new(0.4f, 1);
                rt.anchorMin = new(0, 1);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -30);

                searchBar.AddComponent<CosmeticSearchBar>();
            }
        }

        Dictionary<string, IItemCollection> _itemCollections;
        IItemCollection _currentColection;

        internal static HashSet<string> FavList { get; set; }
        GameObject _searchPrefab;
        string _recentQuery;
        internal static bool SearchActive;
        internal string CurrentSection;
        internal string PreviousSection;
        internal string InputString;

        public override void RegisterService()
        {

        }

        void ResetFavList()
        {
            FGTLog(LogLevel.Warning, GetType(), $"Failed to recover fav list.");

            if (File.Exists(Launcher.CustomFavList))
                File.Delete(Launcher.CustomFavList);

            FavList = [];
            var stats = JsonSerializer.Serialize(FavList);
            File.WriteAllText(Launcher.CustomFavList, stats);
        }

        public void Load()
        {
            if (_itemCollections != null && _itemCollections.Count > 0)
            {
                FGTLog(LogLevel.Info, GetType(), "Already loaded, finishing.");
                MakeUI();
                return;
            }

            FGTLog(LogLevel.Info, GetType(), "Load");
        
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

                if (FavList == null) ResetFavList();

                _itemCollections = [];
                _itemCollections.Add("colour", new ItemCollection<CustomiserColourSection, ColourOption, ColourSchemeDto>("costumes_colour_schemes"));
                _itemCollections.Add("pattern", new ItemCollection<CustomiserPatternsSection, SkinPatternOption, PatternDto>("costumes_patterns"));
                _itemCollections.Add("face", new ItemCollection<CustomiserFaceplateSection, FaceplateOption, FaceplateDto>("costumes_faceplates"));
                _itemCollections.Add("upper", new ItemCollection<CustomiserUpperCostumeSection, CostumeOption, UpperCostumePieceDto>("costumes_upper"));
                _itemCollections.Add("lower", new ItemCollection<CustomiserLowerCostumeSection, CostumeOption, LowerCostumePieceDto>("costumes_lower"));
                _itemCollections.Add("banner", new ItemCollection<CustomiserNameplateSection, NameplateOption, NameplateDto>("cosmetics_nameplates"));
                _itemCollections.Add("emotes", new ItemCollection<CustomiserEmotesSection, ItemDefinitionSO, EmoteDto>("cosmetics_emotes"));
                _itemCollections.Add("victory", new ItemCollection<CustomiserVictorySection, VictoryOption, PunchlineDto>("cosmetics_punchlines"));
                _itemCollections.Add("nickname", new ItemCollection<CustomiserNicknameSection, NicknameOption, NicknameDto>("cosmetics_nicknames"));
                _itemCollections.Add("emoticons", new ItemCollection<CustomiserEmoticonsSection, ItemDefinitionSO, EmoticonDto>("cosmetics_emoticons"));
                _itemCollections.Add("phrases", new ItemCollection<CustomiserPhrasesSection, SocialOption, PhraseDto>("cosmetics_phrases"));

                MakeUI();

                if (AllCosmetics.Value)
                    GrantAllCosmetics();
                else
                    RemoveAllCosmetics();
            }
            catch (Exception e) { FGTLog(LogLevel.Error, GetType(), e); }
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
                _searchPrefab.name = "CosmeticSearch";
                GameObject.DontDestroyOnLoad(_searchPrefab);
            }

            foreach (var collection in _itemCollections)
                collection.Value.MakeUI();
        }

        internal string GetState()
        {
            var cos = CatapultServices.Instance.PlayerCosmeticsService.CosmeticsCollection;

            return CurrentSection switch
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
                _ => "Unsupported!!1 " + CurrentSection,
            };
        }

        string LocalizeState(int count) => !string.IsNullOrEmpty(InputString) ? LocalizedStr("gui_cosmetics_found", [count]) : LocalizedStr("gui_cosmetics_total", [count]);

        public override void UpdateService()
        {
            if (StateManager.FGTCurrentState == FGTStateManager.ToolsState.Menu)
            {
                //if (_searchActive)
                //    Broadcaster.Instance.RaiseEvent(new NavPromptChanged(new Il2CppSystem.Collections.Generic.Dictionary<NavPrompt, Il2CppSystem.Action>()));
            }
        }

        public void SearchStart()
        {
            SearchActive = true;
            _currentColection.StartTyping();
        }

        public void SearchEnd()
        {
            SearchActive = false;
            _currentColection.StopTyping();
        }

        internal void UpdateSection()
        {
            if (!_itemCollections.TryGetValue(CurrentSection, out _currentColection)) return;
            _currentColection.RefreshSection();
        }

        internal void ResumeSearch()
        {
            Refresh();
            if (string.IsNullOrEmpty(_recentQuery)) return;
            Search(_recentQuery, CurrentSection);
        }

        internal void ResolveSections()
        {
            foreach (var collection in _itemCollections)
                collection.Value.ResolveSection();
        }

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

            try
            {
                if (_itemCollections.TryGetValue(type, out var itemCollection))
                    itemCollection.Search(req);

                Refresh();
            }
            catch (Exception ex)
            {
                FGTLog(LogLevel.Error, GetType(), ex);
            }
        }

        public void GrantAllCosmetics()
        {
            foreach (var collection in _itemCollections)
                collection.Value.GrantAll();

            Refresh();
        }

        public void RemoveAllCosmetics()
        {
            foreach (var collection in _itemCollections)
                collection.Value.GrantUser();

            Refresh();
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(CurrentSection))
            {
                foreach (var section in _itemCollections)
                    section.Value.RefreshSection();
                return;
            }

            if (_itemCollections.TryGetValue(CurrentSection, out var collection))
                collection.RefreshSection();
        }

        static bool IsValidTerm(string term, string displayName, string itemId, HashSet<string> ids)
        {
            return !string.IsNullOrEmpty(term) &&
                !string.IsNullOrEmpty(displayName) &&
                (displayName.Contains(term, Il2CppSystem.StringComparison.CurrentCultureIgnoreCase) ||
                (AllowSearchById.Value && itemId.Contains(term, Il2CppSystem.StringComparison.CurrentCultureIgnoreCase))) &&
                ids.Contains(itemId.ToLower());
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

        static ItemDto CMSDefinitionToItemDto(CMSItemDefinition itemDefinition)
        {
            var itemDto = new ItemDto()
            {
                ContentId = itemDefinition.Id,
                Id = itemDefinition.FullItemId,
                ContentType = itemDefinition.GroupId,
                Quantity = 1
            };

            return itemDto;
        }

        static T ItemDtoTo<T>(ItemDto itemDto) where T : Il2CppSystem.Object
        {
            var dto = (T)Activator.CreateInstance(typeof(T));
            var owned = dto.TryCast<IOwnedCosmeticDto>();
            owned.Item = itemDto;
            owned.IsFavourite = false;
            owned.EarnedAt = Il2CppSystem.DateTime.Now;
            return dto;
        }
    }
}
