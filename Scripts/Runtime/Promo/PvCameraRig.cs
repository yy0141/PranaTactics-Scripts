#if UNITY_EDITOR || PV_CAPTURE
using UnityEngine;
using UnityEngine.Rendering.Universal;

using PranaTactics.Unit;

namespace PranaTactics.Promo
{
    /// <summary>
    /// カメラのカット、補間移動、対象追従と手動操作を担当します。
    /// </summary>
    public sealed class PvCameraRig : MonoBehaviour
    {
        private Camera _camera;

        private PvCue _cue;

        private PvActor _target;

        private Vector3 _fromPosition;

        private Quaternion _fromRotation;

        private float _fromFieldOfView;

        private bool _isFree;

        private PvActor[] _targets = System.Array.Empty<PvActor>();

        private float _shakeStarted = float.NegativeInfinity;

        private Camera _firstPerson;

        private Camera _orbitDestination;

        private float _firstPersonHeight;

        private float _orbitDestinationHeight;

        private float _firstPersonTurnStarted;

        private float _firstPersonTurnDuration;

        private bool _isOrbit;

        private bool _usesPresentationTime;

        private float _presentationTime;

        private float _orbitStarted;

        private float _orbitDuration;

        private Vector3 _orbitCenter;

        private Vector3 _orbitStart;

        private float _orbitRadius;

        public Camera Camera => _camera;

        public void Initialize(Camera source)
        {
            _camera = gameObject.AddComponent<Camera>();

            if (source != null)
            {
                _camera.CopyFrom(source);
            }

            _camera.targetTexture = null;
            _camera.depth = 100;
            _camera.enabled = true;
            UniversalAdditionalCameraData data = gameObject.AddComponent<UniversalAdditionalCameraData>();

            if (source != null && source.TryGetComponent(out UniversalAdditionalCameraData sourceData))
            {
                data.renderPostProcessing = sourceData.renderPostProcessing;
                data.antialiasing = sourceData.antialiasing;
                data.volumeLayerMask = sourceData.volumeLayerMask;
            }

            gameObject.AddComponent<AudioListener>();
        }

        public void SetShot(PvCue cue, PvActor target, PvActor[] targets)
        {
            _firstPerson = null;
            _isOrbit = false;
            _usesPresentationTime = false;
            _cue = cue;
            _target = target;
            _targets = targets;
            _shakeStarted = float.NegativeInfinity;
            _fromPosition = transform.position;
            _fromRotation = transform.rotation;
            _fromFieldOfView = _camera.fieldOfView;
        }

        public void SetFirstPerson(
            PvActor actor,
            float turnDuration = 0,
            float impactShake = 0,
            float heightOffset = 0)
        {
            _firstPersonHeight = heightOffset;
            _fromRotation = transform.rotation;
            _firstPersonTurnStarted = _presentationTime;
            _firstPersonTurnDuration = turnDuration;
            _firstPerson = actor.Unit.MicroUnitBehavior.UnitCamera;
            _isOrbit = false;
            _target = actor;
            _targets = System.Array.Empty<PvActor>();
            _cue = new PvCue { ImpactShake = impactShake };
            _shakeStarted = float.NegativeInfinity;
        }

        /// <summary>
        /// 両者の中点を中心に半周し、終盤は相手の一人称カメラへ滑らかに収束します。
        /// </summary>
        public void StartOrbit(PvActor first, PvActor second, float duration, float destinationHeight = 0)
        {
            _orbitDestinationHeight = destinationHeight;
            _firstPerson = null;
            _orbitDestination = second.Unit.MicroUnitBehavior.UnitCamera;
            _isOrbit = true;
            _orbitStarted = _presentationTime;
            _orbitDuration = duration;
            _fromPosition = transform.position;
            _fromRotation = transform.rotation;
            _fromFieldOfView = _camera.fieldOfView;
            _orbitCenter = (first.Position + second.Position) * 0.5f + Vector3.up * 2;
            _orbitStart = Vector3.ProjectOnPlane(first.Position - second.Position, Vector3.up).normalized;
            float halfAngle = Mathf.Min(
                60 * Mathf.Deg2Rad * 0.5f,
                Mathf.Atan(
                Mathf.Tan(
                60 * Mathf.Deg2Rad * 0.5f) * _camera.aspect));

            _orbitRadius = (Vector3.Distance(first.Position, second.Position) * 0.5f + 8) / Mathf.Sin(
                halfAngle);
        }

