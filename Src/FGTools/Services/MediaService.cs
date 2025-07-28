using DG.Tweening;
using FGTools.Services.Logic;
using Il2CppInterop.Runtime.Attributes;
using Levels.Obstacles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using static FGTools.UI.ReadyPopups;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using BepInEx.Logging;
namespace FGTools.Services
{
    internal class MediaService : FGTService
    {
        int imgCount = 0;
        public bool imgLoadNearFG = true;
        public float transX;
        public float transY;
        public float transZ;
        public bool followFGPos = true;
        public bool asCube;
        public bool urlLoad;
        public string url = "http://";
        public string imgPath = "example.png";
        Sprite ObedSprite;

        [HideFromIl2Cpp]
        public IEnumerator LoadImage(string path, bool pingas = false, bool alwaysURL = false)
        {
            bool urlLoadPrev = urlLoad;
            bool asCubePrev = asCube;
            string baseName = $"Loaded Image {imgCount++}";
            GameObject imageObject = null;
            Texture2D texture;
            if (pingas)
            {
                urlLoad = false;
                asCube = true;
            }
            if (!urlLoad && !alwaysURL)
            {
                if (File.Exists(path))
                {
                    texture = new Texture2D(1920, 1080);
                    byte[] imgBytes = File.ReadAllBytes(path);
                    if (!asCube)
                    {
                        imageObject = new GameObject
                        {
                            name = $"{baseName}"
                        };
                        if (texture.LoadImage(imgBytes))
                            ObedSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0f, 0f), 100f, 0U, SpriteMeshType.Tight);
                        var spr = imageObject.AddComponent<SpriteRenderer>();
                        spr.sprite = ObedSprite;
                    }
                    else
                    {
                        imageObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        imageObject.name = $"{baseName} (cube)";
                        texture.LoadImage(imgBytes);
                        Material mat = new(Shader.Find("Standard"))
                        {
                            mainTexture = texture
                        };
                        imageObject.GetComponent<Renderer>().material = mat;
                        if (pingas)
                        {
                            imageObject.name = "PINGAS";
                            imageObject.transform.localScale = new Vector3(50, 15, 3);
                            imageObject.transform.rotation = new(1, 1, 180, 1);
                            imageObject.transform.position = new Vector3(FGBehaviour.FallGuy.transform.position.x, FGBehaviour.FallGuy.transform.position.y, FGBehaviour.FallGuy.transform.position.z - 300f);
                            imageObject.transform.DOMove(new Vector3(FGBehaviour.FallGuy.transform.position.x, FGBehaviour.FallGuy.transform.position.y, FGBehaviour.FallGuy.transform.position.z + 300f), 8.5f);
                            var bouncer = imageObject.AddComponent<COMMON_Bouncer>();
                            bouncer._allowBounceFromSides = true;
                            bouncer._bounceVelocity = new(20, 20, 20);
                            yield return new WaitForSeconds(8.5f);
                            GameObject.Destroy(imageObject);
                            urlLoad = urlLoadPrev;
                            asCube = asCubePrev;
                        }
                    }
                    if (FGBehaviour != null && imgLoadNearFG)
                        imageObject.transform.position = FGBehaviour.transform.position;
                    else
                        imageObject.transform.position = new Vector3(transX, transY, transZ);
                }
                else
                {
                    string msg = $"{LocalizedStr("unable_to_find_path")} {path}";
                    ErrorPopup(msg);
                }
            }
            else
            {
                UnityWebRequest request = UnityWebRequestTexture.GetTexture(path);
                yield return request.SendWebRequest();

                if (request.isHttpError || request.isNetworkError)
                {
                    string error = request.error;
                    FGTLog(LogLevel.Info, base.GetType(), $"{LocalizedStr("unable_to_load_img")} " + error);
                    ErrorPopup(error);
                }
                else
                {
                    texture = DownloadHandlerTexture.GetContent(request);
                    byte[] imgBytes = texture.EncodeToPNG();
                    if (!asCube)
                    {
                        imageObject = new GameObject
                        {
                            name = $"{baseName} (URL)"
                        };
                        imageObject.AddComponent<SpriteRenderer>();

                        if (texture.LoadImage(imgBytes))
                            ObedSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0f, 0f), 100f, 0U, SpriteMeshType.Tight);
                        SpriteRenderer spr = imageObject.GetComponent<SpriteRenderer>();
                        spr.sprite = ObedSprite;
                    }
                    else
                    {
                        imageObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        imageObject.name = $"{baseName} (cube) (URL)";
                        texture.LoadImage(imgBytes);
                        Material mat = new(Shader.Find("Standard"))
                        {
                            mainTexture = texture
                        };
                        imageObject.GetComponent<Renderer>().material = mat;
                    }

                    if (FGBehaviour != null && imgLoadNearFG)
                        imageObject.transform.position = FGBehaviour.transform.position;
                    else
                        imageObject.transform.position = new Vector3(transX, transY, transZ);
                }
            }
        }

        public override void DrawGUI()
        {

        }

        public override void RegisterService()
        {

        }

        public override void UpdateService()
        {

        }
    }
}
