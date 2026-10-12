using UnityEngine;

namespace PranaTactics.Unit
{
    /// <summary>
    /// 全体マップカメラの高度とFPS視点の間を双方向に補間し、終了時に元の設定を復元します。
    /// </summary>
    public sealed class MicroPhaseCameraTransition
    {
        private readonly Camera _camera;

        private readonly Vector3 _targetLocalPosition;

        private readonly Quaternion _targetLocalRotation;

        private readonly Vector3 _startPosition;

        private readonly Quaternion _startRotation;

        private readonly float _originalFarClip;

        private readonly bool _isAscending;

        public MicroPhaseCameraTransition(UnitBehaviorBase macroUnit, MicroUnitBehavior microUnit,
    Transform fullMapCamera, bool isAscending = false)
        {
            _isAscending = isAscending;

            if (microUnit == null || microUnit.UnitCamera == null)
            {
                return;
            }

            _camera = microUnit.UnitCamera;
            Transform cameraTransform = _camera.transform;

            _targetLocalPosition = cameraTransform.localPosition;
            _targetLocalRotation = cameraTransform.localRotation;
            _originalFarClip = _camera.farClipPlane;
            Camera macroCamera = macroUnit != null ? macroUnit.UnitCamera : null;
            float startHeight = fullMapCamera != null
                ? fullMapCamera.position.y : cameraTransform.position.y;

            _startPosition = new Vector3(cameraTransform.position.x,
                startHeight,
                cameraTransform.position.z);
            _startRotation = macroCamera != null ? macroCamera.transform.rotation : cameraTransform.rotation;
            _camera.farClipPlane = _originalFarClip + Mathf.Abs(startHeight - cameraTransform.position.y);
            Apply(0.0f);

            if (macroUnit != null)
            {
                macroUnit.SwitchCamera(false);
            }

            microUnit.SwitchCamera(true);
        }

        /// <summary>
        /// 正規化された進行度を使い、開始と終了で減速する位置・回転補間を適用します。
        /// </summary>
        public void Apply(float progress)
        {
            if (_camera == null)
            {
                return;
            }

            Transform cameraTransform = _camera.transform;
            Transform parent = cameraTransform.parent;
            Vector3 targetPosition = parent != null
                ? parent.TransformPoint(_targetLocalPosition) : _targetLocalPosition;
            Quaternion targetRotation = parent != null
                ? parent.rotation * _targetLocalRotation : _targetLocalRotation;
            float blend = Mathf.SmoothStep(0.0f, 1.0f, _isAscending ? 1.0f - progress : progress);

            cameraTransform.SetPositionAndRotation(
                Vector3.Lerp(_startPosition, targetPosition, blend),
                Quaternion.Slerp(_startRotation, targetRotation, blend));
        }

        /// <summary>
        /// 完了・中断のどちらでもFPSカメラのローカル姿勢と描画距離を復元します。
        /// </summary>
        public void Restore()
        {
            if (_camera == null)
            {
                return;
            }

            _camera.transform.localPosition = _targetLocalPosition;
            _camera.transform.localRotation = _targetLocalRotation;
            _camera.farClipPlane = _originalFarClip;
        }
    }
}
