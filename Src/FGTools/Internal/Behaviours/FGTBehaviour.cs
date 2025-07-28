using FGClient;
using FGTools.Internal.Extensions;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FGTools.Internal.Behaviours
{
    internal class FGTBehaviour : MonoBehaviour
    {
        internal static FGTBehaviour _mainBehaviour;
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
            FGTBase.FGTServiceManager?.Update();
            FGTBase.StateManager?.Update();
        }

        void OnSceneWasLoaded(Scene scene, LoadSceneMode mode)
        {
            FGTBase.StateManager?.OnSceneLoaded(scene, mode);
        }

        void OnGUI()
        {
            FGTBase.FGTServiceManager?.DrawGUI();
            FGTBase.StateManager?.DrawGUI();
        }
    }
}