        public void SetPresentationShot(PvCue cue, PvActor[] targets)
        {
            SetShot(cue, null, targets);
            _usesPresentationTime = true;
            cue.Time = _presentationTime;
        }

        /// <summary>
        /// 通常のMicroからMacroへの遷移と同じ向き・補間で、指定した割合の高度まで上昇させます。
        /// </summary>
        public void StartOverhead(PvActor actor, Transform fullMapCamera, float duration, float heightRatio)
        {
            Camera macroCamera = actor.Unit.GetMacroUnitBehavior().UnitCamera;
            Vector3 position = transform.position;

            position.y = Mathf.Lerp(position.y, fullMapCamera.position.y, heightRatio);
            Vector3 angles = macroCamera.transform.eulerAngles;

            _camera.farClipPlane += Mathf.Abs(position.y - transform.position.y);
            SetPresentationShot(new PvCue
            {
                Duration = duration,
                Position = new PvPosition { X = position.x, Y = position.y, Z = position.z },
                Pitch = angles.x,
                Yaw = angles.y,
                FieldOfView = _camera.fieldOfView
            }, System.Array.Empty<PvActor>());
        }

        public void ToggleFree()
        {
            _isFree = !_isFree;
        }

        /// <summary>
        /// 現在のカットに映る演者の実際の被弾で、短いカメラ揺れを開始します。
        /// </summary>
        public void ReactToDamage(UnitBehavior unit, float time)
        {
            if (_cue == null || _cue.ImpactShake <= 0)
            {
                return;
            }

            bool isFramed = _target != null && _target.Unit == unit;

            foreach (PvActor actor in _targets)
            {
                isFramed |= actor.Unit == unit;
            }

            if (isFramed)
            {
                _shakeStarted = time;
            }
        }

        public void Tick(float time, float presentationDelta = 0)
        {
            _presentationTime += presentationDelta;

            if (_isFree)
            {
                UpdateFreeCamera();

                return;
            }

            if (_firstPerson != null)
            {
                float turnProgress = _firstPersonTurnDuration <= 0 ? 1 :
                    Mathf.SmoothStep(
                        0,
                        1,
                        Mathf.Clamp01(
                        (_presentationTime - _firstPersonTurnStarted) / _firstPersonTurnDuration));

                transform.SetPositionAndRotation(_firstPerson.transform.position +
                    Vector3.up * _firstPersonHeight,
                    Quaternion.Slerp(_fromRotation, _firstPerson.transform.rotation, turnProgress));
                _camera.fieldOfView = _firstPerson.fieldOfView;
                ApplyImpactShake(time);

                return;
            }

            if (_isOrbit)
            {
                float orbitProgress = Mathf.Clamp01((_presentationTime - _orbitStarted) / _orbitDuration);
                float angle = Mathf.SmoothStep(0, 180, orbitProgress);
                Vector3 radial = Quaternion.AngleAxis(angle, Vector3.up) * _orbitStart;
                Vector3 position = _orbitCenter + radial * _orbitRadius + Vector3.up * 12;
                float pullback = Mathf.SmoothStep(0, 1, Mathf.Clamp01(orbitProgress / 0.25f));

                position = Vector3.Lerp(_fromPosition, position, pullback);
                Quaternion orbitRotation = Quaternion.LookRotation(_orbitCenter - position);

                orbitRotation = Quaternion.Slerp(_fromRotation, orbitRotation, pullback);
                float arrival = Mathf.SmoothStep(0, 1, Mathf.Clamp01((orbitProgress - 0.65f) / 0.35f));

                position = Vector3.Lerp(
                    position,
                    _orbitDestination.transform.position +
                    Vector3.up * _orbitDestinationHeight,
                    arrival);
                orbitRotation = Quaternion.Slerp(orbitRotation, _orbitDestination.transform.rotation, arrival);
                transform.SetPositionAndRotation(position, orbitRotation);
                _camera.fieldOfView = Mathf.Lerp(
                    Mathf.Lerp(_fromFieldOfView, 60, pullback),
                    _orbitDestination.fieldOfView,
                    arrival);

                return;
            }

            if (_cue == null)
            {
                return;
            }

            float shotTime = _usesPresentationTime ? _presentationTime : time;
            float progress = _cue.Duration <= 0 ? 1 : Mathf.Clamp01((shotTime - _cue.Time) / _cue.Duration);

            progress = Mathf.SmoothStep(0, 1, progress);
            Vector3 destination = new Vector3(_cue.Position.X, _cue.Position.Y, _cue.Position.Z) +
                (_target == null ? Vector3.zero : _target.Position);
            Quaternion rotation = Quaternion.Euler(_cue.Pitch, _cue.Yaw, 0);

            if (_target != null)
            {
                Vector3 direction = _target.Position + Vector3.up * 1.5f - destination;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    rotation = Quaternion.LookRotation(direction);
                }
            }

