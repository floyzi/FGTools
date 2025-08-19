using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Events;
using FallGuys.Player.Protocol.Client.Episodes;
using FGClient;
using FGTools.Config;
using FGTools.States.Logic;
using Il2CppInterop.Runtime.Injection;
using UnityEngine.SceneManagement;
using static FG.Common.GameStateMachine;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Internal.FMODTool;

namespace FGTools.States
{
    public class UltimatePartyState(UltimatePartyState.JoinPolicy policy) : FGTState
    {
        public enum JoinPolicy
        {
            FGC,
            Random,
            Endless
        }

        public JoinPolicy CurrentJoinPolicy = policy;
        public Il2CppSystem.Collections.Generic.List<CompletedRoundDto> CompletedRounds = new();
        public int CompletedRoundsСount = 0;
        public int SkippedRoundsCount = 0;

        public void OnRoundComplete(string roundId, bool qual)
        {
            var medal = qual ? "gold" : "pink";

            CompletedRounds.Add(new CompletedRoundDto()
            {
                BadgeId = medal,
                RoundIndex = CompletedRounds.Count + 1,
                LevelId = roundId,
                Qualified = qual,
            });

            if (qual)
                CompletedRoundsСount++;
        }

        public void OnExploreQuit()
        {
            StateManager.FGTCurrentState = FGTStateManager.FGTState.Results;
            StateManager.DropActiveState();
            GlobalGameStateClient.Instance.StoreServiceDataSnapshot();
            CompletedEpisodeDto completedEpisode = new CompletedEpisodeDto()
            {
                Rounds = CompletedRounds,
            };
            GlobalGameStateClient.Instance.SwitchToRewardScreen(completedEpisode);
            //FMODTool.PlayFMODEvent("MUS_InGame_Win_LP",UnloadParam.UnloadOnNewScene, null);
        }

       
        public void RequestNewRound()
        {
            switch (CurrentJoinPolicy)
            {
                case JoinPolicy.FGC:
                    RequestFGCRound();
                    break;
                case JoinPolicy.Random:
                    FGTRoundLoader.LoadRandomCms();
                    break;
                case JoinPolicy.Endless:
                    RequestRandRound();
                    break;
            }
        }

        void RequestRandRound()
        {
            var rand = UnityEngine.Random.Range(1, 102);
            if (rand > 51)
                RequestFGCRound();
            else
                FGTRoundLoader.LoadRandomCms();
        }

        void RequestFGCRound()
        {
            var code = string.Empty;
            if (OnlineCheck != null && OnlineCheck.ExploreCodes != null && OnlineCheck.ExploreCodes.Count > 0)
            {
                var total = OnlineCheck.ExploreCodes.Count;

                switch (ConfigManager.RoundsFilter.Value)
                {
                    case ConfigManager.RandomRoundsFilter.Race:
                        List<string> races = OnlineCheck.ExploreCodes.Where(pair => pair.Value == "GAMEMODE_GAUNTLET").Select(pair => pair.Key).ToList();
                        code = races[UnityEngine.Random.Range(0, races.Count)];
                        break;
                    case ConfigManager.RandomRoundsFilter.Survival:
                        List<string> survivals = OnlineCheck.ExploreCodes.Where(pair => pair.Value == "GAMEMODE_SURVIVAL").Select(pair => pair.Key).ToList();
                        code = survivals[UnityEngine.Random.Range(0, survivals.Count)];
                        break;
                    case ConfigManager.RandomRoundsFilter.Hunt:
                        List<string> points = OnlineCheck.ExploreCodes.Where(pair => pair.Value == "GAMEMODE_POINTS").Select(pair => pair.Key).ToList();
                        code = points[UnityEngine.Random.Range(0, points.Count)];
                        break;
                    default:
                        code = OnlineCheck.ExploreCodes.Keys.ElementAt(UnityEngine.Random.RandomRange(0, total));
                        break;
                }
            }

            if (!string.IsNullOrEmpty(code))
            {
                FGTLog(LogLevel.Info, base.GetType(), $"Requesting new FGC round for explore - code: {code}");
                FGTRoundLoader.LoadFGCRound(code, null, false);
            }
            else
                FGTRoundLoader.LoadRandomCms();
        }

        public override void DisplayGUI()
        {
        }

        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
        }

        public override void OnStateSet()
        {
            FGTLog(LogLevel.Info, base.GetType(), $"[{base.GetType().Name}] Joined explore with join policy = {CurrentJoinPolicy}");
            RequestNewRound();
        }

        public override void UpdateState()
        {
        }

        public override void OnStateExit()
        {
        }
    }
}
