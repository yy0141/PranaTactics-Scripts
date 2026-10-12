using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using PranaTactics.Controls;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// キー欄の空欄・カーソル表示と入力待ちを管理し、キャンセル時は保存値を維持します。
    /// </summary>
    public sealed class SettingsKeyCapture : IDisposable
    {
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();

        private Button _button;

        private GameObject _caret;

        private Action<string> _accepted;

        private Action _refresh;

        private EventSystem _eventSystem;

        private bool _wasNavigationEnabled;

        private int _startedFrame;

        private int _finishedFrame = -1;

        private float _startedTime;

        public bool IsCapturing => _accepted != null;

        public bool IsEditing(Button button)
        {
            return IsCapturing && _button == button;
        }

        public void Begin(
            Button button,
            TMP_Text label,
            GameObject caret,
            Action refresh,
            Action<string> accepted)
        {
            if (IsEditing(button))
            {
                return;
            }

            Cancel();
            RestoreNavigation();
            _eventSystem = EventSystem.current;

            if (_eventSystem != null)
            {
                _wasNavigationEnabled = _eventSystem.sendNavigationEvents;
                _eventSystem.sendNavigationEvents = false;
                _eventSystem.SetSelectedGameObject(button.gameObject);
            }

            _button = button;
            _caret = caret;
            _refresh = refresh;
            _accepted = accepted;
            _startedFrame = Time.frameCount;
            _startedTime = Time.unscaledTime;
            label.text = "";
            _caret.SetActive(true);
        }

        /// <summary>
        /// Enter・欄外クリック・フォーカス喪失はキャンセルし、それ以外のキーを確定します。
        /// 起動操作と確定操作が別のUI操作を起こさないようフレーム単位で消費します。
        /// </summary>
        public bool Poll()
        {
            if (!IsCapturing)
            {
                if (Time.frameCount > _finishedFrame)
                {
                    RestoreNavigation();
                }

                return Time.frameCount == _finishedFrame;
            }

            if (!_button.gameObject.activeInHierarchy || !_button.IsInteractable() || !Application.isFocused)
            {
                Cancel();

                return true;
            }

            _caret.SetActive((Time.unscaledTime - _startedTime) % 1.0f < 0.5f);

            if (Time.frameCount <= _startedFrame)
            {
                return true;
            }

            if (IsOutsidePointerPress() || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(
                KeyCode.KeypadEnter))
            {
                Cancel();

                return true;
            }

            if (KeyboardBindingInput.TryGetPressed(out string key))
            {
                Action<string> accepted = _accepted;

                Cancel();
                accepted(key);
            }

            return true;
        }

        private bool IsOutsidePointerPress()
        {
            if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
            {
                return false;
            }

            if (EventSystem.current == null)
            {
                return true;
            }

            var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };

            _hits.Clear();
            EventSystem.current.RaycastAll(pointer, _hits);

            return _hits.Count == 0 || !_hits[0].gameObject.transform.IsChildOf(_button.transform);
        }

        public void Cancel()
        {
            if (!IsCapturing)
            {
                return;
            }

            _finishedFrame = Time.frameCount;
            Action refresh = _refresh;

            if (_caret != null)
            {
                _caret.SetActive(false);
            }

            if (_eventSystem != null && _button != null &&
                _eventSystem.currentSelectedGameObject == _button.gameObject)
            {
                _eventSystem.SetSelectedGameObject(null);
            }

            _accepted = null;
            _refresh = null;
            _button = null;
            _caret = null;
            refresh?.Invoke();
        }

        private void RestoreNavigation()
        {
            if (_eventSystem != null)
            {
                _eventSystem.sendNavigationEvents = _wasNavigationEnabled;
                _eventSystem = null;
            }
        }

        public void Dispose()
        {
            Cancel();
            RestoreNavigation();
        }
    }
}
