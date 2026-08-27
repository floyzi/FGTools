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

        internal override string TabName => "gui_presets_title";
        internal override string TabTitle => "gui_presets_title";

        internal override void Destroy()
        {

        }

        internal override void Draw(GameObject root)
        {
            ControlledObject = UIFactory.CreateVerticalGroup(root, $"Tab_{Tab}", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(ControlledObject, minHeight: 25, flexibleHeight: 0);

            TryDrawUI(() => FGTTargetSettings.CosmeticPresets, ControlledObject, new(() =>
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

                var presetInfoZone = UIFactory.CreateHorizontalGroup(ControlledObject, "presetInfoZone", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                
                var infoScroll = UIFactory.CreateScrollView(presetInfoZone, "PRESETINFO", out GameObject content, out var scrollBar, new(0.1f, 0.1f, 0.1f));
                UIFactory.SetLayoutElement(infoScroll, flexibleHeight: 9999, minHeight: 250);

                var info = infoScroll.GetComponent<ScrollRect>().content.transform;
                var presetInfo = UIFactory.CreateLabel(info.gameObject, "presetInfo", $"{LocalizedStr("gui_presets_desc")}", TextAnchor.LowerLeft, default, true, 14);

                Service.SetUIReferences(
                [
                    presetDrop,
                    presetInfo
                ]);

                var bottomLine = UIFactory.CreateLabel(ControlledObject, "creditsInfo_2", $"{LocalizedStr("gui_presets_desc")}", TextAnchor.LowerCenter, default, true, 14);
                UIFactory.SetLayoutElement(bottomLine.gameObject, preferredHeight: 1000, flexibleHeight: 9999, flexibleWidth: 9999);
            }));

        }

        internal override void Refresh()
        {

        }
    }
}
