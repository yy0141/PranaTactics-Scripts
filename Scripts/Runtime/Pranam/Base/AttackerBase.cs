using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Match.Phases;
using PranaTactics.Pranam.Networking;
using PranaTactics.Pranam.Targeting;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 近接攻撃型プラーナムに共通する、
    /// 対象への接近、攻撃および元の位置への帰還処理を管理します。
    /// </summary>
    public abstract class AttackerBase : PranamBase
    {
        protected override IAttackTargetSelection TargetSelection => NearestAttackTargetSelection.Instance;

        /// <summary>
        /// 接近から帰還まで、開始時に決定した近接攻撃対象を共有します。
        /// </summary>
        protected override bool TryGetCommittedAttackTarget(out UnitBehavior targetUnit)
        {
            targetUnit = _target != null ? _target.GetComponent<MicroUnitBehavior>()?.UnitBehavior : null;
            return _state != AttackState.Idle;
        }

        /// <summary>
        /// 攻撃位置へ到達したと判定する距離です。
        /// </summary>
        private const float k_ReachDistance = 0.1f;

        /// <summary>
        /// 攻撃対象へ接近するときの標準補間率です。
        /// </summary>
        private const float k_DefaultAttackPositionRatio = 0.75f;

        /// <summary>
        /// 方向ベクトルを有効と判断する最小二乗距離です。
        /// </summary>
        private const float k_MinimumDirectionSqrMagnitude = 0.0001f;

        /// <summary>
        /// 近接攻撃型プラーナムの設定データです。
        /// </summary>
        protected AttackerPranamData _pranamData;

        /// <summary>
        /// 所有ユニットのミクロ・フェイズ用コンポーネントです。
        /// </summary>
        protected MicroUnitBehavior _microUnitBehavior;

        /// <summary>
        /// 現在の攻撃対象です。
        /// </summary>
        protected GameObject _target;

        /// <summary>
        /// 攻撃開始前のユニット位置です。
        /// </summary>
        protected Vector3 _startPosition;

        /// <summary>
        /// 対象への攻撃を行う位置です。
        /// </summary>
        protected Vector3 _attackPosition;

        /// <summary>
        /// 現在の近接攻撃状態です。
        /// </summary>
        protected AttackState _state = AttackState.Idle;

        public bool IsAttackSequenceActive => _state != AttackState.Idle;

        protected enum AttackState
        {
            Idle,

            Moving,

            Attacking,

            Returning
        }

        protected AttackerPranamData AttackerData
        {
            get
            {
                return _pranamData;
            }
        }

        protected MicroUnitBehavior OwnerMicroUnit
        {
            get
            {
                return _microUnitBehavior;
            }
        }

        protected PranamBehavior OwnerPranamBehavior
        {
            get
            {
                return _pranamBehavior;
            }
        }

        protected int OwnerPlayerId
        {
            get
            {
                return _playerId;
            }
        }

        /// <summary>
        /// プラーナムの使用間隔を計測する経過時間を初期化します。
        /// </summary>
        protected void ResetElapsedTime()
        {
            _time = 0.0f;
        }

        /// <summary>
        /// 近接攻撃型プラーナムを初期化します。
        /// </summary>
        /// <param name="playerId">
        /// 所有プレイヤーのIDです。
        /// </param>
        /// <param name="isMain">
        /// メインプラーナムとして装備されている場合は<c>true</c>です。
        /// </param>
        /// <param name="number">
        /// メインまたはサブ内でのスロット番号です。
        /// </param>
        /// <param name="unitBehavior">
        /// このプラーナムを所有するユニットです。
        /// </param>
        /// <param name="pranamBehavior">
        /// このプラーナムを管理するコンポーネントです。
        /// </param>
        /// <param name="pranamData">
        /// このプラーナムの設定データです。
        /// </param>
        /// <param name="startAngle">
        /// 攻撃範囲の初期回転角度です。
        /// </param>
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
                    $"{nameof(AttackerBase)}: " +
                    $"{nameof(UnitBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(AttackerBase)}: " +
                    $"{nameof(PranamBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamData is not AttackerPranamData attackerPranamData)
            {
                Debug.LogError(
                    $"{nameof(AttackerBase)}: " +
                    $"{nameof(AttackerPranamData)}以外のデータが渡されました。" +
                    $" Data={pranamData?.GetType().Name ?? "null"}",
                    this);

                return;
            }

            _playerId = playerId;
            _isMain = isMain;
            _number = number;

            _unitBehavior = unitBehavior;
            _pranamBehavior = pranamBehavior;
            _pranamData = attackerPranamData;

            _mapUnitBehavior =
                unitBehavior.GetTacticalMapUnitBehavior();

            _microUnitBehavior =
                unitBehavior.GetMicroUnitBehavior();

            if (_mapUnitBehavior == null ||
                _microUnitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(AttackerBase)}: " +
                    "初期化に必要なユニットコンポーネントを取得できません。" +
                    $" PlayerId={playerId}",
                    unitBehavior);

                return;
            }

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

            _time = _pranamData.RecoveryTime;
            _state = AttackState.Idle;
            _target = null;

            _isInitialized = true;
        }

        /// <summary>
        /// 自身と対象の間から、近接攻撃を実行する位置を計算します。
        /// </summary>
        /// <param name="target">
        /// 攻撃対象です。
        /// </param>
        /// <param name="positionRatio">
        /// 自身を0、対象を1とした攻撃位置の補間率です。
        /// </param>
        /// <returns>
        /// 計算されたワールド座標です。
        /// 対象が無効な場合は現在位置です。
        /// </returns>
        public Vector3 CalculateAttackPosition(
            GameObject target,
            float positionRatio = k_DefaultAttackPositionRatio)
        {
            if (_microUnitBehavior == null)
            {
                return transform.position;
            }

            Vector3 selfPosition =
                _microUnitBehavior.transform.position;

            if (target == null)
            {
                return selfPosition;
            }

            return Vector3.Lerp(
                selfPosition,
                target.transform.position,
                Mathf.Clamp01(positionRatio));
        }

        /// <summary>
        /// 指定された対象への近接攻撃移動を開始します。
        /// </summary>
        /// <param name="target">
        /// 攻撃対象です。
        /// </param>
        /// <param name="positionRatio">
        /// 自身を0、対象を1とした攻撃位置の補間率です。
        /// </param>
        /// <returns>
        /// 攻撃移動を開始できた場合は<c>true</c>です。
        /// </returns>
        protected bool BeginAttackMovement(
            GameObject target,
            float positionRatio = k_DefaultAttackPositionRatio)
        {
            if (!_isInitialized ||
                _state != AttackState.Idle ||
                target == null ||
                _unitBehavior == null ||
                _microUnitBehavior == null)
            {
                return false;
            }

            _target = target;

            _startPosition =
                _microUnitBehavior.transform.position;

            _attackPosition =
                CalculateAttackPosition(
                    target,
                    positionRatio);

            _state = AttackState.Moving;

            _unitBehavior.LerpMoveToPosition(
                _attackPosition);

            return true;
        }

        /// <summary>
        /// 指定された位置へ到達したかどうかを判定します。
        /// </summary>
        /// <param name="position">
        /// 到達判定を行う位置です。
        /// </param>
        /// <returns>
        /// 指定位置との距離が許容範囲内の場合は<c>true</c>です。
        /// </returns>
        private bool HasReached(
            Vector3 position)
        {
            if (_microUnitBehavior == null)
            {
                return false;
            }

            float squaredDistance =
                (_microUnitBehavior.transform.position -
                position).sqrMagnitude;

            return squaredDistance <=
                k_ReachDistance *
                k_ReachDistance;
        }

        /// <summary>
        /// 対象への近接攻撃を実行します。
        /// </summary>
        private void StartAttack()
        {
            if (_target == null ||
                _microUnitBehavior == null ||
                _unitBehavior == null ||
                _pranamBehavior == null ||
                _pranamData == null)
            {
                BeginReturnMovement();

                return;
            }

            UnitStatus targetStatus = _target.GetComponent<MicroUnitBehavior>()?.GetUnitStatus();
            if (targetStatus == null || !targetStatus.IsAlive)
            {
                BeginReturnMovement();
                return;
            }

            _state = AttackState.Attacking;

            Vector3 instantiatePosition =
                _microUnitBehavior.transform.position;

            Vector3 attackDirection =
                _target.transform.position -
                instantiatePosition;

            attackDirection.y = 0.0f;

            Quaternion instantiateRotation =
                attackDirection.sqrMagnitude >
                k_MinimumDirectionSqrMagnitude
                    ? Quaternion.LookRotation(
                        attackDirection.normalized)
                    : _microUnitBehavior.transform.rotation;

            CreateAttackObject(
                _playerId,
                instantiatePosition,
                instantiateRotation,
                targetStatus.PlayerId);

            _pranamBehavior.RequestPranamSound(
                _pranamData,
                PranamSoundEvent.Use,
                instantiatePosition,
                attackDirection.sqrMagnitude >
                k_MinimumDirectionSqrMagnitude
                    ? attackDirection.normalized
                    : _microUnitBehavior.transform.forward);

            BeginReturnMovement();
        }

        /// <summary>
        /// 攻撃開始前の位置への帰還を開始します。
        /// </summary>
        private void BeginReturnMovement()
        {
            _state = AttackState.Returning;

            if (_unitBehavior == null)
            {
                CompleteAttackSequence();

                return;
            }

            _unitBehavior.LerpMoveToPosition(
                _startPosition);
        }

        /// <summary>
        /// 攻撃終了後の硬直を適用します。
        /// </summary>
        protected void ApplyRecovery()
        {
            if (_pranamBehavior == null ||
                _pranamData == null)
            {
                return;
            }

            _pranamBehavior.ApplyRecoveryLock(
                _isMain,
                _number,
                _pranamData.RecoveryTime);
        }

        /// <summary>
        /// 近接攻撃の移動状態を更新します。
        /// </summary>
        private void UpdateAttackState()
        {
            switch (_state)
            {
                case AttackState.Idle:
                    return;

                case AttackState.Moving:
                    UpdateMovingState();
                    return;

                case AttackState.Attacking:
                    return;

                case AttackState.Returning:
                    UpdateReturningState();
                    return;
            }
        }

        /// <summary>
        /// 攻撃位置へ移動中の状態を更新します。
        /// </summary>
        private void UpdateMovingState()
        {
            if (_target == null)
            {
                BeginReturnMovement();

                return;
            }

            if (HasReached(_attackPosition))
            {
                StartAttack();
            }
        }

        /// <summary>
        /// 元の位置へ帰還中の状態を更新します。
        /// </summary>
        private void UpdateReturningState()
        {
            if (HasReached(_startPosition))
            {
                CompleteAttackSequence();
            }
        }

        /// <summary>
        /// 一連の近接攻撃処理を完了します。
        /// </summary>
        private void CompleteAttackSequence()
        {
            _state = AttackState.Idle;
            _target = null;

            ApplyRecovery();
        }

        /// <summary>
        /// 近接攻撃の状態と、
        /// 基底クラスのターゲット検出処理を更新します。
        /// </summary>
        protected override void Update()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null ||
                !unitManager.CanExecuteUnitLogic(
                    _playerId))
            {
                return;
            }

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null ||
                phaseManager.CurrentPhase !=
                MatchPhase.Micro)
            {
                return;
            }

            UpdateAttackState();

            base.Update();
        }

        /// <summary>
        /// 近接攻撃用のプラーナオブジェクトを生成します。
        /// </summary>
        /// <param name="playerId">
        /// 攻撃を実行したプレイヤーIDです。
        /// </param>
        /// <param name="instantiatePosition">
        /// 生成位置です。
        /// </param>
        /// <param name="instantiateRotation">
        /// 生成回転です。
        /// </param>
        /// <param name="targetPlayerId">
        /// この近接攻撃でダメージを与える対象ユニットです。
        /// </param>
        protected void CreateAttackObject(
            int playerId,
            Vector3 instantiatePosition,
            Quaternion instantiateRotation,
            int targetPlayerId)
        {
            if (_pranamBehavior == null ||
                _pranamData == null)
            {
                return;
            }

            PranamContext context =
                new()
                {
                    PlayerId = playerId,
                    TargetPlayerId = targetPlayerId,
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
