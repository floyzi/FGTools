extern alias wle;

using FGClient.UI;
using FGTools.Content;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.UI.Tabs.Logic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using wle::Wushu.LevelEditor.Runtime.UI.LevelBrowser;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.UI.FGToolsUI;
using static FGTools.UI.ReadyPopups;

namespace FGTools.UI.Tabs
{
    internal class ImgInFgcTab : UITab<MediaService>
    {
        public ImgInFgcTab() : base(Tab.IMG2FGCLoader, FGTServiceManager.GetService<MediaService>())
        {
            StatePerGroup = new()  
            {
                { new GroupPolicy(ObjectGroup.Editor, GroupOperation.Interactable), () => false },
                { new GroupPolicy(ObjectGroup.Menu, GroupOperation.Interactable), () => true },
                { new GroupPolicy(ObjectGroup.Gameplay, GroupOperation.Interactable), () => false },
                { new GroupPolicy(ObjectGroup.Loading, GroupOperation.Interactable), () => false }
            };
        }

        internal string imgHeight = "75";
        internal string imgWidth = "75";
        public bool shouldDeleteWhitePixels = false;
        public bool shouldDeleteBlackPixels = false;
        public bool isDigital = false;

        internal override string TabName => "gui_img2fgc_tab";
        internal override string TabTitle => "gui_img2fgc";

