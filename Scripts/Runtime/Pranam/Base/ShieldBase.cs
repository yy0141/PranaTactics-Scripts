using UnityEngine;

using PranaTactics.Prana;
using PranaTactics.Pranam.Networking;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 防御型プラーナムに共通する初期化処理と、
    /// シールド用プラーナオブジェクトの生成処理を提供します。
    /// </summary>
    public abstract class ShieldBase : PranamBase
    {
        /// <summary>
        /// 防御型プラーナムの設定データです。
        /// </summary>
        private DefencePranamData _pranamData;

        /// <summary>
        /// 所有ユニットのミクロ・フェイズ用コンポーネントです。
        /// </summary>
        private MicroUnitBehavior _microUnitBehavior;

        /// <summary>
        /// 現在生成されているシールドオブジェクトです。
        /// </summary>
        private ShieldPranaBase _activeShield;

        protected DefencePranamData ShieldData
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

        protected ShieldPranaBase ActiveShield
        {
            get
            {
                return _activeShield;
            }
            set
            {
                _activeShield = value;
            }
        }

        protected bool HasActiveShield
        {
            get
            {
                return _activeShield != null;
            }
        }

        /// <summary>
        /// 防御型プラーナムを初期化します。
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
        /// プラーナムの初期回転角度です。
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
                    $"{nameof(ShieldBase)}: " +
                    $"{nameof(UnitBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(ShieldBase)}: " +
                    $"{nameof(PranamBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamData is not DefencePranamData defencePranamData)
            {
                Debug.LogError(
                    $"{nameof(ShieldBase)}: " +
                    $"{nameof(DefencePranamData)}以外のデータが渡されました。" +
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
                    $"{nameof(ShieldBase)}: " +
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
            _pranamData = defencePranamData;

            _activeShield = null;
            _time = _pranamData.RecoveryTime;

            DisableTargetDetection();

            _isInitialized = true;
        }

        /// <summary>
        /// シールド用のプラーナオブジェクトを生成します。
        /// </summary>
        /// <param name="playerId">
        /// シールドを使用したプレイヤーIDです。
        /// </param>
        /// <param name="isMain">
        /// メインプラーナムとして使用された場合は<c>true</c>です。
        /// </param>
        /// <param name="isMountRoot">
        /// <c>true</c>の場合はMicroUnitに追従するMountRoot、
        /// <c>false</c>の場合はワールド空間のWorldRootへ設定します。
        /// </param>
        /// <param name="instantiatePosition">
        /// シールドの生成位置です。
        /// </param>
        /// <param name="instantiateRotation">
        /// シールドの生成回転です。
        /// </param>
        protected void CreateShieldObject(
            int playerId,
            bool isMain,
            bool isMountRoot,
            Vector3 instantiatePosition,
            Quaternion instantiateRotation)
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
                    $"{nameof(ShieldBase)}: " +
                    "シールドPrefabのResourcesパスが設定されていません。" +
                    $" Pranam={_pranamData.DisplayName}",
                    _pranamData);

                return;
            }

            PranamContext context =
                new()
                {
                    PlayerId = playerId,
                    IsMain = isMain,
                    PranamNumber = _number,
                    IsMountRoot = isMountRoot,

                    GeneratePosition = instantiatePosition,

                    DefinitionId = _pranamData.name
                };

            _pranamBehavior.CreatePranaObject(
                _pranamData.PrefabResourcePath,
                instantiatePosition,
                instantiateRotation,
                context);
        }

        /// <summary>
        /// 現在のシールド参照を設定します。
        /// </summary>
        /// <param name="shield">
        /// 生成されたシールドです。
        /// </param>
        protected void SetActiveShield(
            ShieldPranaBase shield)
        {
            _activeShield = shield;
        }

        /// <summary>
        /// 現在保持しているシールド参照を解除します。
        /// </summary>
        /// <param name="shield">
        /// 解除対象のシールドです。
        /// 現在のシールドと一致する場合のみ解除されます。
        /// </param>
        protected void ClearActiveShield(
            ShieldPranaBase shield)
        {
            if (_activeShield != shield)
            {
                return;
            }

            _activeShield = null;
        }

        /// <summary>
        /// 防御型プラーナムでは対象検出を使用しないため、
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
