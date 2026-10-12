using UnityEngine;

using PranaTactics.Board;
using PranaTactics.Controls;
using PranaTactics.Match.Phases;
using PranaTactics.Settings;
using PranaTactics.UI.Screens;

namespace PranaTactics.Unit
{
    /// <summary>
    /// Macroカメラのズーム、右ドラッグによる平行移動と位置リセットを管理します。
    /// </summary>
    /// <remarks>
    /// 入力回数はUIなどで遮断されなかったスクロールのみを数えます。
    /// チュートリアルはページ開始時との差分を使い、LateUpdateで受理した入力を次の更新で確認します。
    /// </remarks>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class MacroCameraZoom : MonoBehaviour
    {
        private const float k_TransitionSpeed = 1.5f;

        private MacroUnitBehavior _owner;

        private Camera _camera;

        private Vector3 _initialLocalPosition;

        private float _initialOrthographicSize;

        private float _initialFarClip;

        private float _targetZoom;

        private float _zoom;

        private bool _wasActive;

        private readonly MacroCameraDragGesture _dragGesture = new MacroCameraDragGesture();
        private Vector3 _panOffset;
        private Vector3 _previousPointerPosition;

        public bool IsPointerGestureActive => _dragGesture.IsPressed;
        public bool WasTargetClickReleased => _dragGesture.WasClickReleased;

        /// <summary>
        /// 操作ユニットのマクロカメラだけ入力を受け付けます。
        /// </summary>
        private bool IsActiveCamera()
        {
            return _owner != null && _camera != null && _camera.gameObject.activeInHierarchy &&
                _owner.GetUnitStatus() != null && UnitManager.Instance != null &&
                UnitManager.Instance.ControlledUnitId == _owner.GetUnitStatus().PlayerId &&
                MatchPhaseManager.Instance != null && MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Macro;
        }

        /// <summary>
        /// UI、確認画面とチュートリアルの遮断中はカメラ入力を抑止します。
        /// </summary>
        private bool CanInteract()
        {
            return !PranaTactics.Tutorial.TutorialSession.BlocksInput &&
                (SettingsUIManager.Instance == null || !SettingsUIManager.Instance.IsOpen) &&
                MatchPhaseManager.Instance?.MacroPlanning?.IsPlanningConfirmationOpen != true &&
                InputRaycastUtility.GetUIRaycastResults().Count == 0;
        }

        /// <summary>
        /// 経路操作より前に右クリックを判定し、ドラッグ時だけ視点移動量を更新します。
        /// </summary>
        private void Update()
        {
            if (!IsActiveCamera())
            {
                _dragGesture.Cancel();
                return;
            }

            bool canInteract = CanInteract();
            if (canInteract && KeyboardBindingInput.IsPressed(SettingsManager.Current.Controls.MacroViewResetKey))
            {
                ResetViewPosition();
                ViewResetInputCount++;
                return;
            }

            Vector3 pointer = Input.mousePosition;
            bool pressed = Input.GetMouseButtonDown(1);
            _dragGesture.Update(pressed, Input.GetMouseButton(1), Input.GetMouseButtonUp(1),
                pointer.x, pointer.y, canInteract);
            if (pressed)
            {
                _previousPointerPosition = pointer;
            }

            if (canInteract && (_dragGesture.IsDragging || _dragGesture.WasDragReleased))
            {
                ApplyDrag(_previousPointerPosition, pointer);
                _previousPointerPosition = pointer;
            }
        }

        /// <summary>
        /// 同じ盤面平面に投影したマウス位置の差分だけ、盤面をつかむ方向へカメラを動かします。
        /// </summary>
        private void ApplyDrag(Vector3 previousPointer, Vector3 pointer)
        {
            if (BoardManager.Instance == null || !BoardManager.Instance.TryGetMacroCameraBounds(out Bounds bounds))
            {
                return;
            }

            var plane = new Plane(Vector3.up, new Vector3(0, bounds.max.y, 0));
            Ray previousRay = _camera.ScreenPointToRay(previousPointer);
            Ray currentRay = _camera.ScreenPointToRay(pointer);
            if (!plane.Raycast(previousRay, out float previousDistance) ||
                !plane.Raycast(currentRay, out float currentDistance))
            {
                return;
            }

            Vector3 delta = previousRay.GetPoint(previousDistance) - currentRay.GetPoint(currentDistance);
            Vector3 position = _camera.transform.position + delta;
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            delta = position - _camera.transform.position;
            delta.y = 0.0f;
            _panOffset += delta;
            if (delta.sqrMagnitude > 0.0001f)
            {
                PanInputCount++;
            }
        }

        /// <summary>
        /// ズーム倍率を保ったまま、視点を通常の追従位置へ戻します。
        /// </summary>
        public void ResetViewPosition()
        {
            _panOffset = Vector3.zero;
            _dragGesture.Cancel();
        }

        public int PanInputCount { get; private set; }
        public int ViewResetInputCount { get; private set; }

        public int ZoomOutInputCount
        {
            get; private set;
        }

        public int ZoomInInputCount
        {
            get; private set;
        }

        /// <summary>
        /// スクロールの方向と感度を現在の設定から反映し、操作回数を更新します。
        /// </summary>
        private void ApplyScrollInput(float rawScroll, ControlsSettings controls)
        {
            float scroll = controls.ApplyScrollDirection(rawScroll);
            _targetZoom = MacroCameraZoomMath.ApplyScroll(_targetZoom, scroll, controls.ScrollSensitivity);
            if (scroll < 0.0f)
            {
                ZoomOutInputCount++;
            }
            else if (scroll > 0.0f)
            {
                ZoomInInputCount++;
            }
        }

        /// <summary>
        /// カメラ生成時の位置を保存し、初期高度をズームの下限にします。
        /// </summary>
        public void Initialize(MacroUnitBehavior owner, Camera camera)
        {
            if (_camera != null)
            {
                return;
            }

            _owner = owner;
            _camera = camera;
            _initialLocalPosition = camera.transform.localPosition;
            _initialOrthographicSize = camera.orthographicSize;
            _initialFarClip = camera.farClipPlane;
        }

        /// <summary>
        /// ユニット移動後にカメラを更新し、引くほど盤面中央への追従率を高めます。
        /// </summary>
        private void LateUpdate()
        {
            bool isActive = IsActiveCamera();

            if (!isActive)
            {
                if (_wasActive)
                {
                    RestoreCamera();
                }

                return;
            }

            _wasActive = true;

            if (BoardManager.Instance == null ||
                !BoardManager.Instance.TryGetMacroCameraBounds(
                out Bounds bounds))
            {
                return;
            }

            if (CanInteract() && InputManager.Instance != null)
            {
                ApplyScrollInput(
                    InputManager.Instance.CurrentState.MouseScrollDelta,
                    SettingsManager.Current.Controls);
            }

            _zoom = Mathf.MoveTowards(_zoom, _targetZoom, k_TransitionSpeed * Time.unscaledDeltaTime);

            Vector3 initialPosition = _camera.transform.parent != null
                ? _camera.transform.parent.TransformPoint(_initialLocalPosition)
                : _initialLocalPosition;
            Vector3 unitPosition = _owner.transform.position;
            Vector3 closePosition = new Vector3(unitPosition.x, initialPosition.y, unitPosition.z);
            Vector3 right = _camera.transform.right;
            Vector3 up = _camera.transform.up;
            float width = 2.0f * (Mathf.Abs(right.x) * bounds.extents.x +
                Mathf.Abs(right.z) * bounds.extents.z);
            float depth = 2.0f * (Mathf.Abs(up.x) * bounds.extents.x + Mathf.Abs(up.z) * bounds.extents.z);
            float height = MacroCameraZoomMath.CalculateFitHeight(
                width,
                depth,
                _camera.fieldOfView,
                _camera.aspect,
                closePosition.y - bounds.max.y);
            Vector3 farPosition = new Vector3(bounds.center.x, bounds.max.y + height, bounds.center.z);

            Vector3 defaultPosition = Vector3.Lerp(closePosition, farPosition, _zoom);
            Vector3 position = defaultPosition + _panOffset;
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            _panOffset = position - defaultPosition;
            _camera.transform.position = position;

            if (_camera.orthographic)
            {
                float size = Mathf.Max(
                    _initialOrthographicSize,
                    Mathf.Max(
                    depth,
                    width / Mathf.Max(
                    0.01f,
                    _camera.aspect)) * 0.54f);

                _camera.orthographicSize = Mathf.Lerp(_initialOrthographicSize, size, _zoom);
            }

            _camera.farClipPlane = Mathf.Max(
                _initialFarClip,
                _camera.transform.position.y - bounds.min.y +
                10.0f);
        }

        /// <summary>
        /// ズームを保持し、平行移動を解除して非操作中のカメラ設定を復元します。
        /// </summary>
        private void RestoreCamera()
        {
            if (_camera != null)
            {
                _camera.transform.localPosition = _initialLocalPosition;
                _camera.orthographicSize = _initialOrthographicSize;
                _camera.farClipPlane = _initialFarClip;
            }

            _panOffset = Vector3.zero;
            _dragGesture.Cancel();
            _wasActive = false;
        }

        /// <summary>
        /// 無効化時もズームを保持したままカメラ設定を復元します。
        /// </summary>
        private void OnDisable()
        {
            RestoreCamera();
        }
    }
}