        internal override void Draw(GameObject root)
        {
            ControlledObject = UIFactory.CreateVerticalGroup(root, $"Tab_{Tab}", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(ControlledObject, minHeight: 25, flexibleHeight: 0);

            FGToolsUI.Instance.TryDrawUI(() => FGTTargetSettings.Img2Fgc, ControlledObject, new(() =>
            {
                //image input file name area
                GameObject img2fgc_imageName = UIFactory.CreateHorizontalGroup(ControlledObject, "imageNameGroup", true, true, true, true, 5, new Vector4(2f, 2f, 2f, 2f), default, null);
                Text img2fgc_imgName = UIFactory.CreateLabel(img2fgc_imageName, "imgNameHint", $"{LocalizedStr("gui_img2fgc_img_name")}", TextAnchor.UpperLeft, default, true, 14);
                UIFactory.SetLayoutElement(img2fgc_imgName.gameObject, minWidth: 110, flexibleWidth: 0);
                InputFieldRef img2fgcInput = UIFactory.CreateInputField(img2fgc_imageName, "imageNameInput", $"{LocalizedStr("inputfield_placeholder")}");
                img2fgcInput.Text = Service.imgPath;
                img2fgcInput.OnValueChanged += input => { Service.imgPath = input; };
                UIFactory.SetLayoutElement(img2fgcInput.UIRoot, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);

                ButtonRef imagesFolderBtn = UIFactory.CreateButton(img2fgc_imageName, "openImgFolder", $"{LocalizedStr("gui_folder")}", null);
                imagesFolderBtn.OnClick += () => { Application.OpenURL(Launcher.ImgDir); };
                //UIFactory.SetLayoutElement(imagesFolderBtn.GameObject, minHeight: 25, flexibleHeight: 0);
                UIFactory.SetLayoutElement(imagesFolderBtn.Component.gameObject, minWidth: 100, minHeight: 25, flexibleWidth: 0, flexibleHeight: 0);

                //width height area
                GameObject img2fgc_imagescale = UIFactory.CreateHorizontalGroup(ControlledObject, "imageNameGroup", false, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(img2fgc_imagescale, minHeight: 25, flexibleHeight: 0);

                Text img2fgc_wh = UIFactory.CreateLabel(img2fgc_imagescale, "imgWhHint", $"{LocalizedStr("gui_img2fgc_wh")}", TextAnchor.UpperLeft, default, true, 14);
                UIFactory.SetLayoutElement(img2fgc_wh.gameObject, minWidth: 110, flexibleWidth: 0);

                InputFieldRef img2fgc_width = UIFactory.CreateInputField(img2fgc_imagescale, "imageNameInput", $"{LocalizedStr("inputfield_placeholder")}");
                img2fgc_width.Text = imgWidth;
                img2fgc_width.Component.characterLimit = 5;
                img2fgc_width.OnValueChanged += input => { imgWidth = input; };

                UIFactory.SetLayoutElement(img2fgc_width.Component.gameObject, minWidth: 100, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);

                InputFieldRef img2fgc_height = UIFactory.CreateInputField(img2fgc_imagescale, "imageNameInput", $"{LocalizedStr("inputfield_placeholder")}");
                img2fgc_height.Text = imgHeight;
                img2fgc_height.Component.characterLimit = 5;
                img2fgc_height.OnValueChanged += input => { imgHeight = input; };
                UIFactory.SetLayoutElement(img2fgc_height.Component.gameObject, minWidth: 170, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);

                GameObject img2fgc_bools = UIFactory.CreateHorizontalGroup(ControlledObject, "img2fgcBools", false, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(img2fgc_bools, minHeight: 25, flexibleHeight: 0);

                GameObject img2fgc_sdbt = UIFactory.CreateToggle(img2fgc_bools, "img2fgc_sdbt", out Toggle img2fgc_sdbtToggle, out Text img2fgc_sdbtToggle_t);
                UIFactory.SetLayoutElement(img2fgc_sdbt.gameObject, minWidth: 170, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);

                img2fgc_sdbtToggle_t.text = $"{LocalizedStr("gui_img2fgc_should_del_black")}";
                img2fgc_sdbtToggle.isOn = false;
                img2fgc_sdbtToggle.onValueChanged.AddListener(new Action<bool>(t => { shouldDeleteBlackPixels = t; }));

                GameObject img2fgc_sdwt = UIFactory.CreateToggle(img2fgc_bools, "img2fgc_sdwt", out Toggle img2fgc_sdwtToggle, out Text img2fgc_sdwtToggle_t);
                UIFactory.SetLayoutElement(img2fgc_sdwt, flexibleWidth: 9999);
                img2fgc_sdwtToggle_t.text = $"{LocalizedStr("gui_img2fgc_should_del_white")}";
                img2fgc_sdwtToggle.isOn = false;
                img2fgc_sdwtToggle.onValueChanged.AddListener(new Action<bool>(t => { shouldDeleteWhitePixels = t; }));

                GameObject img2fgc_dt = UIFactory.CreateToggle(img2fgc_bools, "img2fgc_dt", out Toggle img2fgc_dtToggle, out Text img2fgc_dtToggle_t);
                UIFactory.SetLayoutElement(img2fgc_dt, flexibleWidth: 9999);
                img2fgc_dtToggle_t.text = $"{LocalizedStr("gui_img2fgc_digital")}";
                img2fgc_dtToggle.isOn = false;
                img2fgc_dtToggle.onValueChanged.AddListener(new Action<bool>(t => { isDigital = t; }));

                GameObject img2fgc_actions = UIFactory.CreateHorizontalGroup(ControlledObject, "img2fgcActions", false, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(img2fgc_actions, minHeight: 25, flexibleHeight: 0);
                ButtonRef genLevel = UIFactory.CreateButton(img2fgc_actions, "genLevel", $"{LocalizedStr("gui_img2fgc_gen_level")}", null);
                genLevel.OnClick += () => { IMG2FGCAlert(); };
                UIFactory.SetLayoutElement(genLevel.GameObject, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);
                ButtonRef repAll = UIFactory.CreateButton(img2fgc_actions, "repAll", $"{LocalizedStr("gui_img2fgc_replace")}", GUIRed);
                repAll.OnClick += () =>
                {

                    var tiles = Resources.FindObjectsOfTypeAll<LevelBrowserTileViewModel>().ToList().FindAll(x => x.name.Contains("Clone"));
                    var lvl = Path.Combine(Application.persistentDataPath, "Img2FGC.json");

                    if (!File.Exists(lvl))
                    {
                        ErrorPopup($"{LocalizedStr("img2fgc_uhh")}");
                        return;
                    }

                    if (tiles == null || tiles.Count == 0)
                    {
                        ErrorPopup($"{LocalizedStr("img2fgc_no_levels")}");
                        return;
                    }

                    foreach (var tile in tiles)
                    {
                        if (tile != null && tile.TileData != null && tile.TileData.level != null && tile.TileData.level._levelJSON != null && tile.TileData.level._levelJSON._url != null)
                            tile.TileData.level._levelJSON._url = $"file://{lvl}";
                    }

                };
                UIFactory.SetLayoutElement(repAll.GameObject, minHeight: 25, flexibleHeight: 0, flexibleWidth: 9999);
                GameObject imageViewport = UIFactory.CreateVerticalGroup(ControlledObject, "ImageViewport", false, false, true, true, bgColor: new(1, 1, 1, 0), childAlignment: TextAnchor.MiddleCenter);
                UIFactory.SetLayoutElement(imageViewport, flexibleWidth: 9999, flexibleHeight: 9999);

                GameObject imageHolder = UIFactory.CreateUIObject("ImageHolder", imageViewport);
                UnityEngine.UI.LayoutElement imageLayout = UIFactory.SetLayoutElement(imageHolder, 356, 170, 0, 0);

                GameObject actualImageObj = UIFactory.CreateUIObject("ActualImage", imageHolder);
                RectTransform actualRect = actualImageObj.GetComponent<RectTransform>();
                actualRect.anchorMin = new(0, 0);
                actualRect.anchorMax = new(1, 1);
                actualImageObj.AddComponent<Image>().sprite = GetSpriteFromFile(Launcher.AssetsDir + "obedguyslore.png", 356, 170);
                GameObject img2fgc_about = UIFactory.CreateLabel(ControlledObject, "img2fgc_about", $"{LocalizedStr("gui_img2fgc_desc")}\n{LocalizedStr("gui_img2fgc_credits")}", TextAnchor.LowerCenter, default, true, 14).gameObject;
                UIFactory.SetLayoutElement(img2fgc_about.gameObject, preferredHeight: 1000, flexibleHeight: 9999, flexibleWidth: 9999);
            }));
        }

        void IMG2FGCAlert()
        {
            DoModal(new(LocalizedStr("img2fgc_title"), LocalizedStr("img2fgc_desc"), UIModalMessage.ModalType.MT_OK_CANCEL, UIModalMessage.OKButtonType.Disruptive, new Action<bool>((wasok) =>
            {
                if (wasok)
                {
                    List<string> writeInfo = [];
                    string outputfile = Path.Combine(Application.persistentDataPath, "output.txt");
                    if (File.Exists(outputfile)) File.Delete(outputfile);
                    File.Create(outputfile).Close();
                    writeInfo.Add("path_to_file" + " = " + Launcher.ImgDir + FGTServiceManager.GetService<MediaService>().imgPath);
                    writeInfo.Add("width" + " = " + imgWidth);
                    writeInfo.Add("height" + " = " + imgHeight);
                    writeInfo.Add("shouldDeleteBlackPixels" + " = " + shouldDeleteBlackPixels);
                    writeInfo.Add("shouldDeleteWhitePixels" + " = " + shouldDeleteWhitePixels);
                    writeInfo.Add("isDigital" + " = " + isDigital);
                    File.WriteAllLines(outputfile, writeInfo);
                    Application.OpenURL(Launcher.IMG2FGCExe);
                }
            }), hideLvl: ModalHideGUIType.KeepHiddenForThisModal));
        }

        internal override void Refresh()
        {
        }
    }
}
