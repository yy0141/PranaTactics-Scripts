using System;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Match;
using PranaTactics.UI.Gameplay;

namespace PranaTactics.Unit
{
    /// <summary>
    /// ユニットの表示モデル、カメラ、座標および
    /// 基本的な移動処理を提供する基底クラスです。
    /// </summary>
    /// <remarks>
    /// 派生クラスは、ボード座標に対応する位置とスケールを
    /// <see cref="GetMoveCoordinatePosition"/>および
    /// <see cref="GetMoveCoordinateScale"/>で提供します。
    ///
    /// ネットワーク同期はこのクラスで直接行わず、
    /// <see cref="UnitBehavior"/>やネットワーク抽象層を通して行います。
    /// </remarks>
    [DisallowMultipleComponent]
    public abstract class UnitBehaviorBase : MonoBehaviour
    {
        /// <summary>
        /// 移動先へ到着したと判定する距離です。
        /// </summary>
        private const float k_ArrivalThreshold = 0.05f;

        /// <summary>
        /// ボードの高さスケールから表示位置を計算する際の除数です。
        /// </summary>
        private const float k_BoardHeightDivisor = 5.0f;

        /// <summary>
        /// ダッシュ移動速度へ適用する倍率です。
        /// </summary>
        private const float k_DirectMoveSpeedMultiplier = 2.0f;

        /// <summary>
        /// 速度計算時に0除算を防ぐための最小時間です。
        /// </summary>
        private const float k_MinimumDeltaTime = 0.000001f;

        /// <summary>
        /// ユニットの表示モデルです。
        /// </summary>
        [Header("References")]
        [FormerlySerializedAs("MODEL")]
        [SerializeField]
        private GameObject _model;

        /// <summary>
        /// ユニットに割り当てられたカメラです。
        /// </summary>
        [FormerlySerializedAs("CAMERA")]
        [SerializeField]
        private Camera _unitCamera;

        /// <summary>
        /// この動作コンポーネントを所有する
        /// ユニット全体の制御コンポーネントです。
        /// </summary>
        protected UnitBehavior _unitBehavior;

        /// <summary>
        /// ユニットの現在のステータスです。
        /// </summary>
        protected UnitStatus _unitStatus;

        /// <summary>
        /// 通常移動時に消費する移動コストです。
        /// </summary>
        protected int _movementCost = 1;

        /// <summary>
        /// ユニットが現在存在するボード座標です。
        /// </summary>
        protected Vector2Int _currentCoordinate;

        /// <summary>
        /// ユニットが次に移動するボード座標です。
        /// </summary>
        protected Vector2Int _nextCoordinate =
            Vector2Int.zero;

        /// <summary>
        /// ボード座標への移動で使用する目標位置です。
        /// </summary>
        protected Vector3 _towardPosition =
            Vector3.zero;

        /// <summary>
        /// 座標に依存しない直接移動を実行中かどうかです。
        /// </summary>
        protected bool _isDirectMove;

        /// <summary>
        /// 直接移動の目標位置です。
        /// </summary>
        protected Vector3 _directTargetPosition;

        /// <summary>
        /// 直前の移動から計算されたユニットの速度です。
        /// </summary>
        protected Vector3 _velocity;

        /// <summary>
        /// 移動ロックが解除されるまでの残り時間です。
        /// </summary>
        private float _movementLockTimer;

        /// <summary>
        /// ユニットの移動がロックされているかどうかです。
        /// </summary>
        private bool _isMovementLocked;

        public GameObject Model
        {
            get
            {
                return _model;
            }
        }

        public Camera UnitCamera
        {
            get
            {
                return _unitCamera;
            }
        }

        public UnitBehavior UnitBehavior
        {
            get
            {
                return _unitBehavior;
            }
        }

        public UnitStatus UnitStatus
        {
            get
            {
                return _unitStatus;
            }
        }

        public Vector2Int CurrentCoordinate
        {
            get
            {
                return _currentCoordinate;
            }
        }

