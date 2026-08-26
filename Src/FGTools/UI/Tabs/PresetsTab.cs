using FGTools.Content;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using FGTools.UI.Tabs.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using static FGTools.UI.NewGUI;
using static FGTools.Services.LocalizationService;

namespace FGTools.UI.Tabs
{
    internal class PresetsTab : UITab<PresetsService>
    {
        public PresetsTab() : base(Tab.PresetSelector, FGTServiceManager.GetService<PresetsService>())
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

            NewGUI.Instance.TryDrawUI(() => FGTTargetSettings.CosmeticPresets, ControlledObject, new(() =>
            {
                GameObject presetsDropGroup = UIFactory.CreateHorizontalGroup(ControlledObject, "presetsDropGroup", true, true, true, true, 2, new Vector4(2f, 2f, 2f, 2f), default, null);
                UIFactory.SetLayoutElement(presetsDropGroup, minHeight: 30, flexibleHeight: 0);
                GameObject presetsDrop = UIFactory.CreateDropdown(presetsDropGroup, "presetsDrop", out var presetDrop, $"{LocalizedStr("dropdown_placeholder")}", 14, Service.OnPresetsDropSelect, null);
                UIFactory.SetLayoutElement(presetsDropGroup, flexibleWidth: 9999, minHeight: 30, flexibleHeight: 0);

                GameObject presetBtns = UIFactory.CreateHorizontalGroup(ControlledObject, "presetBtns", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                UIFactory.SetLayoutElement(presetBtns, minHeight: 25, flexibleHeight: 0);

                var usePresetBtn = UIFactory.CreateButton(presetBtns, "usePresetBtn", $"{LocalizedStr("gui_use_preset")}", null);
                usePresetBtn.OnClick += Service.TryUsePreset;

                UIFactory.SetLayoutElement(usePresetBtn.GameObject, 30, 20, null, 0, null, null, null);
                var newPresetBtn = UIFactory.CreateButton(presetBtns, "newPresetBtn", $"{LocalizedStr("gui_new_preset")}", new Color(0.2f, 0.3f, 0.2f));
                newPresetBtn.OnClick += Service.MakeNewPresetPopup;

                UIFactory.SetLayoutElement(usePresetBtn.GameObject, 30, 20, null, 0, null, null, null);
                var delPresetBtn = UIFactory.CreateButton(presetBtns, "delPresetBtn", $"{LocalizedStr("gui_delete_preset")}", GUIRed);
                delPresetBtn.OnClick += Service.TryDeletePreset;

                UIFactory.SetLayoutElement(delPresetBtn.GameObject, 30, 20, null, 0, null, null, null);

                GameObject presetInfoZone = UIFactory.CreateHorizontalGroup(ControlledObject, "presetInfoZone", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                GameObject hell = UIFactory.CreateScrollView(presetInfoZone, "PRESETINFO", out GameObject content, out var scrollBar, new(0.1f, 0.1f, 0.1f));
                UIFactory.SetLayoutElement(hell, flexibleHeight: 9999, minHeight: 250);
                Transform settingsList = hell.GetComponent<ScrollRect>().content.transform;
                Text presetInfo = UIFactory.CreateLabel(settingsList.gameObject, "presetInfo", $"{LocalizedStr("gui_presets_desc")}", TextAnchor.LowerLeft, default, true, 14);

                Service.SetUIReferences(
                [
                    presetDrop,
                    presetInfo
                ]);

                Text bottomLine = UIFactory.CreateLabel(ControlledObject, "creditsInfo_2", $"{LocalizedStr("gui_presets_desc")}", TextAnchor.LowerCenter, default, true, 14);
                UIFactory.SetLayoutElement(bottomLine.gameObject, preferredHeight: 1000, flexibleHeight: 9999, flexibleWidth: 9999);
            }));

        }

        internal override void Refresh()
        {

        }
    }
}
