using FGTools.Content;
using FGTools.Services;
using FGTools.States.Logic;
using FGTools.UI.Tabs.Logic;
using System;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Widgets;
using static FGTools.UI.NewGUI;
using static FGTools.Services.LocalizationService;

namespace FGTools.UI.Tabs
{
    internal class ShowLoaderTab : UITab<ShowLoaderService>
    {
        public ShowLoaderTab() : base(Tab.ShowLoader, FGTServiceManager.GetService<ShowLoaderService>())
        {
            StatePerGroup = new()
            {
                { new GroupPolicy(ObjectGroup.Editor, GroupOperation.Interactable), () => false },
                { new GroupPolicy(ObjectGroup.Menu, GroupOperation.Interactable), () => true },
                { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.Interactable), () => true },
                { new GroupPolicy(ObjectGroup.Loading, GroupOperation.Interactable), () => false }
            };
        }

        internal override void Draw(GameObject root)
        {
            ControlledObject = UIFactory.CreateVerticalGroup(root, $"Tab_{Tab}", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(ControlledObject, minHeight: 25, flexibleHeight: 0);

            NewGUI.Instance.TryDrawUI(() => FGTTargetSettings.ShowLoader, ControlledObject, new(() =>
            {
                var sL = FGTBase.FGTServiceManager.GetService<ShowLoaderService>();

                GameObject showSearchbarGroup = UIFactory.CreateHorizontalGroup(ControlledObject, "searchGroup", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                UIFactory.SetLayoutElement(showSearchbarGroup, minHeight: 30, flexibleHeight: 0);
                var showSearchBar = UIFactory.CreateInputField(showSearchbarGroup, "searchInRoundNames", $"{LocalizedStr("gui_search_show")}");

                GameObject showsDropGroup = UIFactory.CreateHorizontalGroup(ControlledObject, "showsDropGroup", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                UIFactory.SetLayoutElement(showsDropGroup, minHeight: 30, flexibleHeight: 0);
                GameObject showsDropUI = UIFactory.CreateDropdown(showsDropGroup, "showsDropUI", out Dropdown showListDrop, $"{LocalizedStr("dropdown_placeholder")}", 14, null, null);

                GameObject showLoadBtns = UIFactory.CreateHorizontalGroup(ControlledObject, "loadingBtns", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                UIFactory.SetLayoutElement(showLoadBtns, minHeight: 25, flexibleHeight: 0);
                var showPlayBtn = UIFactory.CreateButton(showLoadBtns, "play", $"{LocalizedStr("gui_play")}", null);
                NewGUI.Instance.AssignToGroups(showPlayBtn.GameObject, new()
            {
                    { new GroupPolicy(ObjectGroup.Menu, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Loading, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Results, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.SetActive), () => true },
                    { new GroupPolicy(ObjectGroup.Explore, GroupOperation.SetActive), () => false },
                    { new GroupPolicy(ObjectGroup.Editor, GroupOperation.SetActive), () => false }
            });

                UIFactory.SetLayoutElement(showPlayBtn.GameObject, 30, 20, null, 0, null, null, null);

                var showInfoGroup = UIFactory.CreateHorizontalGroup(ControlledObject, "infoArea", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                var placeholder = UIFactory.CreateLabel(showInfoGroup, "ShowDesc", $"...", TextAnchor.UpperLeft, default, true, 14);
                UIFactory.SetLayoutElement(placeholder.gameObject, minHeight: 25, flexibleHeight: 0, preferredWidth: 4);
                UIFactory.SetLayoutElement(showInfoGroup, minHeight: 55, flexibleHeight: 0);

                var showInfoButtons = UIFactory.CreateVerticalGroup(showInfoGroup, "infoArea", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                var viewImgBtn = UIFactory.CreateButton(showInfoButtons, "viewImg", $"{LocalizedStr("gui_show_image")}", null);
                UIFactory.SetLayoutElement(viewImgBtn.GameObject, minHeight: 25, flexibleHeight: 0);
                UIFactory.SetLayoutElement(showInfoButtons, minHeight: 25, flexibleHeight: 0);

                var showRoundList = UIFactory.CreateHorizontalGroup(ControlledObject, "roundsArea", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                var hell = UIFactory.CreateScrollView(showRoundList, "showRounds", out GameObject content, out AutoSliderScrollbar scrollBar, new(0.1f, 0.1f, 0.1f));
                UIFactory.SetLayoutElement(hell, flexibleHeight: 9999, minHeight: 120);
                Transform settingsList = hell.GetComponent<ScrollRect>().content.transform;
                var showlist = UIFactory.CreateLabel(settingsList.gameObject, "showList", $"...", TextAnchor.LowerLeft, default, true, 14);
                //UIFactory.SetLayoutElement(hotkeys.previewTheme, minHeight: 25, flexibleHeight: 0);
                showInfoGroup.gameObject.SetActive(false);
                showRoundList.gameObject.SetActive(false);

                GameObject icoGrp = UIFactory.CreateVerticalGroup(showRoundList, "Image", false, false, true, true, 0, new Vector4(0, 0, 0, 0), childAlignment: TextAnchor.UpperLeft);
                var showIco = UIFactory.CreateUIObject("gradient", icoGrp).AddComponent<Image>();
                UIFactory.SetLayoutElement(showIco.gameObject, minHeight: 170, preferredHeight: 170, flexibleHeight: 170, flexibleWidth: 168, preferredWidth: 168, minWidth: 168);

                var SLG_Gameplay = UIFactory.CreateHorizontalGroup(ControlledObject, "ingameUI", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                NewGUI.Instance.AssignToGroups(SLG_Gameplay, new()
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
                var showInfo = UIFactory.CreateLabel(ControlledObject, "showInfo", $"{LocalizedStr("gui_loaded_show")}: ...", TextAnchor.LowerCenter, default, true, 14);
                UIFactory.SetLayoutElement(showInfo.gameObject, minHeight: 25, flexibleHeight: 0);
                GameObject placeholder42 = UIFactory.CreateLabel(ControlledObject, "ShowLoaderDesc", $"{LocalizedStr("gui_show_loader_desc")}", TextAnchor.LowerCenter, default, true, 14).gameObject;
                UIFactory.SetLayoutElement(placeholder42.gameObject, preferredHeight: 1000, flexibleHeight: 9999, flexibleWidth: 9999);

                sL.SetUIReferences([showListDrop, showSearchBar, showRoundList, showIco, randomShow, placeholder, showInfo, showInfoGroup, showlist, viewImgBtn, showPlayBtn]);
            }));
        }

        internal override void Refresh()
        {

        }
    }
}