        public Vector2Int NextCoordinate
        {
            get
            {
                return _nextCoordinate;
            }
        }

        public int MovementCost
        {
            get
            {
                return _movementCost;
            }
        }

        public Vector3 Velocity
        {
            get
            {
                return _velocity;
            }
        }

        public bool IsDirectMoving
        {
            get
            {
                return _isDirectMove;
            }
        }

        public virtual bool IsMovementLocked
        {
            get
            {
                return _isMovementLocked;
            }
        }

        [Obsolete(
    "Use Model instead.")]
        protected GameObject MODEL
        {
            get
            {
                return _model;
            }

            set
            {
                _model = value;
            }
        }

        [Obsolete(
    "Use UnitCamera instead.")]
        protected Camera CAMERA
        {
            get
            {
                return _unitCamera;
            }

            set
            {
                _unitCamera = value;
            }
        }

        [Obsolete(
    "Use MovementCost or _movementCost instead.")]
        protected int MOVEMENT_COST
        {
            get
            {
                return _movementCost;
            }

            set
            {
                _movementCost =
                    Mathf.Max(
                        0,
                        value);
            }
        }

        /// <summary>
        /// ユニット動作を初期化します。
        /// </summary>
        /// <remarks>
        /// 派生クラスで追加の初期化が必要な場合にオーバーライドします。
        /// </remarks>
        public virtual void Initialize()
        {
        }

        /// <summary>
        /// 移動ロックの残り時間を更新します。
        /// </summary>
        /// <remarks>
        /// 派生クラスで<see cref="Update"/>をオーバーライドする場合は、
        /// 必ず<c>base.Update()</c>を呼び出してください。
        /// </remarks>
        protected virtual void Update()
        {
            UpdateMovementLock(
                Time.deltaTime);
        }

        /// <summary>
        /// ユニットの表示モデルを取得します。
        /// </summary>
        /// <returns>
        /// ユニットの表示モデルです。
        /// </returns>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// 新しいコードでは<see cref="Model"/>を使用できます。
        /// </remarks>
        public GameObject GetModel()
        {
            return Model;
        }

        /// <summary>
        /// このコンポーネントが属するゲームオブジェクトを取得します。
        /// </summary>
        /// <returns>
        /// このコンポーネントが属するゲームオブジェクトです。
        /// </returns>
        public GameObject GetGameObject()
        {
            return gameObject;
        }

        /// <summary>
        /// ユニットの移動がロックされているかどうかを取得します。
        /// </summary>
        /// <returns>
        /// 移動がロックされている場合は<c>true</c>です。
        /// </returns>
        /// <remarks>
        /// 新しいコードでは<see cref="IsMovementLocked"/>を使用できます。
        /// </remarks>
        public bool GetIsMovementLocked()
        {
            return IsMovementLocked;
        }

        /// <summary>
        /// ユニットの移動がロックされているかどうかを取得します。
        /// </summary>
        /// <returns>
        /// 移動がロックされている場合は<c>true</c>です。
        /// </returns>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// </remarks>
        public bool IsMovementLockedState()
        {
            return IsMovementLocked;
        }

        /// <summary>
        /// 指定された時間だけユニットの移動をロックします。
        /// </summary>
        /// <param name="duration">
        /// 移動をロックする秒数です。
        /// </param>
        public void LockMovement(
            float duration)
        {
            if (duration <= 0.0f)
            {
                return;
            }

            _movementLockTimer =
                Mathf.Max(
                    _movementLockTimer,
                    duration);

            _isMovementLocked =
                true;
        }

        /// <summary>
        /// ユニットの移動ロックを即座に解除します。
        /// </summary>
        public void UnlockMovement()
        {
            _movementLockTimer =
                0.0f;

            _isMovementLocked =
                false;
        }

