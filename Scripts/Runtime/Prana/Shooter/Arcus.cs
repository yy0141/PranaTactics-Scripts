using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Match.Phases;
using PranaTactics.Unit;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 対象上空まで放物線状に移動した後、
    /// 対象へ向かって下降する射出型プラーナです。
    /// </summary>
    /// <remarks>
    /// 飛行処理は、上昇、放物線移動、下降の
    /// 3段階で構成されます。
    ///
    /// 拡散を含む最終目標位置は生成時に確定し、
    /// 対象ユニットが移動した場合でも飛行中は変更しません。
    /// </remarks>
    public sealed class Arcus : ShooterPranaBase
    {
        /// <summary>
        /// 有効な方向ベクトルと判断する最小二乗長です。
        /// </summary>
        private const float k_MinimumDirectionSqrMagnitude =
            0.0001f;

        /// <summary>
        /// 放物線の長さがほぼ0であると判断する値です。
        /// </summary>
        private const float k_MinimumArcLength =
            0.001f;

        /// <summary>
        /// 壁レイヤーの名前です。
        /// </summary>
        private const string k_WallLayerName =
            "Wall";

        private enum FlightState
        {
            ScatterDelay,

            Scattering,

            Aiming,

            Rising,

            Arcing,

            Falling,

            Finished
        }

        /// <summary>
        /// 壁の最高地点へ追加する最低飛行高度です。
        /// </summary>
        [Header("Flight Height")]
        [Min(0.0f)]
        [SerializeField]
        private float _minimumClearanceHeight =
            25.0f;

        /// <summary>
        /// 水平移動距離から放物線の高さを算出する倍率です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _arcHeightDistanceRate =
            0.3f;

        /// <summary>
        /// 放物線へ適用する最低追加高度です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _minimumArcHeight =
            90.0f;

        /// <summary>
        /// 放物線へ適用する最大追加高度です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _maximumArcHeight =
            150.0f;

        /// <summary>
        /// 制御点の高さへ適用する倍率です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _arcControlHeightMultiplier =
            2.0f;

        /// <summary>
        /// 上昇先へ到達したと判断する高さの許容差です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _riseArrivalTolerance =
            0.05f;

        /// <summary>
        /// 下降時に目標位置を通過してから
        /// 追加で移動できる距離です。
        /// </summary>
        [Header("Fall")]
        [Min(0.0f)]
        [SerializeField]
        private float _fallOvershootDistance =
            10.0f;

        /// <summary>
        /// 放物線長の近似に使用する分割数です。
        /// </summary>
        [Header("Arc Approximation")]
        [Min(2)]
        [SerializeField]
        private int _arcSampleCount =
            40;

        /// <summary>
        /// 移動中の壁衝突判定に使用する球の半径です。
        /// </summary>
        [Header("Collision")]
        [Min(0.0f)]
        [SerializeField]
        private float _collisionSphereRadius =
            0.2f;

        /// <summary>
        /// 現在の飛行段階です。
        /// </summary>
        private FlightState _flightState =
            FlightState.ScatterDelay;

        /// <summary>
        /// 拡散を含めて確定した最終目標位置が
        /// 存在するかどうかを示します。
        /// </summary>
        private bool _hasResolvedTargetPosition;

        /// <summary>
        /// 上昇後に使用する基準飛行高度です。
        /// </summary>
        private float _flightHeight;

        /// <summary>
        /// 放物線へ適用する追加高度です。
        /// </summary>
        private float _arcHeight;

        /// <summary>
        /// 放物線の開始位置です。
        /// </summary>
        private Vector3 _arcStart;

        /// <summary>
        /// 放物線の終了位置です。
        /// </summary>
        private Vector3 _arcEnd;

        /// <summary>
        /// 二次ベジェ曲線の制御点です。
        /// </summary>
        private Vector3 _arcControl;

        /// <summary>
        /// 放物線上を移動した累積距離です。
        /// </summary>
        private float _arcMoveDistance;

        /// <summary>
        /// 近似した放物線の全長です。
        /// </summary>
        private float _arcTotalLength;

        /// <summary>
        /// 放物線上の累積距離を保持するテーブルです。
        /// </summary>
        private float[] _arcLengthTable;

        /// <summary>
        /// 下降時に使用する進行方向です。
        /// </summary>
        private Vector3 _fallDirection;

        /// <summary>
        /// 下降開始後に移動した累積距離です。
        /// </summary>
        private float _fallMoveDistance;

        /// <summary>
        /// 下降中に移動できる最大距離です。
        /// </summary>
        private float _fallMaximumDistance;

        /// <summary>
        /// 壁判定に使用するLayerMaskです。
        /// </summary>
        private int _wallLayerMask;

        /// <summary>
        /// Shooter系プラーナの共通初期化完了後に、
        /// Arcus固有の飛行情報を初期化します。
        /// </summary>
        protected override void OnShooterInitialized()
        {
            base.OnShooterInitialized();

            ResetFlightState();

            _wallLayerMask =
                LayerMask.GetMask(
                    k_WallLayerName);

            if (!TryResolveTargetPosition())
            {
                Debug.LogWarning(
                    $"{nameof(Arcus)}: " +
                    "攻撃対象位置を取得できないため、" +
                    "プラーナを破棄します。" +
                    $" TargetPlayerId={Context?.TargetPlayerId ?? -1}",
                    this);

                DestroyLocally(
                    false);

                return;
            }

            ResetElapsedTime();

            if (UsesScatter)
            {
                if (!TrySetCommonScatterPosition())
                {
                    Debug.LogWarning(
                        $"{nameof(Arcus)}: " +
                        "散開位置を設定できないため、" +
                        "プラーナを破棄します。",
                        this);

                    DestroyLocally(
                        false);

                    return;
                }

                IsScattered = false;
                _flightState =
                    FlightState.ScatterDelay;

                return;
            }

            IsScattered = true;

            if (!PlayFireSoundOnce())
            {
                return;
            }
            BeginFlight();
        }

        /// <summary>
        /// Arcusの飛行状態を更新します。
        /// </summary>
        protected override void Update()
        {
            if (!IsInitialized ||
                _flightState ==
                FlightState.Finished)
            {
                return;
            }

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager != null &&
                phaseManager.CurrentPhase !=
                MatchPhase.Micro)
            {
                DestroyLocally(
                    false);

                return;
            }

            FollowPreparationOwner();

            ElapsedTime +=
                Time.deltaTime;

            switch (_flightState)
            {
                case FlightState.ScatterDelay:
                    UpdateScatterDelay();
                    break;

                case FlightState.Scattering:
                    UpdateScattering();
                    break;

                case FlightState.Aiming:
                    UpdateAiming();
                    break;

                case FlightState.Rising:
                    UpdateRise();
                    break;

                case FlightState.Arcing:
                    UpdateArc();
                    break;

                case FlightState.Falling:
                    UpdateFall();

                    if (_flightState ==
                        FlightState.Falling)
                    {
                        SendThreat(
                            false);
                    }

                    break;

                case FlightState.Finished:
                    break;

                default:
                    Debug.LogWarning(
                        $"{nameof(Arcus)}: " +
                        $"未対応の飛行状態です。" +
                        $" State={_flightState}",
                        this);

                    DestroyLocally(
                        false);
                    break;
            }
        }

        /// <summary>
        /// Arcusでは現在の敵座標ではなく、
        /// 初期化時に確定した拡散済み目標位置への方向を返します。
        /// </summary>
        /// <param name="playerId">
        /// 対象プレイヤーのIDです。
        /// この実装ではフォールバック時のみ使用します。
        /// </param>
        /// <param name="spreadAngle">
        /// 拡散角度です。
        /// 初期目標位置へ既に適用済みのため使用しません。
        /// </param>
        /// <returns>
        /// 確定済み目標位置へ向かう正規化済み方向です。
        /// </returns>
        protected override Vector3 GetTargetDirection(
            int playerId,
            float spreadAngle = 0.0f)
        {
            if (_hasResolvedTargetPosition)
            {
                Vector3 direction =
                    TargetPosition -
                    transform.position;

                if (direction.sqrMagnitude >
                    k_MinimumDirectionSqrMagnitude)
                {
                    return direction.normalized;
                }
            }

            return base.GetTargetDirection(
                playerId,
                0.0f);
        }

        /// <summary>
        /// 散開を開始するまで待機します。
        /// </summary>
        private void UpdateScatterDelay()
        {
            if (ElapsedTime <
                ScatterDelay)
            {
                return;
            }

            PlayScatterSoundOnce();

            _flightState =
                FlightState.Scattering;
        }

        /// <summary>
        /// Geradeと共通の速度で散開位置まで移動します。
        /// </summary>
        private void UpdateScattering()
        {
            if (!MoveToScatterPosition())
            {
                return;
            }

            _flightState =
                FlightState.Aiming;
        }

        /// <summary>
        /// 散開完了後、Geradeと共通の時間だけ待機して
        /// Arcus固有の飛行を開始します。
        /// </summary>
        private void UpdateAiming()
        {
            if (ElapsedTime <
                AimDuration)
            {
                return;
            }

            if (!PlayFireSoundOnce())
            {
                return;
            }
            BeginFlight();
        }

        /// <summary>
        /// 発射時点の敵位置からArcusの飛行先を確定します。
        /// </summary>
        protected override bool TryPrepareLaunch()
        {
            return TryResolveTargetPosition();
        }

        /// <summary>
        /// 発射時に確定した対象位置へ向かう飛行を開始します。
        /// </summary>
        private void BeginFlight()
        {
            float maximumWallHeight =
                GetMaximumWallHeight(
                    transform.position,
                    TargetPosition);

            _flightHeight =
                maximumWallHeight +
                _minimumClearanceHeight;

            ResetElapsedTime();

            _flightState =
                FlightState.Rising;
        }

        /// <summary>
        /// 現在位置から安全飛行高度まで上昇します。
        /// </summary>
        private void UpdateRise()
        {
            Vector3 riseTarget =
                new(
                    transform.position.x,
                    _flightHeight,
                    transform.position.z);

            Vector3 nextPosition =
                Vector3.MoveTowards(
                    transform.position,
                    riseTarget,
                    GetMovementDistance());

            if (!TryMoveWithWallCollision(
                    nextPosition))
            {
                return;
            }

            if (Mathf.Abs(
                    transform.position.y -
                    _flightHeight) >
                _riseArrivalTolerance)
            {
                return;
            }

            transform.position =
                new Vector3(
                    transform.position.x,
                    _flightHeight,
                    transform.position.z);

            BeginArc();
        }

        /// <summary>
        /// 上昇完了後に放物線移動を初期化します。
        /// </summary>
        private void BeginArc()
        {
            _arcStart =
                transform.position;

            _arcEnd =
                new Vector3(
                    TargetPosition.x,
                    _flightHeight,
                    TargetPosition.z);

            float horizontalDistance =
                Vector3.Distance(
                    _arcStart,
                    _arcEnd);

            float calculatedArcHeight =
                horizontalDistance *
                _arcHeightDistanceRate;

            _arcHeight =
                Mathf.Clamp(
                    calculatedArcHeight,
                    Mathf.Min(
                        _minimumArcHeight,
                        _maximumArcHeight),
                    Mathf.Max(
                        _minimumArcHeight,
                        _maximumArcHeight));

            SetupParabola();
            BuildBezierLengthTable();

            _arcMoveDistance = 0.0f;
            _flightState = FlightState.Arcing;
        }

        /// <summary>
        /// 二次ベジェ曲線に沿って対象上空まで移動します。
        /// </summary>
        private void UpdateArc()
        {
            if (_arcTotalLength <=
                k_MinimumArcLength)
            {
                if (!TryMoveWithWallCollision(
                        _arcEnd))
                {
                    return;
                }

                BeginFall();

                return;
            }

            _arcMoveDistance +=
                GetMovementDistance();

            float parameter =
                GetParameterByDistance(
                    _arcMoveDistance);

            Vector3 nextPosition =
                GetBezierPoint(
                    _arcStart,
                    _arcControl,
                    _arcEnd,
                    parameter);

            if (!TryMoveWithWallCollision(
                    nextPosition))
            {
                return;
            }

            if (_arcMoveDistance <
                _arcTotalLength)
            {
                return;
            }

            transform.position =
                _arcEnd;

            BeginFall();
        }

        /// <summary>
        /// 対象位置へ向かう下降移動を初期化します。
        /// </summary>
        private void BeginFall()
        {
            _fallDirection =
                GetTargetDirection(
                    Context.TargetPlayerId,
                    0.0f);

            if (_fallDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                DestroyLocally(
                    false);

                return;
            }

            _fallDirection.Normalize();

            float distanceToTarget =
                Vector3.Distance(
                    transform.position,
                    TargetPosition);

            _fallMaximumDistance =
                distanceToTarget +
                _fallOvershootDistance;

            _fallMoveDistance = 0.0f;

            Direction =
                _fallDirection;

            ResetElapsedTime();

            _flightState =
                FlightState.Falling;
        }

        /// <summary>
        /// 確定済み目標位置へ向かって下降します。
        /// </summary>
        private void UpdateFall()
        {
            float movementDistance =
                GetMovementDistance();

            Vector3 nextPosition =
                transform.position +
                _fallDirection *
                movementDistance;

            if (!TryMoveWithWallCollision(
                    nextPosition))
            {
                return;
            }

            _fallMoveDistance +=
                movementDistance;

            if (_fallMoveDistance >=
                _fallMaximumDistance)
            {
                DestroyLocally(
                    false);
            }
        }

        /// <summary>
        /// 現在のフレームで移動できる距離を取得します。
        /// </summary>
        /// <returns>
        /// コンテキストに設定された速度と
        /// デルタタイムから計算した移動距離です。
        /// </returns>
        private float GetMovementDistance()
        {
            if (Context == null)
            {
                return 0.0f;
            }

            return Mathf.Max(
                    0.0f,
                    Context.Speed) *
                Time.deltaTime;
        }

        /// <summary>
        /// 指定された位置へ移動する前にSphereCastを実行し、
        /// 壁とのすり抜けを防止します。
        /// </summary>
        /// <param name="nextPosition">
        /// 移動後の予定位置です。
        /// </param>
        /// <returns>
        /// 壁へ衝突せず移動できた場合は<c>true</c>です。
        /// </returns>
        private bool TryMoveWithWallCollision(
            Vector3 nextPosition)
        {
            Vector3 movement =
                nextPosition -
                transform.position;

            float movementDistance =
                movement.magnitude;

            if (movementDistance <=
                Mathf.Epsilon)
            {
                return TryMoveProjectileWithCollision(nextPosition);
            }

            if (_wallLayerMask != 0 &&
                Physics.SphereCast(
                    transform.position,
                    Mathf.Max(
                        0.0f,
                        _collisionSphereRadius),
                    movement.normalized,
                    out RaycastHit hit,
                    movementDistance,
                    _wallLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                Vector3 wallStop = transform.position + movement.normalized * hit.distance;
                if (!TryMoveProjectileWithCollision(wallStop))
                {
                    return false;
                }

                DestroyLocally(
                    true);

                return false;
            }

            return TryMoveProjectileWithCollision(nextPosition);
        }

        /// <summary>
        /// 二次ベジェ曲線の制御点を設定します。
        /// </summary>
        private void SetupParabola()
        {
            Vector3 midpoint =
                (_arcStart +
                _arcEnd) *
                0.5f;

            _arcControl =
                new Vector3(
                    midpoint.x,
                    _flightHeight +
                    _arcHeight *
                    _arcControlHeightMultiplier,
                    midpoint.z);
        }

        /// <summary>
        /// ベジェ曲線上の累積距離テーブルを生成します。
        /// </summary>
        private void BuildBezierLengthTable()
        {
            int sampleCount =
                Mathf.Max(
                    2,
                    _arcSampleCount);

            _arcLengthTable =
                new float[
                    sampleCount + 1];

            _arcLengthTable[0] =
                0.0f;

            Vector3 previousPosition =
                _arcStart;

            float totalLength =
                0.0f;

            for (int sampleIndex = 1;
                sampleIndex <= sampleCount;
                sampleIndex++)
            {
                float parameter =
                    sampleIndex /
                    (float)sampleCount;

                Vector3 samplePosition =
                    GetBezierPoint(
                        _arcStart,
                        _arcControl,
                        _arcEnd,
                        parameter);

                totalLength +=
                    Vector3.Distance(
                        previousPosition,
                        samplePosition);

                _arcLengthTable[sampleIndex] =
                    totalLength;

                previousPosition =
                    samplePosition;
            }

            _arcTotalLength =
                totalLength;
        }

        /// <summary>
        /// 放物線上を進んだ距離から、
        /// 対応するベジェ曲線のパラメータを取得します。
        /// </summary>
        /// <param name="distance">
        /// 放物線上を進んだ距離です。
        /// </param>
        /// <returns>
        /// 0から1までのベジェ曲線パラメータです。
        /// </returns>
        private float GetParameterByDistance(
            float distance)
        {
            if (distance <= 0.0f ||
                _arcLengthTable == null ||
                _arcLengthTable.Length < 2)
            {
                return 0.0f;
            }

            if (distance >=
                _arcTotalLength)
            {
                return 1.0f;
            }

            int sampleCount =
                _arcLengthTable.Length - 1;

            for (int sampleIndex = 1;
                sampleIndex <
                _arcLengthTable.Length;
                sampleIndex++)
            {
                float nextLength =
                    _arcLengthTable[sampleIndex];

                if (nextLength <
                    distance)
                {
                    continue;
                }

                float previousLength =
                    _arcLengthTable[
                        sampleIndex - 1];

                float segmentLength =
                    nextLength -
                    previousLength;

                if (segmentLength <=
                    k_MinimumArcLength)
                {
                    return sampleIndex /
                        (float)sampleCount;
                }

                float segmentRate =
                    (distance -
                    previousLength) /
                    segmentLength;

                float previousParameter =
                    (sampleIndex - 1) /
                    (float)sampleCount;

                float nextParameter =
                    sampleIndex /
                    (float)sampleCount;

                return Mathf.Lerp(
                    previousParameter,
                    nextParameter,
                    segmentRate);
            }

            return 1.0f;
        }

        /// <summary>
        /// 二次ベジェ曲線上の座標を計算します。
        /// </summary>
        /// <param name="start">
        /// 曲線の開始位置です。
        /// </param>
        /// <param name="control">
        /// 曲線の制御点です。
        /// </param>
        /// <param name="end">
        /// 曲線の終了位置です。
        /// </param>
        /// <param name="parameter">
        /// 0から1までの曲線パラメータです。
        /// </param>
        /// <returns>
        /// 指定されたパラメータに対応する座標です。
        /// </returns>
        private static Vector3 GetBezierPoint(
            Vector3 start,
            Vector3 control,
            Vector3 end,
            float parameter)
        {
            float clampedParameter =
                Mathf.Clamp01(
                    parameter);

            float inverseParameter =
                1.0f -
                clampedParameter;

            return
                inverseParameter *
                inverseParameter *
                start +
                2.0f *
                inverseParameter *
                clampedParameter *
                control +
                clampedParameter *
                clampedParameter *
                end;
        }

        /// <summary>
        /// 対象との間にある壁のうち、
        /// 最も高い地点のY座標を取得します。
        /// </summary>
        /// <param name="start">
        /// 高さ検索を開始する位置です。
        /// </param>
        /// <param name="target">
        /// 高さ検索を終了する位置です。
        /// </param>
        /// <returns>
        /// 始点、終点および検出した壁の中で
        /// 最も高いY座標です。
        /// </returns>
        private float GetMaximumWallHeight(
            Vector3 start,
            Vector3 target)
        {
            Vector3 direction =
                target -
                start;

            float distance =
                direction.magnitude;

            float maximumHeight =
                Mathf.Max(
                    start.y,
                    target.y);

            if (distance <=
                    k_MinimumArcLength ||
                _wallLayerMask == 0)
            {
                return maximumHeight;
            }

            RaycastHit[] hits =
                Physics.RaycastAll(
                    start,
                    direction.normalized,
                    distance,
                    _wallLayerMask,
                    QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                float wallTop =
                    hit.collider.bounds.max.y;

                maximumHeight =
                    Mathf.Max(
                        maximumHeight,
                        wallTop);
            }

            return maximumHeight;
        }

        /// <summary>
        /// 攻撃対象の現在位置を取得し、
        /// 水平方向の拡散を一度だけ適用します。
        /// </summary>
        /// <returns>
        /// 有効な目標位置を確定できた場合は<c>true</c>です。
        /// </returns>
        private bool TryResolveTargetPosition()
        {
            if (Context == null)
            {
                return false;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return false;
            }

            GameObject targetUnitObject =
                unitManager.GetUnitObject(
                    Context.TargetPlayerId);

            if (targetUnitObject == null ||
                !targetUnitObject.TryGetComponent(
                    out UnitBehavior targetUnit))
            {
                return false;
            }

            MicroUnitBehavior targetMicroUnit =
                targetUnit.GetMicroUnitBehavior();

            if (targetMicroUnit == null)
            {
                return false;
            }

            Vector3 targetPosition =
                targetMicroUnit.transform.position;

            TargetPosition =
                ApplyHorizontalSpread(
                    targetPosition,
                    Context.SpreadAngle);

            _hasResolvedTargetPosition =
                true;

            return true;
        }

        /// <summary>
        /// 対象位置へ水平方向の拡散を適用します。
        /// </summary>
        /// <param name="targetPosition">
        /// 拡散前の対象位置です。
        /// </param>
        /// <param name="spreadAngle">
        /// 左右を合わせた拡散角度です。
        /// </param>
        /// <returns>
        /// 拡散後の対象位置です。
        /// </returns>
        private Vector3 ApplyHorizontalSpread(
            Vector3 targetPosition,
            float spreadAngle)
        {
            if (spreadAngle <= 0.0f)
            {
                return targetPosition;
            }

            Vector3 direction =
                targetPosition -
                transform.position;

            if (direction.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return targetPosition;
            }

            float targetDistance =
                direction.magnitude;

            direction.Normalize();

            float randomYaw =
                Random.Range(
                    -spreadAngle * 0.5f,
                    spreadAngle * 0.5f);

            Vector3 spreadDirection =
                Quaternion.AngleAxis(
                    randomYaw,
                    Vector3.up) *
                direction;

            return transform.position +
                spreadDirection.normalized *
                targetDistance;
        }

        /// <summary>
        /// Arcusの飛行状態を初期値へ戻します。
        /// </summary>
        private void ResetFlightState()
        {
            _flightState =
                FlightState.ScatterDelay;

            _hasResolvedTargetPosition =
                false;

            _flightHeight = 0.0f;
            _arcHeight = 0.0f;

            _arcStart = Vector3.zero;
            _arcEnd = Vector3.zero;
            _arcControl = Vector3.zero;

            _arcMoveDistance = 0.0f;
            _arcTotalLength = 0.0f;
            _arcLengthTable = null;

            _fallDirection = Vector3.zero;
            _fallMoveDistance = 0.0f;
            _fallMaximumDistance = 0.0f;

            Direction =
                Vector3.zero;

            ResetElapsedTime();
        }

        /// <summary>
        /// Arcusをローカル環境から破棄します。
        /// </summary>
        /// <param name="playHitSound">
        /// 壁へ衝突した効果音を再生する場合は<c>true</c>です。
        /// </param>
        private void DestroyLocally(
            bool playHitSound)
        {
            if (_flightState ==
                FlightState.Finished)
            {
                return;
            }

            _flightState =
                FlightState.Finished;

            if (playHitSound && _hasPlayedFireSound)
            {
                RequestPranaSound(
                    PranamSoundEvent.Hit,
                    transform.position);
            }

            base.DestroyLocally();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorから設定された値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _minimumClearanceHeight =
                Mathf.Max(
                0.0f,
                _minimumClearanceHeight);

            _arcHeightDistanceRate =
                Mathf.Max(
                    0.0f,
                    _arcHeightDistanceRate);

            _minimumArcHeight =
                Mathf.Max(
                    0.0f,
                    _minimumArcHeight);

            _maximumArcHeight =
                Mathf.Max(
                    0.0f,
                    _maximumArcHeight);

            _arcControlHeightMultiplier =
                Mathf.Max(
                    0.0f,
                    _arcControlHeightMultiplier);

            _riseArrivalTolerance =
                Mathf.Max(
                    0.0f,
                    _riseArrivalTolerance);

            _fallOvershootDistance =
                Mathf.Max(
                    0.0f,
                    _fallOvershootDistance);

            _arcSampleCount =
                Mathf.Max(
                    2,
                    _arcSampleCount);

            _collisionSphereRadius =
                Mathf.Max(
                    0.0f,
                    _collisionSphereRadius);
        }
#endif
    }
}
