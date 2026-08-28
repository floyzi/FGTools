using BepInEx.Unity.IL2CPP.Utils.Collections;
using DG.Tweening;
using FGTools.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static FGTools.Services.CosmeticsService;

namespace FGTools.Internal.Behaviours
{
    internal class CosmeticSearchBar : ToolsBehaviour
    {
        CosmeticsService Service;
        Coroutine Delay;
        UICustomTMP_InputField InputField;
        DebugDisplayService DebugService;
        void Awake()
        {
            Service = FGTServiceManager.GetService<CosmeticsService>();
            DebugService = FGTServiceManager.GetService<DebugDisplayService>();

            InputField = GetComponentInChildren<UICustomTMP_InputField>();
            var tween = InputField.GetComponent<TweenOnTMP_InputField>();

            //too lazy to figure out why it does not work properly natively (
            //this works just fine so why even bother
            tween.OnMouseHover = new Action<bool>((s) =>
            {
                tween._toggleOn.localScale = Vector3.one;

                tween._inactiveToggleCanvasGroup.DOSafeFade(s ? 0 : 1, tween._alphaInTime);
                tween._activeToggleCanvasGroup.DOSafeFade(s ? 1 : 0, tween._alphaInTime);

                if (s)
                {
                    AudioManager.PlayOneShot(tween._onSelectAudio);
                    tween._toggleOn.DOPunchScale(tween._toSize, tween._bounceTime, tween._bounceNumber, tween._bouncePower);

                    InputField.Select();
                    InputField.ActivateInputField();
                    InputField.caretWidth = 1;
                    DebugService.CanTriggerDebug = false;

                    Service.SearchStart();
                }
                else
                {
                    InputField.DeactivateInputField();
                    InputField.caretWidth = 0;
                    DebugService.CanTriggerDebug = true;

                    Service.SearchEnd(false);
                }
            });

            InputField.onEndEdit.AddListener(new Action<string>((s) =>
            {
                DebugService.CanTriggerDebug = true;
                Service.SearchEnd(false);
            }));

            InputField.onSelect.AddListener(new Action<string>((s) =>
            {
                DebugService.CanTriggerDebug = false;
                Service.SearchStart();
            }));

            InputField.onValueChanged.AddListener(new Action<string>((s) => 
            {
                if (Delay != null) CoroutineRunner.Instance.StopCoroutine(Delay);
                Delay = CoroutineRunner.Instance.StartCoroutine(DelayedSearch(s).WrapToIl2Cpp());
            }));
        }

        IEnumerator DelayedSearch(string s)
        {
            yield return new WaitForSeconds(0.35f);
            Service.Search(s, Service.GetSection(), RequestType.Locker, Config.Config.AllCosmetics.Value);
            Delay = null;
        }

        void OnDisable()
        {
            Reset();
        }

        void OnEnable()
        {
            Reset();
        }

        void Reset()
        {
            DebugService.CanTriggerDebug = true;
            InputField.text = "";
        }
    }
}