        /// <summary>
        /// ユニットの現在の移動速度を設定します。
        /// </summary>
        /// <param name="value">
        /// 設定する移動速度です。
        /// </param>
        public void SetMoveSpeed(
            float value)
        {
            if (_unitStatus == null)
            {
                LogMissingUnitStatus(
                    nameof(SetMoveSpeed));

                return;
            }

            _unitStatus.SetCurrentMoveSpeed(
                Mathf.Max(
                    0.0f,
                    value));
        }

        /// <summary>
        /// このコンポーネントを所有する
        /// UnitBehaviorを設定します。
        /// </summary>
        /// <param name="unitBehavior">
        /// 設定するUnitBehaviorです。
        /// </param>
        public void SetUnitBehavior(
            UnitBehavior unitBehavior)
        {
            _unitBehavior =
                unitBehavior;
        }

        /// <summary>
        /// このコンポーネントを所有する
        /// UnitBehaviorを取得します。
        /// </summary>
        /// <returns>
        /// 設定されているUnitBehaviorです。
        /// </returns>
        public UnitBehavior GetUnitBehavior()
        {
            return UnitBehavior;
        }

        /// <summary>
        /// ユニットのステータスを設定します。
        /// </summary>
        /// <param name="unitStatus">
        /// 設定するユニットステータスです。
        /// </param>
        public void SetUnitStatus(
            UnitStatus unitStatus)
        {
            _unitStatus =
                unitStatus;
        }

        /// <summary>
        /// ユニットのステータスを取得します。
        /// </summary>
        /// <returns>
        /// 設定されているユニットステータスです。
        /// </returns>
        public UnitStatus GetUnitStatus()
        {
            return UnitStatus;
        }

        /// <summary>
        /// ユニットの現在座標を取得します。
        /// </summary>
        /// <returns>
        /// ユニットの現在座標です。
        /// </returns>
        public virtual Vector2Int GetCurrentCoordinate()
        {
            return CurrentCoordinate;
        }

        /// <summary>
        /// ユニットの現在座標を設定します。
        /// </summary>
        /// <param name="x">
        /// X座標です。
        /// </param>
        /// <param name="y">
        /// Y座標です。
        /// </param>
        public void SetCurrentCoordinate(
            int x,
            int y)
        {
            SetCurrentCoordinate(
                new Vector2Int(
                    x,
                    y));
        }

        /// <summary>
        /// ユニットの現在座標を設定します。
        /// </summary>
        /// <param name="coordinate">
        /// 設定する座標です。
        /// </param>
        public void SetCurrentCoordinate(
            Vector2Int coordinate)
        {
            _currentCoordinate =
                coordinate;
        }

        /// <summary>
        /// ローカルプレイヤーが所有するユニットの場合だけ
        /// ユニットカメラを有効にします。
        /// </summary>
        public void LocalCameraOn()
        {
            bool isLocalPlayer =
                IsOwnedByLocalPlayer();

            SwitchCamera(
                isLocalPlayer);
        }

        /// <summary>
        /// ユニットの積極性を指定された値へ設定します。
        /// </summary>
        /// <param name="value">
        /// 設定する積極性です。
        /// </param>
        public void SetProactiveness(
            int value)
        {
            if (_unitStatus == null)
            {
                LogMissingUnitStatus(
                    nameof(SetProactiveness));

                return;
            }

            _unitStatus.SetProactiveness(
                value);

            RefreshLocalProactivenessUi();
        }

        /// <summary>
        /// ユニットの積極性から指定された値を消費します。
        /// </summary>
        /// <param name="value">
        /// 消費する積極性です。
        /// </param>
        public void CalcProactiveness(
            int value)
        {
            GameRuleManager gameRuleManager =
                GameRuleManager.Instance;

            if (gameRuleManager == null ||
                !gameRuleManager
                    .CurrentRule
                    .UseActionPoint)
            {
                return;
            }

            if (_unitStatus == null)
            {
                LogMissingUnitStatus(
                    nameof(CalcProactiveness));

                return;
            }

            int newProactiveness =
                _unitStatus.Proactiveness -
                value;

            _unitStatus.SetProactiveness(
                newProactiveness);

            RefreshLocalProactivenessUi();
        }

