using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Pranam.Networking;
using PranaTactics.Pranam.Targeting;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 銃撃型プラーナムに共通する初期化処理と、
    /// 単発および連続射撃用のプラーナ生成処理を提供します。
    /// </summary>
    public abstract class GunnerBase : PranamBase
    {
        protected override IAttackTargetSelection TargetSelection => NearestAttackTargetSelection.Instance;

        /// <summary>
        /// 銃撃型プラーナムの設定データです。
        /// </summary>
        private GunnerPranamData _pranamData;

        protected GunnerPranamData GunnerData
        {
            get
            {
                return _pranamData;
            }
        }

        /// <summary>
        /// 銃撃型プラーナムを初期化します。
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
                    $"{nameof(GunnerBase)}: " +
                    $"{nameof(UnitBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(GunnerBase)}: " +
                    $"{nameof(PranamBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamData is not GunnerPranamData gunnerPranamData)
            {
                Debug.LogError(
                    $"{nameof(GunnerBase)}: " +
                    $"{nameof(GunnerPranamData)}以外のデータが渡されました。" +
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
                    $"{nameof(GunnerBase)}: " +
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
            _pranamBehavior = pranamBehavior;
            _pranamData = gunnerPranamData;

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
            _isInitialized = true;
        }

        /// <summary>
        /// コンポーネントが再度有効になったとき、
        /// 射撃間隔の計測時間を初期化します。
        /// </summary>
        protected override void OnEnableInternal()
        {
            base.OnEnableInternal();

            if (_pranamData == null)
            {
                return;
            }

            _time = _pranamData.RecoveryTime;
        }

        /// <summary>
        /// 銃撃用のプラーナオブジェクトを生成します。
        /// </summary>
        /// <param name="playerId">
        /// 攻撃を実行したプレイヤーIDです。
        /// </param>
        /// <param name="instantiatePosition">
        /// プラーナの生成位置です。
        /// </param>
        /// <param name="targetPlayerId">
        /// 攻撃対象のプレイヤーIDです。
        /// 対象を指定しない場合は負の値を使用します。
        /// </param>
        /// <remarks>
        /// IdはPranamBehavior.CreatePranaObjectで
        /// 生成オブジェクト固有のIDに設定されます。
        /// </remarks>
        protected void CreateAttackObject(
            int playerId,
            Vector3 instantiatePosition,
            int targetPlayerId)
        {
            if (!_isInitialized ||
                _pranamBehavior == null ||
                _pranamData == null ||
                _unitBehavior == null)
            {
                return;
            }

            MicroUnitBehavior microUnitBehavior =
                _unitBehavior.GetMicroUnitBehavior();

            if (microUnitBehavior == null)
            {
                Debug.LogWarning(
                    $"{nameof(GunnerBase)}: " +
                    $"{nameof(MicroUnitBehavior)}を取得できないため、" +
                    "プラーナを生成できません。",
                    _unitBehavior);

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
                        microUnitBehavior.transform.position,

                    DefinitionId = _pranamData.name
                };

            _pranamBehavior.CreatePranaObject(
                _pranamData.PrefabResourcePath,
                instantiatePosition,
                Quaternion.identity,
                context);
        }

        /// <summary>
        /// 自動射撃用のプラーナを生成し、
        /// 発射音、コスト消費および射撃間隔の硬直を適用します。
        /// </summary>
        /// <param name="playerId">
        /// 攻撃を実行したプレイヤーIDです。
        /// </param>
        /// <param name="instantiatePosition">
        /// プラーナの生成位置です。
        /// </param>
        /// <param name="targetPlayerId">
        /// 攻撃対象のプレイヤーIDです。
        /// </param>
        /// <param name="fireInterval">
        /// 次の弾を発射できるまでの時間です。
        /// </param>
        /// <param name="actionPointCost">
        /// 1発の発射で消費する行動力です。
        /// </param>
        /// <param name="pranaCost">
        /// 1発の発射で消費するプラーナ量です。
        /// </param>
        /// <param name="isFirstShot">
        /// 連続射撃の1発目の場合は<c>true</c>です。
        /// </param>
        protected void CreateAutomaticAttackObject(
            int playerId,
            Vector3 instantiatePosition,
            int targetPlayerId,
            float fireInterval,
            int actionPointCost,
            int pranaCost,
            bool isFirstShot)
        {
            if (!_isInitialized ||
                _pranamBehavior == null ||
                _pranamData == null)
            {
                return;
            }

            CreateAttackObject(
                playerId,
                instantiatePosition,
                targetPlayerId);

            _pranamBehavior.RequestPranamSound(
                _pranamData,
                isFirstShot
                    ? PranamSoundEvent.Fire
                    : PranamSoundEvent.Rapid_Fire,
                instantiatePosition);

            _pranamBehavior.ConsumeCosts(
                playerId,
                Mathf.Max(
                    0,
                    actionPointCost),
                Mathf.Max(
                    0,
                    pranaCost));

            float sanitizedFireInterval =
                Mathf.Max(
                    0.0f,
                    fireInterval);

            _pranamBehavior.ApplyRecoveryLock(
                _isMain,
                _number,
                sanitizedFireInterval);

            _time = 0.0f;
        }
    }
}
