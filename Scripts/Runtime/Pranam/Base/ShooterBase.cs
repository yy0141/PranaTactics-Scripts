using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Pranam.Networking;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 射出型プラーナムに共通する初期化処理と、
    /// 通常弾および分割弾の生成処理を提供します。
    /// </summary>
    public abstract class ShooterBase : PranamBase
    {
        /// <summary>
        /// 対象数がこの値未満の場合に、
        /// 小さい分割数を使用します。
        /// </summary>
        private const int k_LargeDivisionTargetThreshold = 3;

        /// <summary>
        /// 対象数が少ない場合の各軸の分割数です。
        /// </summary>
        private const int k_SmallDivisionCount = 3;

        /// <summary>
        /// 対象数が多い場合の各軸の分割数です。
        /// </summary>
        private const int k_LargeDivisionCount = 4;

        /// <summary>
        /// 分割弾同士の間隔として使用する最小値です。
        /// </summary>
        private const float k_MinimumProjectileSpacing = 0.01f;

        /// <summary>
        /// 射出型プラーナムの設定データです。
        /// </summary>
        private ShooterPranamData _pranamData;

        protected ShooterPranamData ShooterData
        {
            get
            {
                return _pranamData;
            }
        }

        /// <summary>
        /// 射出型プラーナムを初期化します。
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
                    $"{nameof(ShooterBase)}: " +
                    $"{nameof(UnitBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(ShooterBase)}: " +
                    $"{nameof(PranamBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (pranamData is not ShooterPranamData shooterPranamData)
            {
                Debug.LogError(
                    $"{nameof(ShooterBase)}: " +
                    $"{nameof(ShooterPranamData)}以外のデータが渡されました。" +
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
                    $"{nameof(ShooterBase)}: " +
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
            _pranamData = shooterPranamData;

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

            Debug.Log(
                $"[UnitInit] Pranam Initialize " +
                $"Frame={Time.frameCount}," +
                $"Name={pranamData?.DisplayName}," +
                $"Angle={_pranamData.Angle}," +
                $"Range={_pranamData.Diameter}",
                this);
        }

        /// <summary>
        /// コンポーネントが再度有効になったとき、
        /// 使用間隔の計測時間を初期化します。
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
        /// 攻撃用のプラーナオブジェクトを生成します。
        /// </summary>
        /// <param name="playerId">
        /// 攻撃を実行したプレイヤーIDです。
        /// </param>
        /// <param name="instantiatePosition">
        /// プラーナを生成する位置です。
        /// </param>
        /// <param name="generatePosition">
        /// プラーナの軌道計算に使用する基準位置です。
        /// </param>
        /// <param name="targetPlayerId">
        /// 攻撃対象のプレイヤーIDです。
        /// 対象を指定しない場合は負の値を使用します。
        /// </param>
        /// <param name="attackDivisionCount">
        /// 固定設定の合計威力を分配する弾数です。
        /// </param>
        /// <param name="soundGroupId">
        /// 同じ発射操作で生成した弾の効果音グループです。
        /// </param>
        protected void CreateAttackObject(
            int playerId,
            Vector3 instantiatePosition,
            Vector3 generatePosition,
            int targetPlayerId,
            int attackDivisionCount,
            string soundGroupId = null)
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
                    $"{nameof(ShooterBase)}: " +
                    "生成するプラーナのResourcesパスが設定されていません。" +
                    $" Pranam={_pranamData.DisplayName}",
                    _pranamData);

                return;
            }

            PranamContext context =
                new()
                {
                    PlayerId = playerId,
                    SoundGroupId = soundGroupId,
                    IsMain = _isMain,
                    PranamNumber = _number,

                    GeneratePosition = generatePosition,
                    TargetPlayerId = targetPlayerId,

                    DefinitionId = _pranamData.name
                };

            _pranamBehavior.CreatePranaObject(
                _pranamData.PrefabResourcePath,
                instantiatePosition,
                Quaternion.identity,
                context,
                attackDivisionCount);
        }

        /// <summary>
        /// 指定位置を中心にプラーナを立方体状に分割生成し、
        /// 対象プレイヤーへ均等に割り当てます。
        /// </summary>
        /// <param name="generatePosition">
        /// 分割弾を生成する中心位置です。
        /// </param>
        /// <param name="targetPlayerIds">
        /// 分割弾を割り当てる対象プレイヤーIDの一覧です。
        /// </param>
        /// <returns>
        /// 1つ以上のプラーナを生成した場合は<c>true</c>です。
        /// </returns>
        protected bool GenerateDistributedPranaCube(
            Vector3 generatePosition,
            IReadOnlyList<int> targetPlayerIds)
        {
            if (!_isInitialized ||
                _pranamData == null ||
                _pranamBehavior == null)
            {
                return false;
            }

            if (targetPlayerIds == null ||
                targetPlayerIds.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(ShooterBase)}: " +
                    "分割弾の対象が設定されていません。",
                    this);

                return false;
            }

            GameObject pranamPrefab =
                _pranamData.PranamPrefab;

            if (pranamPrefab == null)
            {
                Debug.LogError(
                    $"{nameof(ShooterBase)}: " +
                    "分割弾の間隔計算に使用するPrefabが設定されていません。" +
                    $" Pranam={_pranamData.DisplayName}",
                    _pranamData);

                return false;
            }

            int divisionCount =
                targetPlayerIds.Count <
                k_LargeDivisionTargetThreshold
                    ? k_SmallDivisionCount
                    : k_LargeDivisionCount;

            int projectileCount =
                divisionCount *
                divisionCount *
                divisionCount;

            Vector3 projectileSpacing =
                GetProjectileSpacing(
                    pranamPrefab);

            float centerOffset =
                (divisionCount - 1) *
                0.5f;

            int projectileIndex = 0;
            string soundGroupId = System.Guid.NewGuid().ToString("N");

            for (int xIndex = 0;
                xIndex < divisionCount;
                xIndex++)
            {
                for (int yIndex = 0;
                    yIndex < divisionCount;
                    yIndex++)
                {
                    for (int zIndex = 0;
                        zIndex < divisionCount;
                        zIndex++)
                    {
                        Vector3 gridOffset =
                            new(
                                (xIndex - centerOffset) *
                                projectileSpacing.x,

                                (yIndex - centerOffset) *
                                projectileSpacing.y,

                                (zIndex - centerOffset) *
                                projectileSpacing.z);

                        Vector3 instantiatePosition =
                            generatePosition +
                            gridOffset;

                        int targetPlayerId =
                            targetPlayerIds[
                                projectileIndex %
                                targetPlayerIds.Count];

                        CreateAttackObject(
                            _playerId,
                            instantiatePosition,
                            generatePosition,
                            targetPlayerId,
                            projectileCount,
                            soundGroupId);

                        projectileIndex++;
                    }
                }
            }

            return projectileIndex > 0;
        }

        /// <summary>
        /// プラーナPrefabのスケールから、
        /// 分割生成時に使用する各軸の間隔を取得します。
        /// </summary>
        /// <param name="pranamPrefab">
        /// 間隔計算に使用するPrefabです。
        /// </param>
        /// <returns>
        /// 各軸の生成間隔です。
        /// </returns>
        private static Vector3 GetProjectileSpacing(
            GameObject pranamPrefab)
        {
            if (pranamPrefab == null)
            {
                return Vector3.one;
            }

            Vector3 prefabScale =
                pranamPrefab.transform.lossyScale;

            return new Vector3(
                Mathf.Max(
                    Mathf.Abs(prefabScale.x),
                    k_MinimumProjectileSpacing),

                Mathf.Max(
                    Mathf.Abs(prefabScale.y),
                    k_MinimumProjectileSpacing),

                Mathf.Max(
                    Mathf.Abs(prefabScale.z),
                    k_MinimumProjectileSpacing));
        }
    }
}