        /// <summary>
        /// 指定されたボード座標へ即座に移動します。
        /// </summary>
        /// <param name="x">
        /// 移動先のX座標です。
        /// </param>
        /// <param name="y">
        /// 移動先のY座標です。
        /// </param>
        public virtual void TeleportMoveToCoordinate(
            int x,
            int y)
        {
            if (!TryCalculateMoveTargetPosition(
                    x,
                    y,
                    out Vector3 targetPosition))
            {
                return;
            }

            _towardPosition =
                targetPosition;

            TeleportMoveStart();

            if (Vector3.Distance(
                    transform.position,
                    _towardPosition) >
                k_ArrivalThreshold)
            {
                transform.position =
                    _towardPosition;
            }

            TeleportMoveEnd(
                new Vector2Int(
                    x,
                    y));
        }

        /// <summary>
        /// 指定されたボード座標への補間移動を開始します。
        /// </summary>
        /// <param name="x">
        /// 移動先のX座標です。
        /// </param>
        /// <param name="y">
        /// 移動先のY座標です。
        /// </param>
        public virtual void LerpMoveToCoordinate(
            int x,
            int y)
        {
            if (!TryCalculateMoveTargetPosition(
                    x,
                    y,
                    out Vector3 targetPosition))
            {
                return;
            }

            _towardPosition =
                targetPosition;

            LerpMoveStart();
        }

        /// <summary>
        /// 指定されたワールド座標への直接移動を開始します。
        /// </summary>
        /// <param name="targetPosition">
        /// 直接移動の目標位置です。
        /// </param>
        public void LerpMoveToPosition(
            Vector3 targetPosition)
        {
            _directTargetPosition =
                targetPosition;

            _isDirectMove =
                true;

            DirectMoveStart();
        }

        /// <summary>
        /// ユニットに割り当てられたカメラを取得します。
        /// </summary>
        /// <returns>
        /// ユニットのカメラです。
        /// </returns>
        public Camera GetCamera()
        {
            return UnitCamera;
        }

        /// <summary>
        /// ユニットカメラと、そのカメラに設定された
        /// AudioListenerの有効状態を切り替えます。
        /// </summary>
        /// <param name="isEnabled">
        /// カメラを有効にする場合は<c>true</c>です。
        /// </param>
        /// <remarks>
        /// 視認センサーを持つ論理カメラは、描画を停止してもGameObjectを維持します。
        /// AIの直接視認と兵装の対象取得を画面表示の有無に依存させません。
        /// </remarks>
        public void SwitchCamera(
            bool isEnabled)
        {
            if (_unitCamera == null)
            {
                return;
            }

            GameObject cameraObject =
                _unitCamera.gameObject;

            bool shouldKeepCameraObjectActive =
                isEnabled ||
                _unitCamera.GetComponent<VisionSensor>() != null;

            _unitCamera.enabled =
                isEnabled;

            if (cameraObject.activeSelf !=
                shouldKeepCameraObjectActive)
            {
                cameraObject.SetActive(
                    shouldKeepCameraObjectActive);
            }

            AudioListener audioListener =
                _unitCamera.GetComponent<AudioListener>();

            if (audioListener != null)
            {
                audioListener.enabled =
                    isEnabled;
            }
        }

        /// <summary>
        /// 指定されたボード座標に対応する
        /// ワールド位置を取得します。
        /// </summary>
        /// <param name="x">
        /// ボードのX座標です。
        /// </param>
        /// <param name="y">
        /// ボードのY座標です。
        /// </param>
        /// <returns>
        /// 指定された座標のワールド位置です。
        /// </returns>
        protected abstract Vector3 GetMoveCoordinatePosition(
            int x,
            int y);

