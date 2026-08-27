using FGClient;
using FGTools.Internal.Extensions;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FGTools.Internal.Behaviours
{
    internal class FGTBehaviour : MonoBehaviour
    {
        internal static FGTBehaviour _mainBehaviour;

#if DEV
        bool PauseStateUpdates;
        bool PauseServiceUpdates;
        bool PauseStateUI;
        bool PauseServiceUI;
        GUIStyle _labelStyle;
#endif

        void Awake()
        {
            SceneManager.add_sceneLoaded(new System.Action<Scene, LoadSceneMode>(OnSceneWasLoaded));

            if (_mainBehaviour != null)
                Destroy(_mainBehaviour);

            _mainBehaviour = this;
        }

        public void StartUp()
        {
            _ = new FGTStateManager();
            FLZ_Extensions.FGTLog(BepInEx.Logging.LogLevel.Info, GetType(), $"Successfull startup.");
        }

        void Update()
        {
#if DEV
            if (Input.GetKeyDown(KeyCode.PageDown))
                PauseStateUpdates = !PauseStateUpdates;

            if (Input.GetKeyDown(KeyCode.PageUp))
                PauseServiceUpdates = !PauseServiceUpdates;

            if (Input.GetKeyDown(KeyCode.Home))
                PauseStateUI = !PauseStateUI;

            if (Input.GetKeyDown(KeyCode.End))
                PauseServiceUI = !PauseServiceUI;


            if (!PauseServiceUpdates)
                FGTBase.FGTServiceManager?.Update();

            if (!PauseStateUpdates)
                FGTBase.StateManager?.Update();
#else
            FGTBase.FGTServiceManager?.Update();
            FGTBase.StateManager?.Update();
#endif

        }

        void OnSceneWasLoaded(Scene scene, LoadSceneMode mode)
        {
            FGTBase.StateManager?.OnSceneLoaded(scene, mode);
        }

        void OnGUI()
        {
#if DEV
            if (_labelStyle == null)
            {
                var bTex = new Texture2D(1, 1);
                var c = Color.black;
                c.a = 0.5f;
                bTex.SetPixel(0, 0, c);
                bTex.Apply();
                bTex.hideFlags = HideFlags.HideAndDontSave;

                GameObject.DontDestroyOnLoad(bTex);

                _labelStyle = new GUIStyle(GUI.skin.label);

                _labelStyle.normal.background = bTex;
                _labelStyle.padding.left = 5;
                _labelStyle.padding.right = 5;
            }

            var devOverlay = new StringBuilder();
            devOverlay.AppendLine($"MEM: {FGTBase.StateManager.MemUsage:F2} MB");
            devOverlay.AppendLine($"PEAK: {FGTBase.StateManager.PeakMemUsage:F2} MB");

            if (PauseServiceUpdates)
                devOverlay.AppendLine($"Services Paused");

            if (PauseStateUpdates)
                devOverlay.AppendLine($"States Paused");

            if (PauseServiceUI)
                devOverlay.AppendLine($"Services GUI Paused");

            if (PauseStateUI)
                devOverlay.AppendLine($"States GUI Paused");

            var str = devOverlay.ToString().Trim();
            var s1 = _labelStyle.CalcSize(new(str));
            GUI.Label(new(0, 0, s1.x, s1.y), str, _labelStyle);

            if (!PauseServiceUI)
                FGTBase.FGTServiceManager?.DrawGUI();

            if (!PauseStateUI)
                FGTBase.StateManager?.DrawGUI();
#else
            FGTBase.FGTServiceManager?.DrawGUI();
            FGTBase.StateManager?.DrawGUI();
#endif
        }
    }
}
