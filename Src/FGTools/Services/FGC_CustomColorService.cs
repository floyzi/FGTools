#if INCLUDE_FGC_CUSTOM_COLORS
extern alias wle;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using Mediatonic.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UniverseLib.Utility;
using static FGTools.States.Logic.FGTStateManager;
using static FGTools.Internal.Extensions.FLZ_Extensions;
using static FGTools.Services.LocalizationService;
using static FGTools.Config.ConfigManager;
using BepInEx.Logging;

namespace FGTools.Services
{
    internal class FGC_CustomColorService : FGTService
    {
        bool updatedColor = false;
        string editableColor;
        string currColor;
        string prevCol = null;
        public override void DrawGUI()
        {
            if (StateManager.FGTCurrentState == FGTStateEnum.InCreative && wle.FG.Common.LevelEditorManager.Instance != null && wle.FG.Common.LevelEditorManager.Instance.SelectedObject != null && wle.FG.Common.LevelEditorManager.Instance.SelectedObject.gameObject.GetComponent<wle.LevelEditorColourChangerListener>() != null)
            {
                var CCP = wle.FG.Common.LevelEditorManager.Instance.SelectedObject.gameObject.GetComponent<wle.LevelEditorColourChangerParameter>();
                var CCL = wle.FG.Common.LevelEditorManager.Instance.SelectedObject.gameObject.GetComponent<wle.LevelEditorColourChangerListener>();
                if (!updatedColor)
                {
                    currColor = CCP._currentColour.ToHex();
                    prevCol = currColor;
                    editableColor = currColor;
                    FGTLog(LogLevel.Info, base.GetType(), currColor);
                    updatedColor = true;
                }

                if (prevCol != CCP._currentColour.ToHex())
                    updatedColor = false;

                float offsetX = Screen.width - 210f;
                float offsetY = 25f;

                ColorUtility.TryParseHtmlString("#" + editableColor, out UnityEngine.Color col);

                GUIStyle customCol = new();
                customCol.normal.textColor = col;

                GUI.Box(new Rect(offsetX, offsetY, 200f, 90), "");
                GUI.Label(new Rect(offsetX + 5f, offsetY + 3f, 160, 20f), $"{LocalizedStr("gui_fgc_hex_title")}");

                GUI.Label(new Rect(offsetX + 65f, offsetY + 25f, 160, 20f), $"{LocalizedStr("gui_fgc_hex")}");

                editableColor = GUI.TextField(new Rect(offsetX + 5f, offsetY + 25f, 55, 20f), editableColor);
                GUI.Label(new Rect(offsetX + 65f, offsetY + 45f, 160, 20f), $"{LocalizedStr("gui_fgc_hex_preview")}");
                GUI.Label(new Rect(offsetX + 5f, offsetY + 45f, 150, 20f), $"██████", customCol);

                if (GUI.Button(new Rect(offsetX + 5f, offsetY + 65f, 190, 20f), LocalizedStr("gui_fgc_hex_set")))
                {
                    CCL.SetColour(col);
                    CCP._currentColour = col;
                }

            }
            else
                updatedColor = false;
        }

        public override void RegisterService()
        {
        }

        public override void UpdateService()
        {
        }
    }
}
#endif