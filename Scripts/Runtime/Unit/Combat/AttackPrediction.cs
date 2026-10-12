using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Combat;

namespace PranaTactics.Unit
{
    /// <summary>
    /// ユニットへ通知された攻撃情報から、
    /// 命中する可能性のある攻撃と防御優先度を予測します。
    /// </summary>
    /// <remarks>
    /// 攻撃は、攻撃者ID、Main・Subの種別、弾IDの組み合わせで管理します。
    /// 同じ弾から再通知された場合は、以前の情報を最新情報で置き換えます。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AttackPrediction : MonoBehaviour
    {
        /// <summary>
        /// ベクトルが有効な長さを持つか判定するための値です。
        /// </summary>
        private const float k_VectorEpsilon = 0.0001f;

        /// <summary>
        /// 危険度計算で使用する最小到達時間です。
        /// </summary>
        private const float k_MinimumScoreTime = 0.1f;

        /// <summary>
        /// 攻撃予測の対象となるユニットです。
        /// </summary>
        [Header("Owner")]
        [SerializeField]
        private MicroUnitBehavior _unitBehavior;

        /// <summary>
        /// 最後の通知から攻撃情報を保持する時間です。
        /// </summary>
        [Header("Lifetime")]
        [Min(0.0f)]
        [SerializeField]
        private float _threatLifetime = 2.0f;

        /// <summary>
        /// 命中予測へ使用できる最新通知の経過時間です。
        /// </summary>
        /// <remarks>
        /// 攻撃情報自体は<see cref="_threatLifetime"/>まで保持しますが、
        /// 命中予測には、より新しい情報だけを使用します。
        /// </remarks>
        [Min(0.0f)]
        [SerializeField]
        private float _recentNotificationWindow = 0.8f;

        /// <summary>
        /// 現在時刻から何秒先まで命中を予測するかを表します。
        /// </summary>
        [Header("Collision Prediction")]
        [Min(0.0f)]
        [SerializeField]
        private float _maximumPredictionTime = 2.0f;

        /// <summary>
        /// 通常弾がユニットへ命中すると判定する距離です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _normalHitRadius = 1.25f;

        /// <summary>
        /// 上空から落下する攻撃の水平方向の命中判定半径です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _fallingHitRadius = 3.0f;

        /// <summary>
        /// 落下攻撃として判定する進行方向のY値です。
        /// </summary>
        [Range(-1.0f, 0.0f)]
        [SerializeField]
        private float _fallingDirectionThreshold = -0.5f;

        /// <summary>
        /// 命中予測に使用する最小速度です。
        /// </summary>
        [Min(0.01f)]
        [SerializeField]
        private float _minimumPredictionSpeed = 0.1f;

        /// <summary>
        /// ユニットのTransform位置から命中判定位置へ加える高さです。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _targetHeightOffset = 1.0f;

        /// <summary>
        /// 攻撃を同じクラスタへまとめる方向類似度です。
        /// </summary>
        /// <remarks>
        /// 1に近いほど、進行方向が近い攻撃だけを
        /// 同じクラスタへまとめます。
        /// </remarks>
        [Header("Clustering")]
        [Range(-1.0f, 1.0f)]
        [SerializeField]
        private float _clusterDirectionSimilarity = 0.9f;

        /// <summary>
        /// 追尾攻撃へ適用する危険度倍率です。
        /// </summary>
        [Header("Scoring")]
        [Min(0.0f)]
        [SerializeField]
        private float _homingScoreMultiplier = 1.25f;

        /// <summary>
        /// 対象の動きを継続的に追跡している攻撃へ
        /// 適用する危険度倍率です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _trackingScoreMultiplier = 1.15f;

        /// <summary>
        /// 弾ごとの最新攻撃情報です。
        /// </summary>
        private readonly Dictionary<ThreatKey, ThreatState> _threats = new();

        /// <summary>
        /// 現在計算されている攻撃クラスタです。
        /// </summary>
        private readonly List<ThreatCluster> _currentClusters = new();

        /// <summary>
        /// Dictionaryから削除する攻撃キーを一時的に保持します。
        /// </summary>
        private readonly List<ThreatKey> _removalBuffer = new();

        public IReadOnlyList<ThreatCluster> CurrentClusters
        {
            get
            {
                return _currentClusters;
            }
        }

        public ThreatCluster MostDangerousCluster
        {
            get;
            private set;
        }

        public int ThreatCount
        {
            get
            {
                return _threats.Count;
            }
        }

        /// <summary>
        /// 所有ユニットを取得します。
        /// </summary>
        private void Awake()
        {
            if (_unitBehavior == null)
            {
                _unitBehavior =
                    GetComponentInParent<MicroUnitBehavior>();
            }
        }

        /// <summary>
        /// このコンポーネントが無効になったときに
        /// 攻撃予測情報を初期化します。
        /// </summary>
        private void OnDisable()
        {
            ClearThreats();
        }

        /// <summary>
        /// 実行権限を持つユニットの攻撃予測を更新します。
        /// </summary>
        private void Update()
        {
            if (!CanEvaluatePrediction())
            {
                ClearPredictionResults();

                return;
            }

            RefreshPrediction();
        }

        /// <summary>
        /// 弾から通知された最新の攻撃情報を登録します。
        /// </summary>
        /// <param name="data">
        /// 攻撃者、弾ID、位置、進行方向、威力および
        /// 速度を含む攻撃情報です。
        /// </param>
        public void NotifyThreat(
            ThreatData data)
        {
            if (!IsValidThreat(
                    data))
            {
                return;
            }

            data.Timestamp =
                Time.time;

            data.Direction =
                data.Direction.normalized;

            ThreatKey key =
                new(
                    data.AttackerPlayerId,
                    data.IsMainWeapon,
                    data.ThreatId);

            if (_threats.TryGetValue(
                    key,
                    out ThreatState threatState))
            {
                threatState.Update(
                    data);

                return;
            }

            _threats.Add(
                key,
                new ThreatState(
                    data));
        }

        /// <summary>
        /// 指定された攻撃者およびMain・Sub種別に該当する
        /// すべての攻撃情報を削除します。
        /// </summary>
        /// <param name="attackerPlayerId">
        /// 攻撃者のプレイヤーIDです。
        /// </param>
        /// <param name="isMainWeapon">
        /// Main側の攻撃を削除する場合は<c>true</c>です。
        /// </param>
        /// <returns>
        /// 削除された攻撃情報数です。
        /// </returns>
        public int RemoveThreats(
            int attackerPlayerId,
            bool isMainWeapon)
        {
            _removalBuffer.Clear();

            foreach (
                KeyValuePair<ThreatKey, ThreatState> pair in
                _threats)
            {
                if (pair.Key.AttackerPlayerId !=
                        attackerPlayerId ||
                    pair.Key.IsMainWeapon !=
                        isMainWeapon)
                {
                    continue;
                }

                _removalBuffer.Add(
                    pair.Key);
            }

            foreach (ThreatKey key in
                    _removalBuffer)
            {
                _threats.Remove(
                    key);
            }

            int removedCount =
                _removalBuffer.Count;

            _removalBuffer.Clear();

            return removedCount;
        }

        /// <summary>
        /// 指定された弾の攻撃情報だけを削除します。
        /// </summary>
        /// <param name="attackerPlayerId">
        /// 攻撃者のプレイヤーIDです。
        /// </param>
        /// <param name="isMainWeapon">
        /// Main側の攻撃である場合は<c>true</c>です。
        /// </param>
        /// <param name="threatId">
        /// 削除する弾のIDです。
        /// </param>
        /// <returns>
        /// 攻撃情報を削除できた場合は<c>true</c>です。
        /// </returns>
        public bool RemoveThreat(
            int attackerPlayerId,
            bool isMainWeapon,
            int threatId)
        {
            ThreatKey key =
                new(
                    attackerPlayerId,
                    isMainWeapon,
                    threatId);

            return _threats.Remove(
                key);
        }

        /// <summary>
        /// 保持しているすべての攻撃情報と
        /// 予測結果を削除します。
        /// </summary>
        public void ClearThreats()
        {
            _threats.Clear();
            _removalBuffer.Clear();

            ClearPredictionResults();
        }

        /// <summary>
        /// 最も危険な攻撃クラスタを取得します。
        /// </summary>
        /// <param name="cluster">
        /// 取得された攻撃クラスタです。
        /// </param>
        /// <returns>
        /// 危険な攻撃クラスタが存在する場合は<c>true</c>です。
        /// </returns>
        public bool TryGetMostDangerousCluster(
            out ThreatCluster cluster)
        {
            cluster =
                MostDangerousCluster;

            return cluster != null;
        }

        /// <summary>
        /// 現在の攻撃情報から命中予測と
        /// クラスタリングを更新します。
        /// </summary>
        private void RefreshPrediction()
        {
            float currentTime =
                Time.time;

            RemoveExpiredThreats(
                currentTime);

            ClearPredictionResults();

            if (_threats.Count == 0)
            {
                return;
            }

            Vector3 targetPosition =
                GetPredictionTargetPosition();

            foreach (
                KeyValuePair<ThreatKey, ThreatState> pair in
                _threats)
            {
                if (!TryPredictImpact(
                        pair.Value,
                        targetPosition,
                        currentTime,
                        out ImpactPrediction prediction))
                {
                    continue;
                }

                AddPredictionToCluster(
                    prediction);
            }

            SelectMostDangerousCluster();
        }

        /// <summary>
        /// 寿命を超えた攻撃情報を削除します。
        /// </summary>
        private void RemoveExpiredThreats(
            float currentTime)
        {
            _removalBuffer.Clear();

            foreach (
                KeyValuePair<ThreatKey, ThreatState> pair in
                _threats)
            {
                float elapsedTime =
                    currentTime -
                    pair.Value.Data.Timestamp;

                if (elapsedTime <=
                    _threatLifetime)
                {
                    continue;
                }

                _removalBuffer.Add(
                    pair.Key);
            }

            foreach (ThreatKey key in
                    _removalBuffer)
            {
                _threats.Remove(
                    key);
            }

            _removalBuffer.Clear();
        }

        /// <summary>
        /// 攻撃がユニットへ命中する可能性を計算します。
        /// </summary>
        private bool TryPredictImpact(
            ThreatState threatState,
            Vector3 targetPosition,
            float currentTime,
            out ImpactPrediction prediction)
        {
            prediction =
                default;

            if (threatState == null)
            {
                return false;
            }

            ThreatData data =
                threatState.Data;

            float elapsedTime =
                Mathf.Max(
                    0.0f,
                    currentTime -
                    data.Timestamp);

            if (elapsedTime >
                _recentNotificationWindow)
            {
                return false;
            }

            Vector3 travelDirection =
                data.Direction;

            if (travelDirection.sqrMagnitude <
                k_VectorEpsilon)
            {
                return false;
            }

            travelDirection.Normalize();

            float speed =
                Mathf.Max(
                    data.Speed,
                    _minimumPredictionSpeed);

            Vector3 velocity =
                travelDirection *
                speed;

            Vector3 estimatedPosition =
                data.Position +
                velocity *
                elapsedTime;

            bool isFalling =
                travelDirection.y <=
                _fallingDirectionThreshold;

            bool willHit;

            float timeToImpact;
            float closestDistance;
            Vector3 predictedImpactPosition;

            if (isFalling)
            {
                willHit =
                    TryPredictFallingImpact(
                        estimatedPosition,
                        velocity,
                        targetPosition,
                        out timeToImpact,
                        out closestDistance,
                        out predictedImpactPosition);
            }
            else
            {
                willHit =
                    TryPredictDirectImpact(
                        estimatedPosition,
                        velocity,
                        targetPosition,
                        out timeToImpact,
                        out closestDistance,
                        out predictedImpactPosition);
            }

            if (!willHit)
            {
                return false;
            }

            float score =
                CalculateThreatScore(
                    data,
                    timeToImpact,
                    closestDistance,
                    isFalling
                        ? _fallingHitRadius
                        : _normalHitRadius);

            prediction =
                new ImpactPrediction(
                    data.AttackerPlayerId,
                    data.IsMainWeapon,
                    data.ThreatId,
                    estimatedPosition,
                    travelDirection,
                    predictedImpactPosition,
                    Mathf.Max(
                        0.0f,
                        data.Damage),
                    timeToImpact,
                    closestDistance,
                    score);

            return true;
        }

        /// <summary>
        /// 直進する攻撃の最接近位置と時間を計算します。
        /// </summary>
        private bool TryPredictDirectImpact(
            Vector3 projectilePosition,
            Vector3 velocity,
            Vector3 targetPosition,
            out float timeToImpact,
            out float closestDistance,
            out Vector3 predictedImpactPosition)
        {
            timeToImpact =
                0.0f;

            closestDistance =
                float.MaxValue;

            predictedImpactPosition =
                projectilePosition;

            float velocitySqrMagnitude =
                velocity.sqrMagnitude;

            if (velocitySqrMagnitude <
                k_VectorEpsilon)
            {
                return false;
            }

            Vector3 toTarget =
                targetPosition -
                projectilePosition;

            timeToImpact =
                Vector3.Dot(
                    toTarget,
                    velocity) /
                velocitySqrMagnitude;

            if (timeToImpact <
                    0.0f ||
                timeToImpact >
                    _maximumPredictionTime)
            {
                return false;
            }

            predictedImpactPosition =
                projectilePosition +
                velocity *
                timeToImpact;

            closestDistance =
                Vector3.Distance(
                    predictedImpactPosition,
                    targetPosition);

            return closestDistance <=
                _normalHitRadius;
        }

        /// <summary>
        /// 上空から落下する攻撃がユニットの高さへ
        /// 到達する位置と時間を計算します。
        /// </summary>
        private bool TryPredictFallingImpact(
            Vector3 projectilePosition,
            Vector3 velocity,
            Vector3 targetPosition,
            out float timeToImpact,
            out float horizontalDistance,
            out Vector3 predictedImpactPosition)
        {
            timeToImpact =
                0.0f;

            horizontalDistance =
                float.MaxValue;

            predictedImpactPosition =
                projectilePosition;

            if (velocity.y >=
                -k_VectorEpsilon)
            {
                return false;
            }

            timeToImpact =
                (targetPosition.y -
                projectilePosition.y) /
                velocity.y;

            if (timeToImpact <
                    0.0f ||
                timeToImpact >
                    _maximumPredictionTime)
            {
                return false;
            }

            predictedImpactPosition =
                projectilePosition +
                velocity *
                timeToImpact;

            Vector2 predictedPositionXZ =
                new(
                    predictedImpactPosition.x,
                    predictedImpactPosition.z);

            Vector2 targetPositionXZ =
                new(
                    targetPosition.x,
                    targetPosition.z);

            horizontalDistance =
                Vector2.Distance(
                    predictedPositionXZ,
                    targetPositionXZ);

            return horizontalDistance <=
                _fallingHitRadius;
        }

        /// <summary>
        /// 攻撃の威力、到達時間、命中中心からの距離および
        /// 追尾性能から危険度を計算します。
        /// </summary>
        private float CalculateThreatScore(
            ThreatData data,
            float timeToImpact,
            float closestDistance,
            float hitRadius)
        {
            float validDamage =
                Mathf.Max(
                    0.0f,
                    data.Damage);

            float timeScore =
                validDamage /
                Mathf.Max(
                    timeToImpact,
                    k_MinimumScoreTime);

            float proximityRate =
                hitRadius >
                    k_VectorEpsilon
                    ? 1.0f -
                    Mathf.Clamp01(
                        closestDistance /
                        hitRadius)
                    : 0.0f;

            float score =
                timeScore *
                (1.0f +
                proximityRate *
                0.5f);

            if (data.IsHoming)
            {
                score *=
                    _homingScoreMultiplier;
            }

            if (data.IsTracking)
            {
                score *=
                    _trackingScoreMultiplier;
            }

            return score;
        }

        /// <summary>
        /// 命中予測を既存クラスタへ追加するか、
        /// 新しいクラスタとして登録します。
        /// </summary>
        private void AddPredictionToCluster(
            ImpactPrediction prediction)
        {
            foreach (ThreatCluster cluster in
                    _currentClusters)
            {
                float directionSimilarity =
                    Vector3.Dot(
                        cluster.AverageDirection,
                        prediction.TravelDirection);

                if (directionSimilarity <
                    _clusterDirectionSimilarity)
                {
                    continue;
                }

                cluster.Add(
                    prediction);

                return;
            }

            ThreatCluster newCluster =
                new();

            newCluster.Add(
                prediction);

            _currentClusters.Add(
                newCluster);
        }

        /// <summary>
        /// 合計危険度が最も高いクラスタを選択します。
        /// </summary>
        private void SelectMostDangerousCluster()
        {
            float highestScore =
                float.NegativeInfinity;

            foreach (ThreatCluster cluster in
                    _currentClusters)
            {
                if (cluster.TotalScore <=
                    highestScore)
                {
                    continue;
                }

                highestScore =
                    cluster.TotalScore;

                MostDangerousCluster =
                    cluster;
            }
        }

        /// <summary>
        /// 攻撃予測を実行できる状態か確認します。
        /// </summary>
        private bool CanEvaluatePrediction()
        {
            if (_unitBehavior == null)
            {
                return false;
            }

            UnitStatus unitStatus =
                _unitBehavior.GetUnitStatus();

            if (unitStatus == null ||
                unitStatus.IsDead)
            {
                return false;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return false;
            }

            return unitManager.CanExecuteUnitLogic(
                unitStatus.PlayerId);
        }

        /// <summary>
        /// 命中判定に使用するユニット位置を取得します。
        /// </summary>
        private Vector3 GetPredictionTargetPosition()
        {
            Transform unitTransform =
                _unitBehavior != null
                    ? _unitBehavior.transform
                    : transform;

            return unitTransform.position +
                Vector3.up *
                _targetHeightOffset;
        }

        /// <summary>
        /// 通知された攻撃情報が予測に使用できるか確認します。
        /// </summary>
        private static bool IsValidThreat(
            ThreatData data)
        {
            if (data.AttackerPlayerId <
                0)
            {
                return false;
            }

            if (data.Direction.sqrMagnitude <
                k_VectorEpsilon)
            {
                return false;
            }

            if (data.Speed <=
                0.0f)
            {
                return false;
            }

            return data.Damage >
                0.0f;
        }

        /// <summary>
        /// クラスタリング結果を削除します。
        /// </summary>
        private void ClearPredictionResults()
        {
            _currentClusters.Clear();

            MostDangerousCluster =
                null;
        }

        private readonly struct ThreatKey :
    IEquatable<ThreatKey>
        {
            public int AttackerPlayerId
            {
                get;
            }

            public bool IsMainWeapon
            {
                get;
            }

            public int ThreatId
            {
                get;
            }

            public ThreatKey(
    int attackerPlayerId,
    bool isMainWeapon,
    int threatId)
            {
                AttackerPlayerId =
                    attackerPlayerId;

                IsMainWeapon =
                    isMainWeapon;

                ThreatId =
                    threatId;
            }

            /// <summary>
            /// 別の攻撃キーと同一かどうかを確認します。
            /// </summary>
            public bool Equals(
                ThreatKey other)
            {
                return AttackerPlayerId ==
                        other.AttackerPlayerId &&
                    IsMainWeapon ==
                        other.IsMainWeapon &&
                    ThreatId ==
                        other.ThreatId;
            }

            /// <summary>
            /// オブジェクトが同一の攻撃キーかどうかを確認します。
            /// </summary>
            public override bool Equals(
                object obj)
            {
                return obj is ThreatKey other &&
                    Equals(
                        other);
            }

            /// <summary>
            /// Dictionaryで使用するハッシュ値を取得します。
            /// </summary>
            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode =
                        17;

                    hashCode =
                        hashCode *
                        31 +
                        AttackerPlayerId;

                    hashCode =
                        hashCode *
                        31 +
                        IsMainWeapon.GetHashCode();

                    hashCode =
                        hashCode *
                        31 +
                        ThreatId;

                    return hashCode;
                }
            }
        }

        /// <summary>
        /// 弾単位の最新攻撃情報を保持します。
        /// </summary>
        private sealed class ThreatState
        {
            public ThreatData Data
            {
                get;
                private set;
            }

            public ThreatState(
    ThreatData data)
            {
                Data =
                    data;
            }

            /// <summary>
            /// 攻撃情報を最新状態へ更新します。
            /// </summary>
            public void Update(
                ThreatData data)
            {
                Data =
                    data;
            }
        }

        internal readonly struct ImpactPrediction
        {
            public int AttackerPlayerId
            {
                get;
            }

            public bool IsMainWeapon
            {
                get;
            }

            public int ThreatId
            {
                get;
            }

            public Vector3 SourcePosition
            {
                get;
            }

            public Vector3 TravelDirection
            {
                get;
            }

            public Vector3 PredictedImpactPosition
            {
                get;
            }

            public float Damage
            {
                get;
            }

            public float TimeToImpact
            {
                get;
            }

            public float ClosestDistance
            {
                get;
            }

            public float Score
            {
                get;
            }

            public ImpactPrediction(
    int attackerPlayerId,
    bool isMainWeapon,
    int threatId,
    Vector3 sourcePosition,
    Vector3 travelDirection,
    Vector3 predictedImpactPosition,
    float damage,
    float timeToImpact,
    float closestDistance,
    float score)
            {
                AttackerPlayerId =
                    attackerPlayerId;

                IsMainWeapon =
                    isMainWeapon;

                ThreatId =
                    threatId;

                SourcePosition =
                    sourcePosition;

                TravelDirection =
                    travelDirection;

                PredictedImpactPosition =
                    predictedImpactPosition;

                Damage =
                    damage;

                TimeToImpact =
                    timeToImpact;

                ClosestDistance =
                    closestDistance;

                Score =
                    score;
            }
        }

        /// <summary>
        /// 近い方向から到来する複数の攻撃をまとめた
        /// 防御判断用クラスタです。
        /// </summary>
        public sealed class ThreatCluster
        {
            /// <summary>
            /// 平均進行方向の計算に使用する方向合計です。
            /// </summary>
            private Vector3 _directionSum;

            /// <summary>
            /// 中心位置の計算に使用する弾位置合計です。
            /// </summary>
            private Vector3 _sourcePositionSum;

            /// <summary>
            /// 予測命中位置の計算に使用する位置合計です。
            /// </summary>
            private Vector3 _impactPositionSum;

            public Vector3 AverageDirection
            {
                get;
                private set;
            }

            public Vector3 CenterPosition
            {
                get;
                private set;
            }

            public Vector3 PredictedImpactPosition
            {
                get;
                private set;
            }

            public float TotalDamage
            {
                get;
                private set;
            }

            public float TotalScore
            {
                get;
                private set;
            }

            public float NearestTimeToImpact
            {
                get;
                private set;
            } = float.MaxValue;

            public int Count
            {
                get;
                private set;
            }

            /// <summary>
            /// ユニットから攻撃クラスタ中心への方向を取得します。
            /// </summary>
            /// <param name="unitPosition">
            /// 防御するユニットの位置です。
            /// </param>
            /// <returns>
            /// ユニットから攻撃クラスタへ向かう正規化方向です。
            /// </returns>
            public Vector3 GetDirectionFromUnit(
                Vector3 unitPosition)
            {
                Vector3 direction =
                    CenterPosition -
                    unitPosition;

                if (direction.sqrMagnitude <
                    k_VectorEpsilon)
                {
                    return -AverageDirection;
                }

                return direction.normalized;
            }

            /// <summary>
            /// クラスタへ命中予測を追加します。
            /// </summary>
            internal void Add(
                ImpactPrediction prediction)
            {
                _directionSum +=
                    prediction.TravelDirection;

                _sourcePositionSum +=
                    prediction.SourcePosition;

                _impactPositionSum +=
                    prediction.PredictedImpactPosition;

                Count++;

                AverageDirection =
                    _directionSum.sqrMagnitude >
                        k_VectorEpsilon
                        ? _directionSum.normalized
                        : prediction.TravelDirection;

                CenterPosition =
                    _sourcePositionSum /
                    Count;

                PredictedImpactPosition =
                    _impactPositionSum /
                    Count;

                TotalDamage +=
                    prediction.Damage;

                TotalScore +=
                    prediction.Score;

                NearestTimeToImpact =
                    Mathf.Min(
                        NearestTimeToImpact,
                        prediction.TimeToImpact);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の設定値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _threatLifetime =
                Mathf.Max(
                    0.0f,
                    _threatLifetime);

            _recentNotificationWindow =
                Mathf.Clamp(
                    _recentNotificationWindow,
                    0.0f,
                    _threatLifetime);

            _maximumPredictionTime =
                Mathf.Max(
                    0.0f,
                    _maximumPredictionTime);

            _normalHitRadius =
                Mathf.Max(
                    0.0f,
                    _normalHitRadius);

            _fallingHitRadius =
                Mathf.Max(
                    0.0f,
                    _fallingHitRadius);

            _fallingDirectionThreshold =
                Mathf.Clamp(
                    _fallingDirectionThreshold,
                    -1.0f,
                    0.0f);

            _minimumPredictionSpeed =
                Mathf.Max(
                    0.01f,
                    _minimumPredictionSpeed);

            _targetHeightOffset =
                Mathf.Max(
                    0.0f,
                    _targetHeightOffset);

            _clusterDirectionSimilarity =
                Mathf.Clamp(
                    _clusterDirectionSimilarity,
                    -1.0f,
                    1.0f);

            _homingScoreMultiplier =
                Mathf.Max(
                    0.0f,
                    _homingScoreMultiplier);

            _trackingScoreMultiplier =
                Mathf.Max(
                    0.0f,
                    _trackingScoreMultiplier);
        }
#endif
    }
}
