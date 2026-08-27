using BepInEx.Unity.IL2CPP.Utils.Collections;
using FGTools.Content;
using FGTools.Internal.Behaviours;
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
using UniverseLib;
using UniverseLib.UI;
using UniverseLib.UI.Models;
using static FGTools.Services.LocalizationService;
using static FGTools.UI.FGToolsUI;
using static FGTools.UI.ReadyPopups;
using static RootMotion.FinalIK.AimPoser;

namespace FGTools.UI.Tabs
{
    internal class MediaLoaderTab : UITab<MediaService>
    {
        public MediaLoaderTab() : base(Tab.MediaLoader, FGTServiceManager.GetService<MediaService>())
        {

        }

        internal override string TabName => "gui_media_tools_title";
        internal override string TabTitle => "gui_media_tools_title";

        ButtonRef _imagesBtn;
        GameObject _mediaTab;
        GameObject _mediaFGPosGrp;
        InputFieldRef _posX;
        InputFieldRef _posY;
        InputFieldRef _posZ;
        InputFieldRef _imgLocalLoad;
        InputFieldRef _imgRemoteLoad;

        internal override void Draw(GameObject root)
        {
            ControlledObject = UIFactory.CreateVerticalGroup(root, $"Tab_{Tab}", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(ControlledObject, minHeight: 30, flexibleHeight: 0);

            GameObject mediaTabs = UIFactory.CreateHorizontalGroup(ControlledObject, "Tabs", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
            UIFactory.SetLayoutElement(mediaTabs, minHeight: 25, flexibleHeight: 0);
            _imagesBtn = UIFactory.CreateButton(mediaTabs, $"Button_Img", $"{LocalizedStr("gui_img_loader")}");

            FGToolsUI.Instance.TryDrawUI(() => FGTTargetSettings.MediaLoaderImages, ControlledObject, new(() =>
            {
                _mediaTab = UIFactory.CreateVerticalGroup(ControlledObject, "img", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                UIFactory.SetLayoutElement(_mediaTab, minHeight: 25, flexibleHeight: 0);
                RuntimeHelper.SetColorBlock(_imagesBtn.Component, UniversalUI.EnabledButtonColor, UniversalUI.EnabledButtonColor * 1.2f);

                //IMG NAME INPUT
                _imgLocalLoad = UIFactory.CreateInputField(_mediaTab, "imgField", $"{LocalizedStr("inputfield_placeholder")}");
                _imgLocalLoad.Text = Service.imgPath;
                _imgLocalLoad.OnValueChanged += input =>
                {
                    Service.imgPath = input;
                };

                UIFactory.SetLayoutElement(_imgLocalLoad.GameObject, 30, 25, null, 0, null, null, null);

                _imgRemoteLoad = UIFactory.CreateInputField(_mediaTab, "imgField_web", $"{LocalizedStr("inputfield_placeholder")}");
                _imgRemoteLoad.Text = Service.url;
                _imgRemoteLoad.OnValueChanged += input =>
                {
                    Service.url = input;
                };

                UIFactory.SetLayoutElement(_imgRemoteLoad.GameObject, 30, 25, null, 0, null, null, null);
                _imgRemoteLoad.GameObject.SetActive(false);


                //BTN ACTS
                GameObject btnActs = UIFactory.CreateHorizontalGroup(_mediaTab, "btnActs", true, true, true, true, 2, new Vector4(2, 2, 2, 2));

                ButtonRef loadBtn = UIFactory.CreateButton(btnActs, "loadBtn", $"{LocalizedStr("gui_media_load")}", new Color(0.2f, 0.3f, 0.2f));
                loadBtn.OnClick += () =>
                {
                    if (!Service.urlLoad)
                        CoroutineRunner.Instance.StartCoroutine(Service.LoadImage(Launcher.ImgDir + Service.imgPath).WrapToIl2Cpp());
                    else
                        CoroutineRunner.Instance.StartCoroutine(Service.LoadImage(Service.url).WrapToIl2Cpp());
                };
                UIFactory.SetLayoutElement(loadBtn.GameObject, 30, 25, null, 0, null, null, null);
                ButtonRef dirBtn = UIFactory.CreateButton(btnActs, "dirBtn", $"{LocalizedStr("gui_folder")}", null);
                dirBtn.OnClick += () =>
                {
                    Application.OpenURL(Launcher.ImgDir);
                };
                UIFactory.SetLayoutElement(dirBtn.GameObject, 30, 25, null, 0, null, null, null);
                ButtonRef destAllBtn = UIFactory.CreateButton(btnActs, "destAllBtn", $"{LocalizedStr("gui_destroy_all")}", GUIRed);
                destAllBtn.OnClick += () =>
                {
                    AreYouSurePopup($"{LocalizedStr("gui_del_images_act")}", popAct: new Action<bool>(wasOk =>
                    {
                        if (wasOk)
                        {
                            foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
                            {
                                if (obj.name.StartsWith("Loaded Image"))
                                    UnityEngine.Object.Destroy(obj.gameObject);
                            }
                        }
                    }));
                };
                UIFactory.SetLayoutElement(destAllBtn.GameObject, 30, 25, null, 0, null, null, null);

                GameObject btnActsRow2 = UIFactory.CreateHorizontalGroup(_mediaTab, "btnActsRow2", true, true, true, true, 2, new Vector4(2, 2, 2, 2));

                ButtonRef flzRuImg = UIFactory.CreateButton(btnActsRow2, "flzruImg", $"{LocalizedStr("gui_flzru_img")}", null);
                flzRuImg.OnClick += () =>
                {
                    var randConfig = OnlineCheck.FGTContent.RandomImages;

                    if (!OnlineCheck.FGTContent.RandomImages.Enabled)
                        return;

                    string url;
                    string urlBase = OnlineCheck.FGTContent.RandomImages.Url;
                    if (randConfig.TotalImages != -1)
                    {
                        int randVal = UnityEngine.Random.Range(0, randConfig.TotalImages);
                        if (randConfig.BannedImages != null && randConfig.BannedImages.Contains(randVal))
                            randVal = randConfig.Fallback;
                        url = $"{urlBase}{randVal}.png";
                    }
                    else
                        url = $"{urlBase}_146.png";

                    CoroutineRunner.Instance.StartCoroutine(Service.LoadImage(url, false, true).WrapToIl2Cpp());
                };
                UIFactory.SetLayoutElement(flzRuImg.GameObject, 30, 25, null, 0, null, null, null);

                GameObject t = UIFactory.CreateHorizontalGroup(_mediaTab, "t", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                GameObject asCube = UIFactory.CreateToggle(t, "asCube", out var asCube_toggle, out var asCube_t);
                UIFactory.SetLayoutElement(asCube.gameObject, minWidth: 170, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);
                asCube_t.text = $"{LocalizedStr("gui_as_cube")}";
                asCube_toggle.isOn = false;
                asCube_toggle.onValueChanged.AddListener((val) => { Service.asCube = val; });
                GameObject nearFG = UIFactory.CreateToggle(t, "nearFG", out var nearFG_toggle, out var nearFG_t);
                UIFactory.SetLayoutElement(nearFG, flexibleWidth: 9999);
                nearFG_t.text = $"{LocalizedStr("gui_near_fg")}";
                nearFG_toggle.isOn = true;
                nearFG_toggle.onValueChanged.AddListener((val) => { Service.imgLoadNearFG = val; });
                GameObject viaURL = UIFactory.CreateToggle(t, "viaURL", out var viaURL_toggle, out var viaURL_t);
                UIFactory.SetLayoutElement(viaURL, flexibleWidth: 9999);
                viaURL_t.text = $"{LocalizedStr("gui_url_load")}";
                viaURL_toggle.isOn = false;
                viaURL_toggle.onValueChanged.AddListener((val) => { Service.urlLoad = val; });

                var mediaFGPosGrp = UIFactory.CreateVerticalGroup(_mediaTab, "mediaFGPosGrp", true, true, true, true, 2, new Vector4(2, 2, 2, 2));
                _posX = UIFactory.CreateInputField(mediaFGPosGrp, "X", $"{LocalizedStr("inputfield_placeholder")}");
                _posX.Text = Service.transX.ToString();
                _posX.OnValueChanged += input =>
                {
                    if (float.TryParse(input, out float x))
                        Service.transX = x;
                };
                UIFactory.SetLayoutElement(_posX.Component.gameObject, 30, 25, null, 0, null, null, null);
                _posY = UIFactory.CreateInputField(mediaFGPosGrp, "Y", $"{LocalizedStr("inputfield_placeholder")}");
                _posY.Text = Service.transY.ToString();
                _posY.OnValueChanged += input =>
                {
                    if (float.TryParse(input, out float y))
                        Service.transY = y;
                };
                UIFactory.SetLayoutElement(_posY.Component.gameObject, 30, 25, null, 0, null, null, null);
                _posZ = UIFactory.CreateInputField(mediaFGPosGrp, "Z", $"{LocalizedStr("inputfield_placeholder")}");
                _posZ.Text = Service.transZ.ToString();
                _posZ.OnValueChanged += input =>
                {
                    if (float.TryParse(input, out float z))
                        Service.transZ = z;
                };
                UIFactory.SetLayoutElement(_posZ.Component.gameObject, 30, 25, null, 0, null, null, null);

                GameObject followFgPos = UIFactory.CreateToggle(mediaFGPosGrp, "followFgPos", out Toggle followFgPos_toggle, out Text followFgPos_t);
                UIFactory.SetLayoutElement(followFgPos.gameObject, minWidth: 170, minHeight: 25, flexibleWidth: 9999, flexibleHeight: 0);
                followFgPos_t.text = $"{LocalizedStr("gui_follow_fg_pos")}";
                followFgPos_toggle.isOn = true;
                followFgPos_toggle.onValueChanged.AddListener((val) => { Service.FollowFGPos = val; });

                Text showInfo = UIFactory.CreateLabel(mediaFGPosGrp, "desc", $"X/Y/Z", TextAnchor.LowerCenter, default, true, 14);
                UIFactory.SetLayoutElement(showInfo.gameObject, minHeight: 25, flexibleHeight: 0);
                mediaFGPosGrp.gameObject.SetActive(false);

                Text desc = UIFactory.CreateLabel(_mediaTab, "desc", $"\n{LocalizedStr("gui_imgloader_desc")}\n\n - {LocalizedStr("gui_imgloader_desc_01")}\n - {LocalizedStr("gui_imgloader_desc_02")}", TextAnchor.LowerCenter, default, true, 14);
                //UIFactory.SetLayoutElement(desc.previewTheme, minHeight: 25, flexibleHeight: 0);
            }));

        }

        internal override void Update()
        {
            if (!Service.imgLoadNearFG && _mediaFGPosGrp != null && !_mediaFGPosGrp.activeSelf)
                _mediaFGPosGrp.gameObject.SetActive(true);
            else if (Service.imgLoadNearFG && _mediaFGPosGrp != null && _mediaFGPosGrp.activeSelf)
                _mediaFGPosGrp.gameObject.SetActive(false);

            if (Service.urlLoad && _imgRemoteLoad.GameObject != null && _imgLocalLoad.GameObject.activeSelf)
            {
                _imgLocalLoad.GameObject.SetActive(false);
                _imgRemoteLoad.GameObject.SetActive(true);
            }
            else if (!Service.urlLoad && _imgLocalLoad.GameObject != null && _imgRemoteLoad.GameObject.activeSelf)
            {
                _imgLocalLoad.GameObject.SetActive(true);
                _imgRemoteLoad.GameObject.SetActive(false);
            }

            if (Service.FollowFGPos && FallGuyBehaviour._instance != null && FallGuyBehaviour._instance.FallGuy != null)
            {
                var x = FallGuyBehaviour._instance.FallGuy.transform.position.x;
                var y = FallGuyBehaviour._instance.FallGuy.transform.position.y;
                var z = FallGuyBehaviour._instance.FallGuy.transform.position.z;

                Service.transX = x;
                _posX.Text = x.ToString();
                Service.transY = y;
                _posY.Text = y.ToString();
                Service.transZ = z;
                _posZ.Text = z.ToString();
            }
        }

        internal override void Refresh()
        {

        }
    }
}