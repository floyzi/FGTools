using FGTools.Content;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.UI.Tabs.Logic;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using static FGTools.Services.LocalizationService;
using static FGTools.UI.FGToolsUI;

namespace FGTools.UI.Tabs
{
    internal class FGCLocalSavesTab : UITab<FGC_LocalSavesService>
    {
        public FGCLocalSavesTab() : base(Tab.FGCAutosaves, FGTServiceManager.GetService<FGC_LocalSavesService>())
        {

        }

        Dropdown _savedLevels;
        Dropdown _levelSaves;

        internal override string TabName => "gui_fgc_local_autosaves";
        internal override string TabTitle => "gui_fgc_local_autosaves";

        internal override void Draw(GameObject root)
        {
            ControlledObject = UIFactory.CreateVerticalGroup(root, $"Tab_{Tab}", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(ControlledObject, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 9999);

            TryDrawUI(() => FGTTargetSettings.FGCLocalSaves, ControlledObject, new(() =>
            {

                var nosaves = UIFactory.CreateLabel(ControlledObject, "nosaves", $"{LocalizedStr("gui_local_save_no_saves")}", TextAnchor.UpperCenter, default, true, 14);
                UIFactory.SetLayoutElement(nosaves.gameObject, 0, 20);

                GameObject dropdowns = UIFactory.CreateHorizontalGroup(ControlledObject, "dropdowns", true, false, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);

                //levels
                GameObject savedLevels = UIFactory.CreateDropdown(dropdowns, "savedLevels", out _savedLevels, $"{LocalizedStr("dropdown_placeholder")}", 14, Service.OnLevelSelect, null);
                UIFactory.SetLayoutElement(savedLevels, minHeight: 25, flexibleHeight: 0);

                //saves
                GameObject levelSaves = UIFactory.CreateDropdown(dropdowns, "levelSaves", out _levelSaves, $"{LocalizedStr("dropdown_placeholder")}", 14, Service.OnSaveSelect, null);
                UIFactory.SetLayoutElement(levelSaves, minHeight: 25, flexibleHeight: 0);

                UIFactory.SetLayoutElement(dropdowns, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                GameObject loadBtnGroup = UIFactory.CreateHorizontalGroup(ControlledObject, "btnGroup", true, false, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                var loadbtn = UIFactory.CreateButton(loadBtnGroup, "LoadSave", LocalizedStr("gui_local_save_load"));
                loadbtn.OnClick = () => { Service.TryToLoadSelectedSave(); };
                UIFactory.SetLayoutElement(loadbtn.GameObject, minHeight: 25, flexibleHeight: 0);
                UIFactory.SetLayoutElement(loadBtnGroup, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);


                GameObject SaveInfoGroup = UIFactory.CreateHorizontalGroup(ControlledObject, "SaveInfoGroup", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);

                GameObject hell = UIFactory.CreateScrollView(SaveInfoGroup, "showRounds", out GameObject content, out var scrollBar, new(0.1f, 0.1f, 0.1f));
                UIFactory.SetLayoutElement(hell, flexibleHeight: 9999, minHeight: 120);
                Transform settingsList = hell.GetComponent<ScrollRect>().content.transform;
                Text showlist = UIFactory.CreateLabel(settingsList.gameObject, "saveInfo", $"{LocalizedStr("gui_local_save_info_placeholder")}", TextAnchor.LowerLeft, default, true, 14);

                GameObject icoGrp = UIFactory.CreateVerticalGroup(SaveInfoGroup, "Image", false, false, true, true, 0, new Vector4(0, 0, 0, 0), childAlignment: TextAnchor.UpperLeft);
                Image showIco = UIFactory.CreateUIObject("gradient", icoGrp).AddComponent<Image>();
                UIFactory.SetLayoutElement(showIco.gameObject, minHeight: 170, preferredHeight: 170, flexibleHeight: 170, flexibleWidth: 270, preferredWidth: 270, minWidth: 270);

                GameObject genericActions = UIFactory.CreateHorizontalGroup(ControlledObject, "genericActions", true, false, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                var newSave = UIFactory.CreateButton(genericActions, "newSave", LocalizedStr("gui_local_save_create"));
                newSave.OnClick = () => { Service.TryAutosaveLevel(); };
                UIFactory.SetLayoutElement(newSave.GameObject, minHeight: 25, flexibleHeight: 0);

                var delAll = UIFactory.CreateButton(genericActions, "delAll", LocalizedStr("gui_local_save_del_all"), GUIRed);
                delAll.OnClick = () => { Service.TryDeleteEverything(); };
                UIFactory.SetLayoutElement(delAll.GameObject, minHeight: 25, flexibleHeight: 0);

                UIFactory.SetLayoutElement(genericActions, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                GameObject saveActions = UIFactory.CreateHorizontalGroup(ControlledObject, "saveActions", true, false, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                var delLevel = UIFactory.CreateButton(saveActions, "delLevel", LocalizedStr("gui_local_save_del_level"), GUIRed);
                delLevel.OnClick = () => { Service.TryDeleteLevel(); };
                UIFactory.SetLayoutElement(delLevel.GameObject, minHeight: 25, flexibleHeight: 0);

                var delSave = UIFactory.CreateButton(saveActions, "delSave", LocalizedStr("gui_local_save_del_save"), GUIRed);
                delSave.OnClick = () => { Service.TryDeleteSave(); };
                UIFactory.SetLayoutElement(delSave.GameObject, minHeight: 25, flexibleHeight: 0);

                UIFactory.SetLayoutElement(saveActions, minHeight: 25, flexibleHeight: 25, preferredHeight: 25);

                Service.SetUIReferences([_savedLevels,
                    _levelSaves,
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

                var placeholder42 = UIFactory.CreateLabel(ControlledObject, "ShowLoaderDesc", $"{LocalizedStr("gui_fgc_local_autosaves_desc")}", TextAnchor.LowerCenter, default, true, 14).gameObject;
                UIFactory.SetLayoutElement(placeholder42.gameObject, preferredHeight: 9999, flexibleHeight: 200, flexibleWidth: 200);
            }));
        }

        internal override void Refresh()
        {

        }

        internal override void Destroy()
        {

        }
    }
}
