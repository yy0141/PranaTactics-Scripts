using UnityEngine;

using PranaTactics.Match.Phases;
using PranaTactics.Pranam.Networking;
using PranaTactics.Pranam.Targeting;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 狙撃型プラーナムに共通する初期化処理、
    /// 照準処理および狙撃弾の生成処理を提供します。
    /// </summary>
    public abstract class SniperBase : PranamBase
    {
        /// <summary>
        /// 照準中の移動ロックを再通知する秒数です。
        /// </summary>
        private const float k_AimMovementLockInterval = 0.1f;

        /// <summary>
        /// 通知が途絶えた照準ロックを自動解除する秒数です。
        /// </summary>
        private const float k_AimMovementLockDuration = 0.2f;

        /// <summary>
        /// 遠隔の移動裁定側へ照準中のロックを再通知する時刻です。
        /// </summary>
        private float _nextAimMovementLockTime;

        /// <summary>
        /// 照準中の移動ロックを既存の通信経路で更新します。
        /// </summary>
        /// <remarks>
        /// 通知が途絶えた場合は短時間で解除され、永続ロックを残しません。
        /// 発射後は兵装の硬直時間による移動ロックへ引き継ぎます。
        /// </remarks>
        private void SynchronizeAimMovementLock()
        {
            if (Time.time < _nextAimMovementLockTime)
            {
                return;
            }

            _nextAimMovementLockTime = Time.time + k_AimMovementLockInterval;
            _unitBehavior.LockMovement(k_AimMovementLockDuration);
        }

        protected override IAttackTargetSelection TargetSelection => NearestAttackTargetSelection.Instance;

        /// <summary>
        /// 照準中は今回の発射先として固定した対象を共有します。
        /// </summary>
        protected override bool TryGetCommittedAttackTarget(out UnitBehavior targetUnit)
        {
            targetUnit = _lockedTarget;
            return _isAiming;
        }

        /// <summary>
        /// カメラを取得できなかった場合に使用する
        /// 標準の垂直視野角です。
        /// </summary>
        private const float k_DefaultCameraFieldOfView = 60.0f;

        /// <summary>
        /// スナイパー共通のズーム加速度です。
        /// </summary>
        /// <remarks>
        /// 単位は度毎秒毎秒です。
        /// 値を大きくすると、
        /// より短時間でズームが完了します。
        /// </remarks>
        private const float k_ZoomAcceleration = 200.0f;

        /// <summary>
        /// 発射後のズーム維持時間を決定する倍率です。
        /// </summary>
        /// <remarks>
        /// ズーム維持時間は以下の計算で決まります。
        /// <c>
        /// RecoveryTime * k_ZoomHoldDurationMultiplier
        /// </c>
        /// </remarks>
        private const float k_ZoomHoldDurationMultiplier = 0.2f;

        /// <summary>
        /// 狙撃型プラーナムの設定データです。
        /// </summary>
        private SniperPranamData _pranamData;

        /// <summary>
        /// 所有ユニットのミクロ・フェイズ用コンポーネントです。
        /// </summary>
        private MicroUnitBehavior _microUnitBehavior;

        /// <summary>
        /// 通常のゲーム画面を描画するカメラです。
        /// </summary>
        private Camera _camera;

        /// <summary>
        /// 今回の狙撃処理で固定している対象です。
        /// </summary>
        private UnitBehavior _lockedTarget;

        /// <summary>
        /// スナイパーの照準表示を管理するコンポーネントです。
        /// </summary>
        private SniperScopeController _scopeController;

        /// <summary>
        /// ズーム前のカメラの垂直視野角です。
        /// </summary>
        private float _baseCameraFieldOfView =
            k_DefaultCameraFieldOfView;

        /// <summary>
        /// 現在照準処理を実行しているかどうかを示します。
        /// </summary>
        private bool _isAiming;

        /// <summary>
        /// 現在のズーム速度です。
        /// </summary>
        private float _currentZoomSpeed;

        /// <summary>
        /// 現在の照準用視野角です。
        /// </summary>
        private float _currentZoomFieldOfView;

        /// <summary>
        /// このプラーナムが現在ズーム表示を
        /// 制御しているかどうかを示します。
        /// </summary>
        private bool _isControllingCameraZoom;

        /// <summary>
        /// 発射後に最大ズーム状態を
        /// 維持しているかどうかを示します。
        /// </summary>
        private bool _isHoldingZoom;

        /// <summary>
        /// 発射後の最大ズーム状態を
        /// 維持した経過時間です。
        /// </summary>
        private float _zoomHoldTimer;

        /// <summary>
        /// スコープ表示終了後、
        /// Pranamの硬直終了を待っているかどうかです。
        /// </summary>
        private bool _isWaitingRecoveryUnlock;

        protected SniperPranamData SniperData
        {
            get
            {
                return _pranamData;
            }
        }

        protected MicroUnitBehavior MicroUnitBehavior
        {
            get
            {
                return _microUnitBehavior;
            }
        }

        protected Camera SniperCamera
        {
            get
            {
                return _camera;
            }
        }

        /// <summary>
        /// 狙撃型プラーナムを初期化します。
        /// </summary>
        public override void Initialize(
            int playerId,
            bool isMain,
            int number,
            UnitBehavior unitBehavior,
            PranamBehavior pranamBehavior,
            PranamData pranamData,
            int startAngle)
        {
            _isInitialized = false;

            if (unitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(SniperBase)}: " +
                    $"{nameof(UnitBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(SniperBase)}: " +
                    $"{nameof(PranamBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamData is not SniperPranamData sniperPranamData)
            {
                Debug.LogError(
                    $"{nameof(SniperBase)}: " +
                    $"{nameof(SniperPranamData)}以外のデータが渡されました。" +
                    $" Data={pranamData?.GetType().Name ?? "null"}",
                    this);

                return;
            }

            TacticalMapUnitBehavior mapUnitBehavior =
                unitBehavior.GetTacticalMapUnitBehavior();

            MicroUnitBehavior microUnitBehavior =
                unitBehavior.GetMicroUnitBehavior();

            if (mapUnitBehavior == null ||
                microUnitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(SniperBase)}: " +
                    "初期化に必要なユニットコンポーネントを取得できません。" +
                    $" PlayerId={playerId}",
                    unitBehavior);

                return;
            }

            _playerId = playerId;
            _isMain = isMain;
            _number = number;

            _unitBehavior = unitBehavior;
            _mapUnitBehavior = mapUnitBehavior;
            _microUnitBehavior = microUnitBehavior;

            _pranamBehavior = pranamBehavior;
            _pranamData = sniperPranamData;

            InitializeCamera(
                unitBehavior);

            _scopeController =
                _microUnitBehavior.GetComponentInChildren
                    <SniperScopeController>(
                        true);

            if (_scopeController != null)
            {
                _scopeController.Initialize(
                    _camera);
            }
            else if (IsLocalPlayerOwner())
            {
                Debug.LogWarning(
                    $"{nameof(SniperBase)}: " +
                    $"{nameof(SniperScopeController)}を取得できません。" +
                    $" PlayerId={playerId}",
                    unitBehavior);
            }

            ResetAimingState();

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager != null &&
                unitManager.CanExecuteUnitLogic(playerId))
            {
                CreateArc(
                    gameObject,
                    startAngle,
                    _pranamData.Angle,
                    _pranamData.Diameter);
            }

            _time =
                _pranamData.RecoveryTime;

            _isInitialized = true;
        }

        /// <summary>
        /// カメラ関連の参照を初期化します。
        /// </summary>
        /// <param name="unitBehavior">
        /// 所有ユニットです。
        /// </param>
        private void InitializeCamera(
            UnitBehavior unitBehavior)
        {
            _camera =
                _microUnitBehavior.GetCamera();

            if (_camera != null)
            {
                _baseCameraFieldOfView =
                    _camera.fieldOfView;

                return;
            }

            _baseCameraFieldOfView =
                k_DefaultCameraFieldOfView;

            Debug.LogWarning(
                $"{nameof(SniperBase)}: " +
                "ズーム処理に使用するカメラを取得できません。" +
                $" PlayerId={_playerId}",
                unitBehavior);
        }

        /// <summary>
        /// コンポーネントが再度有効になったとき、
        /// 使用間隔と照準状態を初期化します。
        /// </summary>
        protected override void OnEnableInternal()
        {
            base.OnEnableInternal();

            ResetAimingState();

            if (_pranamData == null)
            {
                return;
            }

            _time =
                _pranamData.RecoveryTime;
        }

        /// <summary>
        /// コンポーネントが無効になったとき、
        /// 照準状態とカメラのズームを解除します。
        /// </summary>
        protected override void OnDisableInternal()
        {
            CancelAiming();

            base.OnDisableInternal();
        }

        /// <summary>
        /// プラーナムの終了時に照準状態と
        /// カメラのズームを解除します。
        /// </summary>
        public override void Exit()
        {
            CancelAiming();

            base.Exit();
        }

        /// <summary>
        /// 対象ユニットの表示モデル中央座標を取得します。
        /// </summary>
        protected static bool TryGetTargetCenterPosition(
            UnitBehavior targetUnit,
            out Vector3 targetPosition)
        {
            targetPosition =
                Vector3.zero;

            if (targetUnit == null)
            {
                return false;
            }

            MicroUnitBehavior targetMicroUnit =
                targetUnit.GetMicroUnitBehavior();

            if (targetMicroUnit == null)
            {
                return false;
            }

            Renderer[] renderers =
                targetMicroUnit.GetComponentsInChildren<Renderer>(
                    false);

            bool hasBounds =
                false;

            Bounds combinedBounds =
                default;

            foreach (Renderer targetRenderer in
                    renderers)
            {
                if (targetRenderer == null ||
                    !targetRenderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds =
                        targetRenderer.bounds;

                    hasBounds =
                        true;

                    continue;
                }

                combinedBounds.Encapsulate(
                    targetRenderer.bounds);
            }

            targetPosition =
                hasBounds
                    ? combinedBounds.center
                    : targetMicroUnit.transform.position;

            return true;
        }

        /// <summary>
        /// スナイパーの照準を開始します。
        /// </summary>
        /// <remarks>
        /// この時点では弾を生成しません。
        /// 視野角が
        /// <see cref="SniperPranamData.ZoomFieldOfView"/>
        /// へ到達した後に発射します。
        /// </remarks>
        public sealed override void UsePranam()
        {
            if (!CanStartAiming(
                    out UnitBehavior targetUnit))
            {
                return;
            }

            if (!_microUnitBehavior.TryBeginSniperAimLock(
                    this,
                    _isMain))
            {
                return;
            }

            _lockedTarget =
                targetUnit;

            ResetControlledCameraZoom();

            _isAiming =
                true;

            _nextAimMovementLockTime = 0.0f;
            SynchronizeAimMovementLock();

            _currentZoomSpeed =
                0.0f;

            _currentZoomFieldOfView =
                _baseCameraFieldOfView;

            _isHoldingZoom =
                false;

            _zoomHoldTimer =
                0.0f;

            if (IsLocalPlayerOwner())
            {
                bool visualStarted =
                    BeginScopeVisual();

                if (!visualStarted)
                {
                    CancelAiming();

                    return;
                }
            }
        }

        /// <summary>
        /// 狙撃型プラーナムの対象検出処理と
        /// 照準処理を更新します。
        /// </summary>
        protected override void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null ||
                !unitManager.CanExecuteUnitLogic(
                    _playerId))
            {
                CancelAiming();

                return;
            }

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null ||
                phaseManager.CurrentPhase != MatchPhase.Micro)
            {
                CancelAiming();

                return;
            }

            base.Update();

            if (_isHoldingZoom)
            {
                UpdateZoomHold();
            }

            if (_isWaitingRecoveryUnlock)
            {
                UpdateRecoveryUnlock();
            }

            if (!_isAiming)
            {
                return;
            }

            SynchronizeAimMovementLock();
            UpdateAiming();
        }

        /// <summary>
        /// 今回の狙撃対象を取得します。
        /// </summary>
        protected abstract bool TryAcquireSniperTarget(
            out UnitBehavior targetUnit);

        /// <summary>
        /// 固定した対象へ狙撃弾を発射します。
        /// </summary>
        protected abstract bool FireSniperShot(
            UnitBehavior targetUnit);

        /// <summary>
        /// 現在の状態で照準を開始できるか判定します。
        /// </summary>
        private bool CanStartAiming(
            out UnitBehavior targetUnit)
        {
            targetUnit =
                null;

            if (!_isInitialized ||
                _isAiming ||
                _isHoldingZoom ||
                _isWaitingRecoveryUnlock ||
                _pranamData == null ||
                _microUnitBehavior == null ||
                _pranamBehavior == null)
            {
                return false;
            }

            if (!_pranamBehavior.CanUsePranam(
                    _pranamData.ActionPointCost,
                    _pranamData.PranaCost))
            {
                return false;
            }

            if (!_microUnitBehavior.CanBeginSniperAim(
                    this,
                    _isMain))
            {
                return false;
            }

            return TryAcquireSniperTarget(
                out targetUnit);
        }

        /// <summary>
        /// スナイパーの照準状態を更新します。
        /// </summary>
        private void UpdateAiming()
        {
            if (!TryGetTargetCenterPosition(
                    _lockedTarget,
                    out Vector3 targetPosition))
            {
                CancelAiming();

                return;
            }

            bool isAimAligned =
                _microUnitBehavior.UpdateSniperAutoAim(
                    this,
                    targetPosition);

            float deltaTime =
                Time.deltaTime;

            if (deltaTime <= 0.0f)
            {
                return;
            }

            _currentZoomSpeed +=
                k_ZoomAcceleration *
                deltaTime;

            float targetFieldOfView =
                GetTargetFieldOfView();

            _currentZoomFieldOfView =
                Mathf.MoveTowards(
                    _currentZoomFieldOfView,
                    targetFieldOfView,
                    _currentZoomSpeed *
                    deltaTime);

            UpdateCameraZoom(
                _currentZoomFieldOfView);

            bool isZoomCompleted =
                Mathf.Approximately(
                    _currentZoomFieldOfView,
                    targetFieldOfView);

            if (!isZoomCompleted ||
                !isAimAligned)
            {
                return;
            }

            _currentZoomFieldOfView =
                targetFieldOfView;

            _currentZoomSpeed =
                0.0f;

            _isAiming =
                false;

            bool wasFired =
                FireSniperShot(
                    _lockedTarget);

            if (!wasFired)
            {
                CancelAiming();

                return;
            }

            _isHoldingZoom =
                true;

            _zoomHoldTimer =
                0.0f;
        }

        /// <summary>
        /// 発射後の最大ズーム状態を更新します。
        /// </summary>
        private void UpdateZoomHold()
        {
            if (_microUnitBehavior == null)
            {
                CancelAiming();

                return;
            }

            UpdateCameraZoom(
                GetTargetFieldOfView());

            _zoomHoldTimer +=
                Time.deltaTime;

            float zoomHoldDuration =
                _pranamData.RecoveryTime *
                k_ZoomHoldDurationMultiplier;

            if (_zoomHoldTimer <
                zoomHoldDuration)
            {
                return;
            }

            _isHoldingZoom =
                false;

            _zoomHoldTimer =
                0.0f;

            ResetControlledCameraZoom();

            _microUnitBehavior.ReleaseSniperViewLock(
                this);

            _isWaitingRecoveryUnlock =
                true;
        }

        /// <summary>
        /// スナイパー発射後の硬直終了を監視し、
        /// 視点操作と武器切替のロックを解除します。
        /// </summary>
        private void UpdateRecoveryUnlock()
        {
            if (!_isWaitingRecoveryUnlock)
            {
                return;
            }

            if (_pranamBehavior != null &&
                _pranamBehavior.IsRecovering)
            {
                return;
            }

            _isWaitingRecoveryUnlock =
                false;

            _microUnitBehavior?.ReleaseSniperAimLock(
                this);

            _lockedTarget =
                null;
        }

        /// <summary>
        /// 現在のスナイパーで使用する
        /// 目標視野角を取得します。
        /// </summary>
        private float GetTargetFieldOfView()
        {
            if (_pranamData == null)
            {
                return _baseCameraFieldOfView;
            }

            return Mathf.Clamp(
                _pranamData.ZoomFieldOfView,
                1.0f,
                _baseCameraFieldOfView);
        }

        /// <summary>
        /// 照準開始時のUI表示を開始します。
        /// </summary>
        private bool BeginScopeVisual()
        {
            if (!IsLocalPlayerOwner() ||
                _microUnitBehavior == null ||
                _pranamData == null ||
                _pranamData.ScopeProfile == null ||
                _scopeController == null)
            {
                return false;
            }

            if (_microUnitBehavior.IsMainPranamPerspective !=
                _isMain)
            {
                return false;
            }

            bool wasStarted =
                _scopeController.BeginAim(
                    _pranamData.ScopeProfile,
                    _baseCameraFieldOfView);

            if (!wasStarted)
            {
                return false;
            }

            _isControllingCameraZoom =
                true;

            return true;
        }

        /// <summary>
        /// 現在の照準視野角を
        /// ローカルプレイヤーの表示へ反映します。
        /// </summary>
        private void UpdateCameraZoom(
            float fieldOfView)
        {
            if (_microUnitBehavior == null ||
                !IsLocalPlayerOwner())
            {
                return;
            }

            bool isCurrentPerspective =
                _microUnitBehavior.IsMainPranamPerspective ==
                _isMain;

            if (!isCurrentPerspective)
            {
                ResetControlledCameraZoom();

                return;
            }

            if (_scopeController != null &&
                _scopeController.IsAiming)
            {
                _scopeController.SetFieldOfView(
                    fieldOfView);

                _isControllingCameraZoom =
                    true;

                return;
            }

            if (_camera == null)
            {
                return;
            }

            _camera.fieldOfView =
                fieldOfView;

            _isControllingCameraZoom =
                true;
        }

        /// <summary>
        /// 現在の照準処理を中断します。
        /// </summary>
        private void CancelAiming()
        {
            ResetAimingState();

            ResetControlledCameraZoom();

            _microUnitBehavior?.ReleaseSniperAimLock(
                this);

            _lockedTarget =
                null;
        }

        /// <summary>
        /// 照準処理に使用する内部状態を初期化します。
        /// </summary>
        private void ResetAimingState()
        {
            _isAiming = false;

            _currentZoomSpeed =
                0.0f;

            _currentZoomFieldOfView =
                _baseCameraFieldOfView;

            _isHoldingZoom = false;

            _zoomHoldTimer =
                0.0f;

            _isWaitingRecoveryUnlock =
                false;
        }

        /// <summary>
        /// このスナイパーによる照準表示と
        /// カメラのズームを解除します。
        /// </summary>
        private void ResetControlledCameraZoom()
        {
            if (!_isControllingCameraZoom)
            {
                return;
            }

            if (IsLocalPlayerOwner())
            {
                if (_scopeController != null &&
                    _pranamData != null &&
                    _pranamData.ScopeProfile != null)
                {
                    _scopeController.EndAim();
                }
                else if (_camera != null)
                {
                    _camera.fieldOfView =
                        _baseCameraFieldOfView;
                }
            }

            _isControllingCameraZoom =
                false;
        }

        /// <summary>
        /// このプラーナムがローカルプレイヤーによって
        /// 所有されているかどうかを判定します。
        /// </summary>
        private bool IsLocalPlayerOwner()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            return unitManager != null &&
                _playerId ==
                unitManager.ControlledUnitId;
        }

        /// <summary>
        /// 狙撃用のプラーナオブジェクトを生成します。
        /// </summary>
        protected void CreateAttackObject(
            int playerId,
            Vector3 instantiatePosition,
            Quaternion instantiateRotation,
            int targetPlayerId)
        {
            if (!_isInitialized ||
                _pranamBehavior == null ||
                _pranamData == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    _pranamData.PrefabResourcePath))
            {
                Debug.LogError(
                    $"{nameof(SniperBase)}: " +
                    "生成するプラーナのResourcesパスが設定されていません。" +
                    $" Pranam={_pranamData.DisplayName}",
                    _pranamData);

                return;
            }

            PranamContext context =
                new()
                {
                    PlayerId = playerId,
                    IsMain = _isMain,
                    PranamNumber = _number,
                    TargetPlayerId = targetPlayerId,

                    GeneratePosition =
                        instantiatePosition,

                    DefinitionId = _pranamData.name
                };

            _pranamBehavior.CreatePranaObject(
                _pranamData.PrefabResourcePath,
                instantiatePosition,
                instantiateRotation,
                context);
        }
    }
}
