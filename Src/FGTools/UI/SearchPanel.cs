using FGTools.Services;
using FGTools.Services.Logic;
using System;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using UniverseLib.UI.Panels;
using UniverseLib.UI.Widgets;
using static FGTools.Services.LocalizationService;
namespace FGTools.UI
{
    public class SearchPanel : PanelBase
    {
        public SearchPanel(UIBase owner) : base(owner)
        {
            instance ??= this;
        }

        public static SearchPanel instance;
        public Text listText;

        public override string Name => "Search Cosmetics";
        public override int MinWidth => 420;
        public override int MinHeight => 235;
        public override Vector2 DefaultAnchorMin => new(0.25f, 0.25f);
        public override Vector2 DefaultAnchorMax => new(0.75f, 0.75f);
        public override Vector2 DefaultPosition => new(-Screen.width / 2f, Screen.height / 2f);
        public override bool CanDragAndResize => false;

        Text Result;
        InputFieldRef SearchBar;
        protected override void ConstructPanelContent()
        {
            TitleBar.gameObject.SetActive(true);

            Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, MinWidth);
            Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, MinHeight);

            GameObject listGroup = null;

            GameObject baseObj = UIFactory.CreateVerticalGroup(ContentRoot, "VerticalGroup", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(baseObj, minHeight: 25, flexibleHeight: 0);

            SearchBar = UIFactory.CreateInputField(baseObj, "imageNameInput", $"{LocalizedStr("inputfield_placeholder")}");
            UIFactory.SetLayoutElement(SearchBar.GameObject, minHeight: 25, flexibleHeight: 0);

            GameObject buttons = UIFactory.CreateHorizontalGroup(baseObj, "HorizontalGroup", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(buttons, minHeight: 25, flexibleHeight: 0);

            ButtonRef doSearchBtn = UIFactory.CreateButton(buttons, "doSearch", $"{LocalizedStr("gui_search")}", null);

            UIFactory.SetLayoutElement(doSearchBtn.GameObject, minHeight: 25, flexibleHeight: 0);
            ButtonRef resetSearchBtn = UIFactory.CreateButton(buttons, "resetSearchBtn", $"{LocalizedStr("gui_cosmetics_search_reset")}", null);
            resetSearchBtn.OnClick += Reset;
            UIFactory.SetLayoutElement(resetSearchBtn.GameObject, minHeight: 25, flexibleHeight: 0);

            GameObject options = UIFactory.CreateVerticalGroup(baseObj, "options", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(options, minHeight: 25, flexibleHeight: 0);

            GameObject printAsList = UIFactory.CreateToggle(options, "printAsList", out Toggle printAsListToggle, out Text printAsListToggle_t);
            printAsListToggle_t.text = LocalizedStr("gui_cosmetics_search_as_list");
            printAsListToggle.isOn = false;
            printAsListToggle.onValueChanged.AddListener(new Action<bool>(val =>
            {
                listGroup.gameObject.SetActive(val);
            }));

            listGroup = UIFactory.CreateHorizontalGroup(baseObj, "listGroup", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(listGroup, minHeight: 25, flexibleHeight: 100);

            GameObject view = UIFactory.CreateScrollView(listGroup, "list", out GameObject content, out AutoSliderScrollbar scrollBar, new(0.1f, 0.1f, 0.1f));
            UIFactory.SetLayoutElement(view, flexibleHeight: 100, minHeight: 100);
            Transform settingsList = view.GetComponent<ScrollRect>().content.transform;
            listText = UIFactory.CreateLabel(settingsList.gameObject, "listText", $"{LocalizedStr("gui_cosmetics_search_as_list_title")}", TextAnchor.LowerLeft, default, true, 14);
            listGroup.SetActive(false);

            Result = UIFactory.CreateLabel(baseObj, "result", LocalizedStr("gui_begin_search"), TextAnchor.LowerCenter);

            SearchBar.OnValueChanged += x =>
            {
                FGTServiceManager.Instance.GetService<CosmeticsService>().SearchStart();
                if (x.Length == 0)
                    FGTServiceManager.Instance.GetService<CosmeticsService>().SearchEnd(false);
            };

            doSearchBtn.OnClick += () => { FGTServiceManager.Instance.GetService<CosmeticsService>().Search(SearchBar.Text, FGTServiceManager.Instance.GetService<CosmeticsService>().GetSection(), printAsListToggle.isOn ? CosmeticsService.RequestType.List : CosmeticsService.RequestType.Locker, Config.Config.AllCosmetics.Value); };

            ChangeTitle(LocalizedStr("gui_cosmetics_search"));
        }

        internal void Reset()
        {
            SearchBar.Text = string.Empty;
            Result.text = LocalizedStr("gui_cosmetics_search");

            var a = FGTServiceManager.Instance.GetService<CosmeticsService>();

            if (Config.Config.AllCosmetics.Value)
                a.GrantAllCosmetics();
            else
                a.RemoveAllCosmetics();
        }

        internal void ChangeTitle(string title) => ContentRoot.transform.GetChild(0).gameObject.transform.GetChild(0).gameObject.GetComponent<Text>().text = $"{title}";
        internal void SetResult(string result) => Result.text = result;
    }
}