            if (_targets.Length > 0)
            {
                var bounds = new Bounds(_targets[0].Position, Vector3.one * 8);

                foreach (PvActor actor in _targets)
                {
                    bounds.Encapsulate(new Bounds(actor.Position, Vector3.one * 8));
                }

                Vector3 offset = new Vector3(_cue.Position.X, _cue.Position.Y, _cue.Position.Z);
                float verticalHalfAngle = _cue.FieldOfView * Mathf.Deg2Rad * 0.5f;
                float horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * _camera.aspect);
                float distance = bounds.extents.magnitude * 1.1f / Mathf.Sin(
                    Mathf.Min(
                    verticalHalfAngle,
                    horizontalHalfAngle));

                destination = bounds.center + offset.normalized * Mathf.Max(offset.magnitude, distance);
                rotation = Quaternion.LookRotation(bounds.center - destination);
            }

            transform.SetPositionAndRotation(
                Vector3.Lerp(_fromPosition, destination, progress),
                Quaternion.Slerp(
                _fromRotation,
                rotation,
                progress));
            _camera.fieldOfView = Mathf.Lerp(_fromFieldOfView, _cue.FieldOfView, progress);
            ApplyImpactShake(time);
        }

        /// <summary>
        /// 通常カットと一人称追従の両方に、実際の被弾による揺れを重ねます。
        /// </summary>
        private void ApplyImpactShake(float time)
        {
            float elapsed = time - _shakeStarted;

            if (_cue != null && elapsed >= 0 && elapsed < 0.22f)
            {
                float strength = _cue.ImpactShake * Mathf.Pow(1 - elapsed / 0.22f, 2);

                transform.position += transform.right * (Mathf.Sin(elapsed * 95) * strength) +
                    transform.up * (Mathf.Sin(elapsed * 73) * strength * 0.5f);
                transform.rotation *= Quaternion.Euler(0, 0, Mathf.Sin(elapsed * 81) * strength);
            }
        }

        private void UpdateFreeCamera()
        {
            if (!Input.GetMouseButton(1))
            {
                return;
            }

            Vector3 angles = transform.eulerAngles;

            angles.x -= Input.GetAxisRaw("Mouse Y") * 2;
            angles.y += Input.GetAxisRaw("Mouse X") * 2;
            transform.rotation = Quaternion.Euler(angles.x, angles.y, 0);
            var movement = new Vector3(
                (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0),
                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            float speed = Input.GetKey(KeyCode.LeftShift) ? 80 : 20;

            transform.Translate(movement.normalized * (speed * Time.unscaledDeltaTime), Space.Self);
            _camera.fieldOfView = Mathf.Clamp(_camera.fieldOfView - Input.mouseScrollDelta.y * 2, 10, 120);
        }
    }
}
#endif
