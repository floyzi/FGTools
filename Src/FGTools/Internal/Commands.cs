using FG.Common;
using FGClient.UI;
using System;
using static FGTools.States.Logic.FGTStateManager;

namespace FGTools.Internal
{
    internal static class Commands
    {
        internal static Action<FGTState> OnStateChange;
        internal static Action OnFGCPlaymodeEnter;
        internal static Action OnFGCPlaymodeExit;
        internal static Action OnTargetsParsed;
        internal static Action OnMenuEnter;
        internal static Action OnRoundStarts;
        internal static Action OnRoundEnds;
        internal static Action OnIntroStarts;
        internal static Action OnIntroEnds;
        internal static Action OnFinished;
        internal static Action<InitialiseClientOverlayEvent> OnOverlayInitialize;
        internal static Action OnRoundLoaded;
        internal static Action OnLapComplete;
        internal static Action<MPGNetObject> OnCheckpointReached;
        internal static Action OnQualified;
        internal static Action OnEliminated;
        internal static Action OnWon;
        internal static Action OnConnectedToServer;
        internal static Action<EnumGameMessageType> OnRecivedMessage;
    }
}
