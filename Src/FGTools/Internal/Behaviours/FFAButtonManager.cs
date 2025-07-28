using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using FGTools.States.Logic;
using UnityEngine;
using static FGTools.Internal.Extensions.FLZ_Extensions;

namespace FGTools.Internal.Behaviours
{
    internal class FFAButtonManager : ToolsBehaviour
    {
        ButtonBasherArenaManager _arenaManager;
        int activeBtnsLimit = -1;
        int pressedBtns = 0;
        HashSet<int> activeButtons = [];
        void Awake()
        {
            _arenaManager = Resources.FindObjectsOfTypeAll<ButtonBasherArenaManager>().FirstOrDefault();
            RollRandomButtons();
        }

        public void PushButton(ScoredButton button)
        {
            if (button._isAnActiveTarget)
            {
                button.AllowButtonToReset(false);
                button._isAnActiveTarget = false;
                CGM._soloScoreManager.AwardSoloPoints(FallGuyBehaviour._instance.FGMPG.NetID, 1);
                button.UpdateVisuals(button._isAnActiveTarget);
                FallGuyBehaviour._instance.controller.GetComponent<FFAButtonManager>().UnRegisterButton(button.GetInstanceID(), true);
                button.HandleStatePushed();
            }
        }

        void RollRandomButtons()
        {
            activeBtnsLimit = UnityEngine.Random.Range(2, 4);
            for (int i = 0; i < activeBtnsLimit; i++)
            {
                var btn = _arenaManager.ScoreableButtons[UnityEngine.Random.Range(0, _arenaManager.ScoreableButtons.Count)];
                if (!btn._isAnActiveTarget)
                {
                    btn.SetAsActiveTarget(0);
                    activeButtons.Add(btn.GetInstanceID());
                }
            }
        }

        public void UnRegisterButton(int button, bool isPressed)
        {
            if (activeButtons.Contains(button))
            {
                FGTLog(LogLevel.Info, base.GetType(), "btn unregister");
                activeButtons.Remove(button);
                var targetBtn = GameObject.FindObjectFromInstanceID(button).Cast<ScoredButton>();
                targetBtn._isAnActiveTarget = false;
                targetBtn.UpdateVisuals(false);
                if (isPressed)
                    pressedBtns++;
                else
                    pressedBtns--;
            }
        }

        void Update()
        {
            if (pressedBtns >= 2)
            {
                foreach (int btn in activeButtons)
                    UnRegisterButton(btn, false);
                RollRandomButtons();
            }
        }
    }
}
