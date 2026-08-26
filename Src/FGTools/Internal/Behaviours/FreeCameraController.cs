extern alias wle;
using System.Linq;
using Cinemachine;
using FG.Common.Audio;
using FGClient.UI;
using FGTools.Services;
using FGTools.Services.Logic;
using FGTools.States.Logic;
using UnityEngine;
using static FGTools.Config.Config;
using static FGTools.Services.LocalizationService;
using static FGTools.States.Logic.FGTStateManager;

namespace FGTools.Internal.Behaviours
{
    internal class FreeCameraController : ToolsBehaviour
    {
        public GameObject CAM;
        float x;
        float y;
        float maxDistDef;
        float currentDist;
        FallGuysCameraAvoidence GPCams;
        bool enterFreeCam = false;
        bool fetchedDist = false;
        Vector3 lastCamPos = Vector3.zero;
        Quaternion lastCamRot = Quaternion.identity;
        bool freecamPause = false;
        float fcBoxPos;
        bool displayUI = false;
        void Update()
        {
            if (StateManager.FGTCurrentState == FGTStateManager.ToolsState.GameActive || StateManager.FGTCurrentState == FGTStateManager.ToolsState.FGCGameActive)
            {
                if (Input.GetKeyDown(PauseFreeCamHotkey.Value) && StateManager.FGCurrentState == PlayerState.FreeCam)
                {
                    freecamPause = !freecamPause;

                    if (!freecamPause)
                        RewiredManager.Instance.DisableMap(0, 0);
                    else
                        RewiredManager.Instance.EnableMap(0, 0);
                }

                if (Input.GetKeyDown(ToggleFreeCamHotkey.Value) && StateManager.FGCurrentState != PlayerState.FreeFly)
                {
                    if (StateManager.FGCurrentState != PlayerState.FreeCam)
                        _stateManager.HandleFGState(PlayerState.FreeCam);
                    else
                        _stateManager.HandleFGState(PlayerState.Despawned);
                }

                if (Input.GetKeyDown(FreeCamToggleUI.Value))
                {
                    displayUI = !displayUI;
                }

                FreeCamController();
            }
        }

        void FreeCamController()
        {
            if (StateManager.FGCurrentState == PlayerState.FreeCam)
            {
                if (!enterFreeCam)
                {
                    if (lastCamPos == Vector3.zero)
                        lastCamPos = transform.position + new Vector3(0f, 10f, 0f);
                    if (lastCamRot == Quaternion.identity)
                        lastCamRot = transform.rotation;
                    if (StateManager.FGTCurrentState != FGTStateManager.ToolsState.InCreative)
                        CAM = Resources.FindObjectsOfTypeAll<CinemachineBrain>().Last().gameObject;
                    else
                        CAM = GameObject.Find("Main Camera Brain");
                    CAM.transform.position = lastCamPos;
                    CAM.transform.rotation = lastCamRot;
                    Resources.FindObjectsOfTypeAll<IntroCameras>().FirstOrDefault().gameObject.SetActive(false);
                    foreach (FallGuysCameraAvoidence fgca in Resources.FindObjectsOfTypeAll<FallGuysCameraAvoidence>())
                        fgca.gameObject.SetActive(false);
                    enterFreeCam = true;
                    CAM.GetComponent<Camera>().fieldOfView = 90f;
                }

                if (!freecamPause)
                {
                    Vector3 mouseInput = new(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"), 0f);
                    Vector3 movementInput = new(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));

                    float sensitivity = FreeCamSens.Value;
                    float speed = FreeCamSpeed.Value;
                    float deltaTime = 0.02f;

                    x = Mathf.Clamp(x - mouseInput.y * sensitivity, -90f, 90f);
                    y += mouseInput.x * sensitivity;
                    CAM.transform.rotation = Quaternion.Euler(x, y, 0f);
                    CAM.transform.position += CAM.transform.right * speed * movementInput.x * deltaTime + CAM.transform.forward * speed * movementInput.z * deltaTime;
                    lastCamPos = CAM.transform.position;
                    lastCamRot = CAM.transform.rotation;

                    if (Input.GetKey(FreeCamMoveUpHotkey.Value))
                        CAM.transform.position += Vector3.up * FreeCamSpeed.Value * Time.deltaTime;

                    if (Input.GetKey(FreeCamMoveDownHotkey.Value))
                        CAM.transform.position += Vector3.down * FreeCamSpeed.Value * Time.deltaTime;


                    float scrollDelta = Input.GetAxis("Mouse ScrollWheel");

                    if (GPCams != null)
                    {
                        if (scrollDelta != 0f)
                        {
                            GPCams.CurrentDistance += scrollDelta * FreeCamZoomSpeed.Value;
                            GPCams._maxDistance += scrollDelta * FreeCamZoomSpeed.Value;
                            GPCams._minDistance += scrollDelta * FreeCamZoomSpeed.Value;
                            GPCams.CurrentDistance = Mathf.Clamp(GPCams.CurrentDistance, 100f, 999f);
                        }
                    }

                    if (scrollDelta != 0f)
                    {
                        float newFieldOfView = CAM.GetComponent<Camera>().fieldOfView + scrollDelta * FreeCamZoomSpeed.Value;
                        newFieldOfView = Mathf.Clamp(newFieldOfView, 5f, 180f);

                        CAM.GetComponent<Camera>().fieldOfView = newFieldOfView;
                    }
                }

                if (GPCams.gameObject.active)
                    _stateManager.HandleFGState(PlayerState.Despawned);
            }
        }

