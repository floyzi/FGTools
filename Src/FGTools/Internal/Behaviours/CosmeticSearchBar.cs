using BepInEx.Unity.IL2CPP.Utils.Collections;
using DG.Tweening;
using FGClient.Customiser;
using FGTools.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using static FGTools.Services.CosmeticsService;

namespace FGTools.Internal.Behaviours
{
    internal class CosmeticSearchBar : ToolsBehaviour
    {
        CosmeticsService _service;
        Coroutine _delay;
        UICustomTMP_InputField _inputField;
        DebugDisplayService _debugService;
        Vector2 _initPos;
        RectTransform _rectTransform;
        CanvasGroup _group;
        void Awake()
        {
            _group = gameObject.AddComponent<CanvasGroup>();

            _rectTransform = GetComponent<RectTransform>();
            _initPos = _rectTransform.anchoredPosition;

            _service = FGTServiceManager.GetService<CosmeticsService>();
            _debugService = FGTServiceManager.GetService<DebugDisplayService>();

            _inputField = GetComponentInChildren<UICustomTMP_InputField>();
            var tween = _inputField.GetComponent<TweenOnTMP_InputField>();

            Reset();

            //too lazy to figure out why it does not work properly native way
            //this works just fine so why even bother
            tween.OnMouseHover = new Action<bool>((s) =>
            {
                tween._toggleOn.DOKill();
                tween._toggleOn.localScale = Vector3.one;

                tween._inactiveToggleCanvasGroup.DOSafeFade(s ? 0 : 1, tween._alphaInTime);
                tween._activeToggleCanvasGroup.DOSafeFade(s ? 1 : 0, tween._alphaInTime);

                if (s)
                {
                    AudioManager.PlayOneShot(tween._onSelectAudio);

                    tween._toggleOn.DOPunchScale(tween._toSize, tween._bounceTime, tween._bounceNumber, tween._bouncePower);

                    _inputField.Select();
                    _inputField.ActivateInputField();
                    _inputField.caretWidth = 1;
                    _debugService.CanTriggerDebug = false;

                    _service.SearchStart();
                }
                else
                {
                    _inputField.DeactivateInputField();
                    _inputField.caretWidth = 0;
                    _debugService.CanTriggerDebug = true;

                    _service.SearchEnd();
                }
            });

            _inputField.onEndEdit.AddListener(new Action<string>((s) =>
            {
                _debugService.CanTriggerDebug = true;
                _service.SearchEnd();
            }));

            _inputField.onSelect.AddListener(new Action<string>((s) =>
            {
                _debugService.CanTriggerDebug = false;
                _service.SearchStart();
            }));

            _inputField.onValueChanged.AddListener(new Action<string>((s) => 
            {
                _service.InputString = s;
                if (_delay != null) CoroutineRunner.Instance.StopCoroutine(_delay);
                _delay = CoroutineRunner.Instance.StartCoroutine(DelayedSearch(s).WrapToIl2Cpp());
            }));
        }

        IEnumerator DelayedSearch(string s)
        {
            yield return new WaitForSeconds(0.35f);
            _service.Search(s, _service.CurrentSection);
            _delay = null;
        }

        void OnDisable()
        {
            Reset();
        }

        void OnEnable()
        {
            Reset();

            if (!Config.Config.ShowSearchInLocker.Value)
            {
                _group.interactable = false;
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
            }
            else
            {
                _group.interactable = true;
                _group.alpha = 1f;
                _group.blocksRaycasts = true;
            }

            _rectTransform.anchoredPosition = _initPos + Vector2.up * 80f;
            _rectTransform.DOAnchorPos(new(_rectTransform.anchoredPosition.x, _initPos.y), 0.2f);
        }

        void Reset()
        {
            _debugService.CanTriggerDebug = true;
            _inputField.text = "";
            _service.InputString = "";
            _rectTransform.DOKill();
        }
    }
}