        /// <summary>
        /// 指定されたボード座標に対応する
        /// マスのスケールを取得します。
        /// </summary>
        /// <param name="x">
        /// ボードのX座標です。
        /// </param>
        /// <param name="y">
        /// ボードのY座標です。
        /// </param>
        /// <returns>
        /// 指定された座標のマスのスケールです。
        /// </returns>
        protected abstract Vector3 GetMoveCoordinateScale(
            int x,
            int y);

        /// <summary>
        /// テレポート移動を開始する直前に呼び出されます。
        /// </summary>
        protected virtual void TeleportMoveStart()
        {
        }

        /// <summary>
        /// テレポート移動が完了した直後に呼び出されます。
        /// </summary>
        /// <param name="coordinate">
        /// 移動後のボード座標です。
        /// </param>
        protected virtual void TeleportMoveEnd(
            Vector2Int coordinate)
        {
        }

        /// <summary>
        /// ボード座標への補間移動を開始するときに呼び出されます。
        /// </summary>
        protected virtual void LerpMoveStart()
        {
        }

        /// <summary>
        /// ボード座標への補間移動が完了したときに呼び出されます。
        /// </summary>
        protected virtual void LerpMoveEnd()
        {
        }

        /// <summary>
        /// ワールド座標への直接移動を開始するときに呼び出されます。
        /// </summary>
        protected virtual void DirectMoveStart()
        {
        }

        /// <summary>
        /// ワールド座標への直接移動が完了したときに呼び出されます。
        /// </summary>
        protected virtual void DirectMoveEnd()
        {
        }

        /// <summary>
        /// 表現ごとの水平移動速度を取得します。
        /// </summary>
        protected virtual float GetHorizontalMoveSpeed()
        {
            return _unitStatus.CurrentMoveSpeed;
        }

        /// <summary>
        /// ユニットをボード座標の目標位置へ水平移動させます。
        /// </summary>
        protected virtual void UnitHorizontalMove()
        {
            if (_unitStatus == null ||
                !_unitStatus.IsMovable)
            {
                return;
            }

            Vector3 previousPosition =
                transform.position;

            transform.position =
                Vector3.MoveTowards(
                    previousPosition,
                    _towardPosition,
                    GetHorizontalMoveSpeed() *
                    Time.deltaTime);

            UpdateVelocity(
                previousPosition,
                transform.position,
                Time.deltaTime);

            if (Vector3.Distance(
                    transform.position,
                    _towardPosition) >
                k_ArrivalThreshold)
            {
                return;
            }

            transform.position =
                _towardPosition;

            _velocity =
                Vector3.zero;

            LerpMoveEnd();
        }

        /// <summary>
        /// ユニットを指定されたワールド位置へ直接移動させます。
        /// </summary>
        protected void DirectMove()
        {
            if (IsMovementLocked || !_isDirectMove ||
                _unitStatus == null)
            {
                return;
            }

            Vector3 previousPosition =
                transform.position;

            float moveSpeed =
                Mathf.Max(
                    0.0f,
                    _unitStatus.DashMoveSpeed) *
                k_DirectMoveSpeedMultiplier;

            transform.position =
                Vector3.MoveTowards(
                    previousPosition,
                    _directTargetPosition,
                    moveSpeed *
                    Time.deltaTime);

            UpdateVelocity(
                previousPosition,
                transform.position,
                Time.deltaTime);

            if (Vector3.Distance(
                    transform.position,
                    _directTargetPosition) >
                k_ArrivalThreshold)
            {
                return;
            }

            transform.position =
                _directTargetPosition;

            _velocity =
                Vector3.zero;

            _isDirectMove =
                false;

            DirectMoveEnd();
        }