        public void ExitFC()
        {
            if (GPCams != null)
            {
                GPCams._maxDistance = maxDistDef;
                GPCams._minDistance = maxDistDef;
                GPCams.CurrentDistance = currentDist;
                if (StateManager.FGTCurrentState != FGTStateManager.ToolsState.InCreative)
                    foreach (FallGuysCameraAvoidence fgca in Resources.FindObjectsOfTypeAll<FallGuysCameraAvoidence>())
                        fgca.gameObject.SetActive(true);
                else
                    Resources.FindObjectsOfTypeAll<PlayerCameraController>()[1].PlayerCamera.gameObject.SetActive(true);
                GPCams.gameObject.SetActive(true);
                CAM = null;
                enterFreeCam = false;
                if (StateManager.FGTCurrentState != FGTStateManager.ToolsState.InCreative)
                {
                    UIM.SwitchToState(InGameUiManager.InGameState.Playing);
                    RewiredManager.Instance.EnableMap(0, 0);
                    FGTServiceManager.Instance.GetService<SpeedrunService>().TriggerTimer(true);
                }

                if (FreeCamAudioEffect.Value && FMODTool.TryGetEventInstance("SFX_TimeAttack_Snapshot_TimeStop", out var evt))
                    evt.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }
        }

        public void EnterFC()
        {
            if (GPCams == null)
            {
                if (StateManager.FGTCurrentState != FGTStateManager.ToolsState.InCreative)
                    GPCams = Resources.FindObjectsOfTypeAll<FallGuysCameraAvoidence>().FirstOrDefault();
                else
                    GPCams = Resources.FindObjectsOfTypeAll<wle.LevelEditorCameraAvoidance>().FirstOrDefault().Cast<FallGuysCameraAvoidence>();
            }
            if (GPCams != null && !fetchedDist)
            {
                if (GPCams.CurrentDistance != 0f)
                    maxDistDef = GPCams.CurrentDistance;
                else
                    maxDistDef = GPCams._maxDistance;

                currentDist = GPCams.CurrentDistance;
                fetchedDist = true;
            }
            CAM?.transform.position = lastCamPos;
            if (StateManager.FGTCurrentState != FGTStateManager.ToolsState.InCreative)
            {
                UIM.SwitchToState(InGameUiManager.InGameState.Banners);
                RewiredManager.Instance.DisableMap(0, 0);
                FGTServiceManager.Instance.GetService<SpeedrunService>().TriggerTimer(false);
            }

            if (FreeCamAudioEffect.Value && FMODTool.TryGetEventInstance("SFX_TimeAttack_Snapshot_TimeStop", out var evt))
                evt.start();
        }

