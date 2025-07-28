using FGClient;
using FGTools.Internal.Behaviours;
using FGTools.Services;
using FGTools.Services.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using static FGTools.Services.Logic.FGTServiceManager;

namespace FGTools.States.Logic
{
    public interface IFGTState
    {
        void OnStateSet();
        void UpdateState();
        void OnSceneLoaded(Scene scene, LoadSceneMode mode);
        void DisplayGUI();
        void OnStateExit();
    }

    public abstract class FGTBase
    {
        internal static FGTStateManager StateManager
        {
            get
            {
                return FGTStateManager._stateManager;
            }
        }

        internal static ClientGameManager CGM
        {
            get
            {
                GlobalGameStateClient.Instance.GameStateView.GetLiveClientGameManager(out var cgm);
                return cgm;
            }
        }

        internal static FallGuyBehaviour FGBehaviour
        {
            get
            {
                return FallGuyBehaviour._instance;
            }
        }

        internal static FGTServiceManager FGTServiceManager
        {
            get
            {
                return FGTServiceManager.Instance;
            }
        }

        internal static RoundLoaderService FGTRoundLoader
        {
            get
            {
                return FGTServiceManager.Instance.GetService<RoundLoaderService>();
            }
        }

        internal static OnlineCheckService OnlineCheck
        {
            get
            {
                return FGTServiceManager.Instance.GetService<OnlineCheckService>();
            }
        }

        internal static FGTBehaviour Instance
        {
            get
            {
                return FGTBehaviour._mainBehaviour;
            }
        }
    }
    public abstract class FGTState : FGTBase, IFGTState
    {
        public abstract void OnSceneLoaded(Scene scene, LoadSceneMode mode);
        public abstract void OnStateSet();
        public abstract void UpdateState();
        public abstract void DisplayGUI();
        public abstract void OnStateExit();
    }
}
