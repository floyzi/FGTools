#if INCLUDE_BUNDLELOADER
using BepInEx.Logging;
using FGTools.Services.Logic;
using Mediatonic.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using static FGTools.Internal.Extensions.FLZ_Extensions;

namespace FGTools.Services
{
    internal class BundleLoaderService : FGTService
    {
        public override void RegisterService()
        {
            
        }

        public override void UpdateService()
        {
            if (Input.GetKeyDown(KeyCode.F11))
                toggleForBundler = !toggleForBundler;
        }

        public override void DrawGUI()
        {
            if (toggleForBundler)
            {
                GUI.Box(new Rect(10f, 10f, 250f, 102f), "");
                GUI.Label(new Rect(15f, 15f, 255f, 30f), $"Bundle Loader");
                LoadField = GUI.TextField(new Rect(15f, 40f, 240f, 20f), LoadField);
                if (GUI.Button(new Rect(15f, 60f, 80f, 20f), "Single"))
                {
                    LoadSceneFromBundle(LoadSceneMode.Single);
                }
                if (GUI.Button(new Rect(95f, 60f, 80f, 20f), "Additive"))
                {
                    LoadSceneFromBundle(LoadSceneMode.Additive);
                }
                if (GUI.Button(new Rect(175f, 60f, 80f, 20f), "List"))
                {
                    BundlePreloader();
                }
                if (GUI.Button(new Rect(15f, 80f, 240f, 20f), "Scenes List Directory"))
                {
                    Application.OpenURL(Plugin.BundlesDir + "ScenesInfo");
                }
            }
        }


        string LoadField;
        private string LoadedSceneBundle;
        private string LoadedScene;
        private AssetBundle bundle;
        bool toggleForBundler = false;
        void BundlePreloader()
        {
            List<string> output = new List<string>();
            string[] bundles = null;
            AssetBundle bundleHolder;
            float num = 0f;
            string file = Plugin.BundlesDir + "ScenesInfo\\" + $"!Output_{DateTime.UtcNow:HH-mm-ss-ff}.txt";

            using (var createdFile = File.Create(file))
            {
                try
                {
                    output.Insert(0, "All scenes list for bundle files that end on .bundle");
                    if (Directory.Exists(Plugin.BundlesDir))
                        bundles = Directory.GetFiles(Plugin.BundlesDir, "*.bundle", SearchOption.AllDirectories);

                    foreach (string bundle in bundles)
                    {
                        try
                        {
                            bundleHolder = AssetBundle.LoadFromFile(bundle);
                            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(bundleHolder.GetAllScenePaths()[0]);
                            bundleHolder.Unload(true);
                            output.Add($"{num}. - " + fileNameWithoutExtension + " | " + Path.GetFileName(bundle));
                            num += 1f;
                        }
                        catch (Exception e)
                        {
                            FGTLog(LogLevel.Warning, base.GetType(), $"[BUNDLE PRELOADING] {e.Message}");
                        }
                    }
                }
                catch
                {
                }
            }

            if (!Directory.Exists(Plugin.BundlesDir + "ScenesInfo"))
                Directory.CreateDirectory(Plugin.BundlesDir + "ScenesInfo");

            File.AppendAllLines(file, output);
            File.AppendAllText(file, $"\nTotal scenes here: {output.Count}");
        }

        void LoadSceneFromBundle(LoadSceneMode SceneMode)
        {
            try
            {
                if (LoadedSceneBundle == LoadField)
                {
                    SceneManager.LoadScene(LoadedScene, SceneMode);
                }
                else
                {
                    bundle = AssetBundle.LoadFromFile(Plugin.BundlesDir + LoadField);
                    string parsedScene = Path.GetFileNameWithoutExtension(bundle.GetAllScenePaths()[0]);
                    LoadedSceneBundle = LoadField;
                    LoadedScene = parsedScene;
                    SceneManager.LoadScene(parsedScene, SceneMode);
                }
            }
            catch (Exception e)
            {
                FGTLog(LogLevel.Fatal, base.GetType(), $"Unable to load bundle. {e.Message}");
            }
        }
    }
}
#endif