extern alias wle;
using System;
using System.Text;
using Discord;
using FGTools.HarmonyPatches;
using FGTools.Internal;
using FGTools.Services.Logic;
using static FGTools.Config.Config;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Launcher;
using static FGTools.States.Logic.FGTStateManager;
using LogLevel = BepInEx.Logging.LogLevel;

namespace FGTools.Services
{
    internal class DiscordRPCService : FGTService
    {
        Discord.Discord _Discord;

        public enum RpcState
        {
            Disabled,
            Unknown,
            MenuLoading,
            Menu,
            LevelLoading,
            LevelPlaying,
            FGCBuild,
            FGCTest,
            Results,
        }

        public RpcState State = RpcState.Unknown;

        public override void RegisterService()
        {
            if (!AllowRPC.Value)
            {
                FGTLog(LogLevel.Info, GetType(), $"RPC Disabled in config.");
                return;
            }

            FGTLog(LogLevel.Info, GetType(), $"Initializing...");

            GameActions.OnFGCPlaymodeEnter = new(() =>
            {
                HandleRPCState(RpcState.FGCTest);
            });

            GameActions.OnFGCPlaymodeExit = new(() =>
            {
                HandleRPCState(RpcState.FGCBuild);
            });

            GameActions.OnStateChange = new((ToolsState res) =>
            {
                switch (res)
                {
                    case ToolsState.BeforeMenu:
                        HandleRPCState(RpcState.MenuLoading);
                        break;
                    case ToolsState.Menu:
                        HandleRPCState(RpcState.Menu);
                        break;
                    case ToolsState.RoundLoading:
                        HandleRPCState(RpcState.LevelLoading);
                        break;
                    case ToolsState.GameActive:
                        HandleRPCState(RpcState.LevelPlaying);
                        break;
                    case ToolsState.InCreative:
                        HandleRPCState(RpcState.FGCBuild);
                        break;
                    default:
                        break;
                }
            });

            try
            {
                _Discord = new Discord.Discord(DiscordAppID, (ulong)CreateFlags.NoRequireDiscord);
            }
            catch
            {

            }
        }

        void HandleRPCState(RpcState newState)
        {
            if (_Discord == null || !AllowRPC.Value)
                return;

            try
            {
                var prevState = State;
                Discord.Activity act = default;
                State = newState;

                switch (State)
                {
                    case RpcState.MenuLoading:
                        act = new Discord.Activity { 
                            State = "Loads Into Main Menu" 
                        };
                        break;
                    case RpcState.Menu:
                        act = new Discord.Activity
                        {
                            State = "In The Main Menu",
                            Timestamps = {
                                    Start =  DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                }
                        };
                        break;
                    case RpcState.LevelLoading:
                        act = new Discord.Activity
                        {
                            Details = $"Loading in {CleanStr(StateManager.CurrentRound.DisplayName.Text, true)}",
                            Timestamps = {
                                    Start =  DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                },
                            Assets = {
                                    SmallImage =  $"ui_medal_icon_{StateManager.CurrentRound.LevelBadgeName.ToLower()}",
                                    SmallText = $"{CleanStr(StateManager.CurrentRound.DisplayName.Text, true)} icon"
                                }
                        };
                        break;
                    case RpcState.LevelPlaying:
                        string defRound = StateManager.IsPlayingExplore == true ? "Playing explore" : $"Playing";
                        act = new Discord.Activity
                        {
                            Details = $"{defRound} {Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(CleanStr(CGM._round.DisplayName.Text, true)))}",
                            Timestamps = {
                                    Start =  DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                },
                            Assets = {
                                    SmallImage =  $"ui_medal_icon_{CGM._round.LevelBadgeName.ToLower()}",
                                    SmallText = $"{CleanStr(CGM._round.DisplayName.Text, true)} icon"
                                }
                        };
                        if (FGTServiceManager.GetService<RoundOptionsService>().ReturnLatestOptions().TimeLimit)
                            act.Timestamps.End = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)CGM.CurrentGameSession.GamePlayingTimeRemaining;
                        break;
                    case RpcState.FGCBuild:
                        act = new Discord.Activity
                        {
                            State = $"{FGCHarmonyPatches.GetLevelName()} | {FGCHarmonyPatches.GetLevelTheme()}",
                            Details = $"In Creative - Building",
                            Timestamps = {
                                    Start =  DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                },
                            Assets = {
                                    SmallImage =  FGCHarmonyPatches.GetLevelIcoRPC(),
                                    SmallText = $"{FGCHarmonyPatches.GetLevelMode()} icon"
                                }
                        };
                        break;
                    case RpcState.FGCTest:
                        act = new Discord.Activity
                        {
                            State = $"{FGCHarmonyPatches.GetLevelName()} | {FGCHarmonyPatches.GetLevelTheme()}",
                            Details = $"In Creative - Testing",
                            Timestamps = {
                                    Start =  DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                },
                            Assets = {
                                    SmallImage =  FGCHarmonyPatches.GetLevelIcoRPC(),
                                    SmallText = $"{FGCHarmonyPatches.GetLevelMode()} icon"
                                }
                        };
                        break;
                    case RpcState.Results:
                        act = new Discord.Activity
                        {
                            State = "On The Results screen",
                            Timestamps = {
                                    Start =  DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                }
                        };
                        break;
                    default:
                        FGTLog(LogLevel.Warning, GetType(), $"Can't handle {newState}. Disposing");
                        _Discord.Dispose();
                        State = RpcState.Unknown;
                        _Discord = null;
                        break;
                }
                act.Assets.LargeImage = "fgtools_logo_big";
                act.Assets.LargeText = $"{Launcher.DisplayName} V{Launcher.BuildInfo.UI_Version}";

                _Discord.GetActivityManager().UpdateActivity(act, null);
            }
            catch (Exception e)
            {
                FGTLog(LogLevel.Error, GetType(), $"Error: {e.Message}\nFallback to Menu state");
                HandleRPCState(RpcState.Menu);
            }
        }

        public override void UpdateService()
        {
            if (AllowRPC.Value && _Discord != null)
                _Discord.RunCallbacks();
        }

        public override void DrawGUI()
        {
        }

        public override void OnAppFocus(bool focus)
        {

        }

        public override void OnAppQuit()
        {

        }
    }
}