        /// <summary>
        /// 指定されたボード座標から移動先の
        /// ワールド位置を計算します。
        /// </summary>
        /// <param name="x">
        /// ボードのX座標です。
        /// </param>
        /// <param name="y">
        /// ボードのY座標です。
        /// </param>
        /// <param name="targetPosition">
        /// 計算された移動先位置です。
        /// </param>
        /// <returns>
        /// 移動先位置を計算できた場合は<c>true</c>です。
        /// </returns>
        protected bool TryCalculateMoveTargetPosition(
            int x,
            int y,
            out Vector3 targetPosition)
        {
            targetPosition =
                transform.position;

            if (_model == null)
            {
                Debug.LogError(
                    $"{nameof(UnitBehaviorBase)}: " +
                    "ユニットモデルが設定されていないため、" +
                    "移動先の高さを計算できません。",
                    this);

                return false;
            }

            Vector3 spacePosition =
                GetMoveCoordinatePosition(
                    x,
                    y);

            Vector3 spaceScale =
                GetMoveCoordinateScale(
                    x,
                    y);

            targetPosition =
                new Vector3(
                    spacePosition.x,
                    spaceScale.y /
                    k_BoardHeightDivisor +
                    _model.transform.lossyScale.y,
                    spacePosition.z);

            return true;
        }

        /// <summary>
        /// 移動前後の位置からユニットの速度を計算します。
        /// </summary>
        /// <param name="previousPosition">
        /// 移動前の位置です。
        /// </param>
        /// <param name="currentPosition">
        /// 移動後の位置です。
        /// </param>
        /// <param name="deltaTime">
        /// 移動に使用した経過時間です。
        /// </param>
        private void UpdateVelocity(
            Vector3 previousPosition,
            Vector3 currentPosition,
            float deltaTime)
        {
            if (deltaTime <=
                k_MinimumDeltaTime)
            {
                _velocity =
                    Vector3.zero;

                return;
            }

            _velocity =
                (currentPosition -
                previousPosition) /
                deltaTime;
        }

        /// <summary>
        /// 移動ロックの残り時間を更新します。
        /// </summary>
        /// <param name="deltaTime">
        /// 前フレームからの経過時間です。
        /// </param>
        private void UpdateMovementLock(
            float deltaTime)
        {
            if (!_isMovementLocked)
            {
                return;
            }

            _movementLockTimer -=
                Mathf.Max(
                    0.0f,
                    deltaTime);

            if (_movementLockTimer >
                0.0f)
            {
                return;
            }

            UnlockMovement();
        }

        /// <summary>
        /// このユニットがローカルプレイヤーの所有かどうかを判定します。
        /// </summary>
        /// <returns>
        /// ローカルプレイヤーのユニットの場合は<c>true</c>です。
        /// </returns>
        private bool IsOwnedByLocalPlayer()
        {
            if (_unitStatus == null)
            {
                return false;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return false;
            }

            return _unitStatus.PlayerId ==
                unitManager.ControlledUnitId;
        }

        /// <summary>
        /// ローカルプレイヤーのユニットである場合に、
        /// HUDへ現在の積極性を反映します。
        /// </summary>
        private void RefreshLocalProactivenessUi()
        {
            if (!IsOwnedByLocalPlayer() ||
                _unitStatus == null)
            {
                return;
            }

            HUDManager hudManager =
                HUDManager.Instance;

            if (hudManager == null)
            {
                return;
            }

            hudManager.SetProactiveness(
                _unitStatus.Proactiveness);
        }

        /// <summary>
        /// UnitStatusが設定されていないことをログへ出力します。
        /// </summary>
        /// <param name="methodName">
        /// UnitStatusを必要としたメソッド名です。
        /// </param>
        private void LogMissingUnitStatus(
            string methodName)
        {
            Debug.LogWarning(
                $"{nameof(UnitBehaviorBase)}.{methodName}: " +
                $"{nameof(UnitStatus)}が設定されていません。",
                this);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定された参照と値を検証します。
        /// </summary>
        private void OnValidate()
        {
            _movementCost =
                Mathf.Max(
                    0,
                    _movementCost);
        }
#endif
    }
}