        void OnGUI()
        {
            if (StateManager.FGCurrentState == PlayerState.FreeCam)
                FreeCAMGUI();
        }

        public void FreeCAMGUI()
        {
            if (!displayUI)
                return;

            float offsetX = Screen.width - 210f;
            float offsetY = 25f;
            float saveBtnPosY = 135f;

            GUI.Box(new Rect(offsetX, offsetY, 200f, fcBoxPos), "");
            GUI.Box(new Rect(offsetX, offsetY, 200f, 25), "");
            GUI.Label(new Rect(offsetX + 5f, offsetY + 3f, 255f, 20f), $"{LocalizedStr("gui_fc_settings")}");
            GUI.Label(new Rect(offsetX + 65f, offsetY + 25f, 255f, 20f), $"{LocalizedStr("gui_move_speed")}");
            string FCMoveSpeed = FreeCamSpeed.Value.ToString();
            FCMoveSpeed = GUI.TextField(new Rect(offsetX + 5f, offsetY + 25f, 55f, 20f), FCMoveSpeed);
            if (float.TryParse(FCMoveSpeed, out float num))
                FreeCamSpeed.Value = num;
            GUI.Label(new Rect(offsetX + 65f, offsetY + 45f, 255f, 20f), $"{LocalizedStr("gui_zoom_speed")}");
            string FCZoomSpeed = FreeCamZoomSpeed.Value.ToString();
            FCZoomSpeed = GUI.TextField(new Rect(offsetX + 5f, offsetY + 45f, 55f, 20f), FCZoomSpeed);
            if (float.TryParse(FCZoomSpeed, out float num2))
                FreeCamZoomSpeed.Value = num2;
            GUI.Label(new Rect(offsetX + 65f, offsetY + 65f, 255f, 20f), $"{LocalizedStr("gui_sensivity")}");
            string FCSensivitySpeed = FreeCamSens.Value.ToString();
            FCSensivitySpeed = GUI.TextField(new Rect(offsetX + 5f, offsetY + 65f, 55f, 20f), FCSensivitySpeed);
            if (float.TryParse(FCSensivitySpeed, out float num1))
                FreeCamSens.Value = num1;
            GUI.Label(new Rect(offsetX + 5f, offsetY + 85f, 255f, 20f), $"{LocalizedStr("gui_stats")}");
            GUI.Label(new Rect(offsetX + 5f, offsetY + 100f, 255f, 20f), $"{LocalizedStr("gui_fc_pos")} - {CAM.transform.position}");
            GUI.Label(new Rect(offsetX + 5f, offsetY + 115f, 255f, 20f), $"{LocalizedStr("gui_fov")} - {CAM.GetComponent<Camera>().fieldOfView}");
            if (freecamPause)
            {
                saveBtnPosY = 155f;
                fcBoxPos = 220f;
                GUI.Label(new Rect(offsetX + 5f, offsetY + 135f, 255f, 20f), $"{LocalizedStr("gui_fc_pause")}");
            }
            else
                fcBoxPos = 200f;
            GUI.Label(new Rect(offsetX + 5f, saveBtnPosY + 30f, 190f, 100f), $"<size=10>{LocalizedStr("gui_fc_hotkeys", [ToggleFreeCamHotkey.Value, PauseFreeCamHotkey.Value, FreeCamMoveUpHotkey.Value, FreeCamMoveDownHotkey.Value, FreeCamToggleUI.Value])}</size>");
        }
    }
}
