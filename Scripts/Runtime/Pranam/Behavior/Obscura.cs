using UnityEngine;

using PranaTactics.Match.Phases;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// ミクロ・フェイズ中に所有ユニットをステルス状態へ変更し、
    /// 一定時間ごとに行動力を消費するオプション型プラーナムです。
    /// </summary>
    public sealed class Obscura : PranamBase
    {
        /// <summary>
        /// オプション型プラーナムの設定データです。
        /// </summary>
        private OptionPranamData _pranamData;

        /// <summary>
        /// 前回の維持コスト消費からの経過時間です。
        /// </summary>
        private float _consumptionTimer;

        /// <summary>
        /// 現在ステルス状態が有効かどうかを示します。
        /// </summary>
        private bool _isStealthActive;

        /// <summary>
        /// AIの交戦判断によって自動発動を許可するかどうかです。
        /// </summary>
        private bool _isAutomaticActivationAllowed = true;

        /// <summary>
        /// 自動発動の可否を変更し、交戦に移るときはステルスを解除します。
        /// </summary>
        public void SetAutomaticActivationAllowed(bool isAllowed)
        {
            _isAutomaticActivationAllowed = isAllowed;
            if (!isAllowed)
            {
                DeactivateStealth(true);
            }
        }

        protected OptionPranamData OptionData
        {
            get
            {
                return _pranamData;
            }
        }

        public bool IsStealthActive
        {
            get
            {
                return _isStealthActive;
            }
        }

        public override bool RequiresInput
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// ステルス型プラーナムを初期化します。
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
        /// 初期角度です。
        /// このプラーナムでは使用しません。
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
                    $"{nameof(Obscura)}: " +
                    $"{nameof(UnitBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(Obscura)}: " +
                    $"{nameof(PranamBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamData is not OptionPranamData optionPranamData)
            {
                Debug.LogError(
                    $"{nameof(Obscura)}: " +
                    $"{nameof(OptionPranamData)}を継承したデータが必要です。" +
                    $" Data={pranamData?.GetType().Name ?? "null"}",
                    this);

                return;
            }

            TacticalMapUnitBehavior mapUnitBehavior =
                unitBehavior.GetTacticalMapUnitBehavior();

            if (mapUnitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(Obscura)}: " +
                    $"{nameof(TacticalMapUnitBehavior)}を取得できません。" +
                    $" PlayerId={playerId}",
                    unitBehavior);

                return;
            }

            DeactivateStealth(
                false);

            _playerId = playerId;
            _isMain = isMain;
            _number = number;

            _unitBehavior = unitBehavior;
            _mapUnitBehavior = mapUnitBehavior;
            _pranamBehavior = pranamBehavior;
            _pranamData = optionPranamData;

            _time = 0.0f;
            _consumptionTimer = 0.0f;
            _isStealthActive = false;

            DisableTargetDetection();

            _isInitialized = true;
        }

        /// <summary>
        /// 所有ユニットのステルス状態を有効化します。
        /// </summary>
        public override void UsePranam()
        {
            if (!_isInitialized ||
                !_isAutomaticActivationAllowed ||
                _isStealthActive ||
                _unitBehavior == null ||
                _pranamBehavior == null ||
                _pranamData == null)
            {
                return;
            }

            if (!_pranamBehavior.CanUsePranam(
                    0,
                    _pranamData.PranaCost))
            {
                return;
            }

            if (!TryGetOwnerTeamId(
                    out int ownerTeamId))
            {
                return;
            }

            _unitBehavior.SwitchStealth(
                ownerTeamId,
                true);

            _isStealthActive = true;
            _consumptionTimer = 0.0f;
        }

        /// <summary>
        /// ステルス状態を解除し、
        /// 終了時のプラーナコストを適用します。
        /// </summary>
        public override void Exit()
        {
            DeactivateStealth(
                true);

            base.Exit();
        }

        /// <summary>
        /// コンポーネントが有効になったとき、
        /// 維持コスト用のタイマーを初期化します。
        /// </summary>
        protected override void OnEnableInternal()
        {
            base.OnEnableInternal();

            _consumptionTimer = 0.0f;
        }

        /// <summary>
        /// コンポーネントが無効になったとき、
        /// コストを発生させずにステルス表示を解除します。
        /// </summary>
        protected override void OnDisableInternal()
        {
            try
            {
                DeactivateStealth(
                    false);
            }
            finally
            {
                base.OnDisableInternal();
            }
        }

        /// <summary>
        /// ステルスの自動発動と維持コストを更新します。
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
                return;
            }

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null ||
                phaseManager.CurrentPhase !=
                MatchPhase.Micro)
            {
                Exit();

                return;
            }

            _time += Time.deltaTime;
            _consumptionTimer += Time.deltaTime;

            if (!_isStealthActive)
            {
                UsePranam();
            }

            if (!_isStealthActive)
            {
                return;
            }

            UpdateConsumptionCost();
        }

        /// <summary>
        /// 設定された時間間隔ごとに、
        /// ステルス維持に必要な行動力を消費します。
        /// </summary>
        private void UpdateConsumptionCost()
        {
            if (_pranamData == null ||
                _pranamBehavior == null)
            {
                return;
            }

            float consumptionInterval =
                _pranamData.ConsumptionInterval;

            if (consumptionInterval <= 0.0f ||
                _consumptionTimer <
                consumptionInterval)
            {
                return;
            }

            int actionPointCost =
                Mathf.Max(
                    0,
                    _pranamData.ActionPointCost);

            if (!_pranamBehavior.CanUsePranam(
                    actionPointCost,
                    0.0f))
            {
                Exit();

                return;
            }

            _consumptionTimer -=
                consumptionInterval;

            _pranamBehavior.ConsumeCosts(
                _playerId,
                actionPointCost,
                0.0f);
        }

        /// <summary>
        /// 所有ユニットのステルス状態を解除します。
        /// </summary>
        /// <param name="shouldConsumePrana">
        /// 終了時のプラーナコストを適用する場合は<c>true</c>です。
        /// </param>
        private void DeactivateStealth(
            bool shouldConsumePrana)
        {
            if (!_isStealthActive)
            {
                return;
            }

            _isStealthActive = false;
            _consumptionTimer = 0.0f;

            UnitBehavior unitBehavior =
                _unitBehavior;

            if (unitBehavior == null ||
                !unitBehavior.gameObject.activeInHierarchy ||
                !unitBehavior.IsInitialized)
            {
                return;
            }

            if (TryGetOwnerTeamId(
                    out int ownerTeamId))
            {
                unitBehavior.SwitchStealth(
                    ownerTeamId,
                    false);
            }

            if (shouldConsumePrana &&
                _pranamData != null &&
                _pranamData.PranaCost > 0)
            {
                unitBehavior.ApplyDamage(
                    _playerId,
                    _pranamData.PranaCost,
                    false);
            }
        }

        /// <summary>
        /// 味方へ通常表示を維持するための，
        /// ステルス使用者の所属チームIDを取得します。
        /// </summary>
        private bool TryGetOwnerTeamId(
            out int ownerTeamId)
        {
            ownerTeamId =
                _unitBehavior != null
                    ? _unitBehavior.TeamId
                    : -1;

            if (ownerTeamId >= 0)
            {
                return true;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return false;
            }

            ownerTeamId =
                unitManager.GetTeamId(
                    _playerId);

            return ownerTeamId >= 0;
        }

        /// <summary>
        /// ステルス型プラーナムでは対象検出を使用しないため、
        /// 基底クラスの検出Colliderを無効化します。
        /// </summary>
        private void DisableTargetDetection()
        {
            SphereCollider detectionCollider =
                GetComponent<SphereCollider>();

            if (detectionCollider != null)
            {
                detectionCollider.enabled = false;
            }
        }
    }
}
