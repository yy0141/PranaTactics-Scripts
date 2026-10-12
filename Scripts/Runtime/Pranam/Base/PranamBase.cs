using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

using PranaTactics.Match.Phases;
using PranaTactics.Pranam.Targeting;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// プラーナムの使用、攻撃範囲の生成、
    /// 対象ユニットの検出およびハイライト表示を管理する基底クラスです。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public abstract class PranamBase : MonoBehaviourPunCallbacks
    {
        /// <summary>
        /// 扇形メッシュの初期角度です。
        /// </summary>
        private const float k_DefaultArcAngle = 90.0f;

        /// <summary>
        /// 扇形メッシュの標準品質です。
        /// </summary>
        private const int k_DefaultArcQuality = 100;

        /// <summary>
        /// 初期生成直後に扇形状態を再検証するフレーム数です。
        /// </summary>
        /// <remarks>
        /// Start、Animator、フェイズ切り替えなどが初期化後に
        /// TransformやMeshを上書きした場合も復元します。
        /// </remarks>
        private const int k_ArcStabilizationFrameCount = 16;

        /// <summary>
        /// ターゲットの再走査を行う標準間隔です。
        /// </summary>
        private const float k_DefaultScanInterval = 0.5f;

        /// <summary>
        /// ターゲット走査間隔の最小値です。
        /// </summary>
        private const float k_MinimumScanInterval = 0.05f;

        /// <summary>
        /// ベクトルを有効と判断する最小二乗距離です。
        /// </summary>
        private const float k_MinimumDirectionSqrMagnitude = 0.0001f;

        /// <summary>
        /// 射程境界の浮動小数点誤差を吸収する許容値です。
        /// </summary>
        private const float k_RangeTolerance = 0.001f;

        /// <summary>
        /// Pranam表示オブジェクトの最小スケールです。
        /// </summary>
        private const float k_PranamVisualScale = 0.1f;

        /// <summary>
        /// 動的に生成する扇形メッシュの名前です。
        /// </summary>
        private const string k_ArcMeshName = "PranamArcMesh";

        /// <summary>
        /// このプラーナムを所有するプレイヤーIDです。
        /// </summary>
        protected int _playerId;

        /// <summary>
        /// このプラーナムを所有するユニットです。
        /// </summary>
        protected UnitBehavior _unitBehavior;

        /// <summary>
        /// 所有ユニットのタクティカルマップ表現です。
        /// </summary>
        protected TacticalMapUnitBehavior _mapUnitBehavior;

        /// <summary>
        /// プラーナムと対象ハイライトを管理するコンポーネントです。
        /// </summary>
        protected PranamManager _pranamManager;

        /// <summary>
        /// このプラーナムに対応する実行時動作です。
        /// </summary>
        protected PranamBehavior _pranamBehavior;

        /// <summary>
        /// メインスロットへ装備されているかどうかを示します。
        /// </summary>
        protected bool _isMain;

        /// <summary>
        /// メインまたはサブ内でのスロット番号です。
        /// </summary>
        protected int _number;

        /// <summary>
        /// 初期化後に経過した時間です。
        /// </summary>
        protected float _time;

        /// <summary>
        /// 生成された検出範囲のワールド半径です。
        /// </summary>
        /// <remarks>
        /// 従来との互換性のため名前を維持していますが、
        /// 実際に保持する値は直径ではなく半径です。
        /// </remarks>
        protected float _diameter;

        /// <summary>
        /// 初期化が完了しているかどうかを示します。
        /// </summary>
        protected bool _isInitialized;

        /// <summary>
        /// ターゲット検出を再実行する時間間隔です。
        /// </summary>
        [Header("Target Detection")]
        [Min(k_MinimumScanInterval)]
        [SerializeField]
        private float _scanInterval = k_DefaultScanInterval;

        /// <summary>
        /// 現在の扇形検出範囲の角度です。
        /// </summary>
        private float _angle = k_DefaultArcAngle;

        /// <summary>
        /// 球形検出範囲内に存在する敵ユニットです。
        /// </summary>
        private readonly HashSet<UnitBehavior> _candidates = new();

        /// <summary>
        /// 球形範囲内かつ扇形範囲内に存在する敵ユニットです。
        /// </summary>
        private readonly HashSet<UnitBehavior> _unitsWithinArc = new();

        /// <summary>
        /// 現在ハイライトされている対象ユニットです。
        /// </summary>
        private readonly HashSet<UnitBehavior> _highlightedTargets = new();

        /// <summary>
        /// 攻撃条件を満たす候補と現在の距離です。
        /// </summary>
        private readonly List<AttackTargetCandidate> _attackTargetCandidates = new();

        /// <summary>
        /// 攻撃と表示が共有する対象選択結果です。
        /// </summary>
        private readonly HashSet<UnitBehavior> _attackTargets = new();

        protected virtual IAttackTargetSelection TargetSelection => AllAttackTargetSelection.Instance;

        /// <summary>
        /// 次のターゲット走査までの経過時間です。
        /// </summary>
        private float _scanTimer;

        /// <summary>
        /// 初期ターゲット走査を完了しているかどうかを示します。
        /// </summary>
        private bool _hasPerformedInitialScan;

        /// <summary>
        /// ターゲット検出に使用するSphereColliderです。
        /// </summary>
        private SphereCollider _detectionCollider;

        /// <summary>
        /// 所有MicroUnitの視認判定を担当するセンサーです。
        /// </summary>
        private VisionSensor _visionSensor;

        /// <summary>
        /// このコンポーネントが動的に生成した扇形メッシュです。
        /// </summary>
        private Mesh _generatedArcMesh;

        /// <summary>
        /// 扇形を生成する対象オブジェクトです。
        /// </summary>
        private GameObject _arcTargetObject;

        /// <summary>
        /// 扇形生成時に使用する初期角度です。
        /// </summary>
        private int _arcStartAngle;

        /// <summary>
        /// 扇形生成時に使用する範囲角度です。
        /// </summary>
        private float _arcAreaAngle;

        /// <summary>
        /// 扇形生成時に使用する範囲半径です。
        /// </summary>
        private float _arcDiameter;

        /// <summary>
        /// 扇形生成時に使用する品質です。
        /// </summary>
        private int _arcQuality =
            k_DefaultArcQuality;

        /// <summary>
        /// 派生クラスから扇形生成が要求されたかどうかです。
        /// </summary>
        private bool _hasArcInitializationRequest;

        /// <summary>
        /// 初期生成後に扇形状態を再検証する残りフレーム数です。
        /// </summary>
        private int _arcStabilizationFramesRemaining;

        public bool IsRuntimeInitialized
        {
            get
            {
                return _isInitialized;
            }
        }

        public bool IsArcInitialized
        {
            get;
            private set;
        }

        public bool IsRuntimeReady
        {
            get
            {
                return _isInitialized &&
                    (
                        !RequiresArcInitialization ||
                        (
                            IsArcInitialized &&
                            IsArcRuntimeStateValid()
                        )
                    );
            }
        }

        public bool RequiresArcInitialization
        {
            get
            {
                return _hasArcInitializationRequest;
            }
        }

        public bool IsMain
        {
            get
            {
                return _isMain;
            }
        }

        public int Number
        {
            get
            {
                return _number;
            }
        }

        public virtual bool IsContinuousUseActive => false;

        public virtual bool CanUseHold
        {
            get
            {
                return false;
            }
        }

        public bool HasHighlightedTarget
        {
            get
            {
                RefreshAttackTargets();

                return _highlightedTargets.Count > 0;
            }
        }

        /// <summary>
        /// このPranamの所有MicroUnitが，
        /// VisionSensorによって対象を視認しているか判定します。
        /// </summary>
        public bool CanSeeTarget(
            UnitBehavior targetUnit)
        {
            if (!TryGetVisionSensor(
                    out VisionSensor visionSensor) ||
                !TryGetMicroUnit(
                    targetUnit,
                    out MicroUnitBehavior targetMicroUnit))
            {
                return false;
            }

            return visionSensor.CanSeeUnitNow(
                targetMicroUnit);
        }

        /// <summary>
        /// マップ追跡型の兵装が、所有チームに発見された敵を対象にできるか判定します。
        /// </summary>
        /// <remarks>
        /// 非ステルスの敵はマップ情報で攻撃できます。ステルス中の敵も、
        /// 所有者が現在視認しているか、所有チームに視認共有されていれば対象にします。
        /// ローカルプレイヤー向けの表示状態は他チームAIの判定へ流用しません。
        /// </remarks>
        protected bool CanTargetMapUnit(UnitBehavior targetUnit)
        {
            if (_unitBehavior == null || targetUnit == null ||
                targetUnit.GetTacticalMapUnitBehavior() == null ||
                targetUnit.GetMicroUnitBehavior() == null ||
                targetUnit.GetUnitStatus() == null || !targetUnit.GetUnitStatus().IsAlive ||
                targetUnit.GetTeamId() == _unitBehavior.GetTeamId())
            {
                return false;
            }

            if (!targetUnit.IsStealthActive || CanSeeTarget(targetUnit))
            {
                return true;
            }

            UnitManager unitManager = UnitManager.Instance;

            return unitManager != null && unitManager.IsUnitVisibleToTeam(
                _unitBehavior.GetTeamId(), targetUnit.PlayerId);
        }

        public virtual bool RequiresInput
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// 動的追加されたPranamを初期化する前に、
        /// 非アクティブな親階層を含めて必要な参照を取得します。
        /// </summary>
        /// <remarks>
        /// <see cref="OnEnable"/>が呼ばれるタイミングに
        /// 初期化結果が依存しないよう、
        /// <see cref="TacticalMapUnitBehavior"/>から
        /// <see cref="Initialize"/>より前に呼び出します。
        /// </remarks>
        public void PrepareRuntimeInitialization()
        {
            CacheReferences();
        }

        protected IReadOnlyCollection<UnitBehavior> AttackTargets
        {
            get
            {
                RefreshAttackTargets();

                return _attackTargets;
            }
        }

        protected IReadOnlyCollection<UnitBehavior> UnitsWithinArc
        {
            get
            {
                CleanupInvalidUnits();

                return _unitsWithinArc;
            }
        }

        /// <summary>
        /// プラーナムを初期化します。
        /// </summary>
        /// <param name="playerId">
        /// 所有プレイヤーのIDです。
        /// </param>
        /// <param name="isMain">
        /// メインスロットへ装備されている場合は<c>true</c>です。
        /// </param>
        /// <param name="number">
        /// メインまたはサブ内でのスロット番号です。
        /// </param>
        /// <param name="unitBehavior">
        /// このプラーナムを所有するユニットです。
        /// </param>
        /// <param name="pranamBehavior">
        /// このプラーナムに対応する実行時動作です。
        /// </param>
        /// <param name="pranamData">
        /// このプラーナムの設定データです。
        /// </param>
        /// <param name="startAngle">
        /// 初期回転角度です。
        /// </param>
        public abstract void Initialize(
            int playerId,
            bool isMain,
            int number,
            UnitBehavior unitBehavior,
            PranamBehavior pranamBehavior,
            PranamData pranamData,
            int startAngle);

        /// <summary>
        /// プラーナムを使用します。
        /// </summary>
        public abstract void UsePranam();

        /// <summary>
        /// 攻撃とハイライトが共有する選択結果から単一対象を取得します。
        /// </summary>
        /// <returns>
        /// 現在の攻撃対象です。攻撃可能な対象がない場合は<c>null</c>です。
        /// </returns>
        protected UnitBehavior GetAttackTarget()
        {
            RefreshAttackTargets();

            foreach (UnitBehavior target in _attackTargets)
            {
                return target;
            }

            return null;
        }

        /// <summary>
        /// 指定されたオブジェクトに扇形メッシュと
        /// ターゲット検出用Colliderを生成します。
        /// </summary>
        /// <param name="targetObject">
        /// メッシュとColliderを設定するオブジェクトです。
        /// </param>
        /// <param name="startAngle">
        /// 扇形の初期回転角度です。
        /// </param>
        /// <param name="areaAngle">
        /// 扇形の角度です。
        /// </param>
        /// <param name="diameter">
        /// 扇形メッシュの大きさです。
        /// 現在の実装ではワールド半径として扱われます。
        /// </param>
        /// <param name="quality">
        /// 360度あたりの分割品質です。
        /// </param>
        public void CreateArc(
            GameObject targetObject,
            int startAngle,
            float areaAngle = 0.0f,
            float diameter = 0.0f,
            int quality = k_DefaultArcQuality)
        {
            _arcTargetObject =
                targetObject;

            _arcStartAngle =
                startAngle;

            _arcAreaAngle =
                areaAngle;

            _arcDiameter =
                diameter;

            _arcQuality =
                Mathf.Max(
                    1,
                    quality);

            _hasArcInitializationRequest =
                true;

            _arcStabilizationFramesRemaining =
                k_ArcStabilizationFrameCount;

            IsArcInitialized =
                TryCreateArc();

            Debug.Log(
                $"[UnitInit] CreateArc " +
                $"Frame={Time.frameCount}," +
                $"Name={name}," +
                $"Angle={areaAngle}," +
                $"Diameter={diameter}," +
                $"Active={gameObject.activeInHierarchy}",
                this);
        }

        /// <summary>
        /// 保存されている生成条件を使用して、
        /// 扇形メッシュと検出Colliderの初期化を再試行します。
        /// </summary>
        /// <returns>
        /// Pranam本体と必要な扇形範囲の初期化が
        /// 完了している場合は<c>true</c>です。
        /// </returns>
        public bool EnsureRuntimeReady()
        {
            CacheReferences();

            if (!_isInitialized)
            {
                return false;
            }

            if (!RequiresArcInitialization)
            {
                EnsureNonArcScale();

                return true;
            }

            IsArcInitialized =
                EnsureArcRuntimeState();

            if (!IsArcInitialized)
            {
                return false;
            }

            _scanTimer =
                0.0f;

            _hasPerformedInitialScan =
                false;

            ScanTargets();

            return true;
        }

        /// <summary>
        /// 現在の扇形状態を検証し、破損または上書きされている場合は
        /// 保存済みの武器パラメータから再構築します。
        /// </summary>
        private bool EnsureArcRuntimeState()
        {
            if (!RequiresArcInitialization)
            {
                return true;
            }

            if (IsArcRuntimeStateValid())
            {
                return true;
            }

            return TryCreateArc();
        }

        /// <summary>
        /// 現在の扇形Mesh、Collider、RigidbodyおよびTransformが
        /// 保存済みの生成条件と一致しているか確認します。
        /// </summary>
        private bool IsArcRuntimeStateValid()
        {
            if (_arcTargetObject == null ||
                _generatedArcMesh == null ||
                !IsArcTransformInitialized())
            {
                return false;
            }

            MeshFilter meshFilter =
                _arcTargetObject.GetComponent<MeshFilter>();

            SphereCollider sphereCollider =
                _arcTargetObject.GetComponent<SphereCollider>();

            Rigidbody rigidbody =
                _arcTargetObject.GetComponent<Rigidbody>();

            if (meshFilter == null ||
                !ReferenceEquals(
                    meshFilter.sharedMesh,
                    _generatedArcMesh) ||
                sphereCollider == null ||
                !sphereCollider.enabled ||
                !sphereCollider.isTrigger ||
                sphereCollider.radius <= 0.0f ||
                rigidbody == null ||
                !rigidbody.isKinematic ||
                rigidbody.useGravity)
            {
                return false;
            }

            float expectedAngle =
                Mathf.Clamp(
                    _arcAreaAngle,
                    0.0f,
                    360.0f);

            return Mathf.Abs(
                    _angle -
                    expectedAngle) <=
                0.001f;
        }

        /// <summary>
        /// 保存されている条件から扇形メッシュと
        /// ターゲット検出用Colliderを生成します。
        /// </summary>
        /// <returns>
        /// 生成に成功した場合は<c>true</c>です。
        /// </returns>
        private bool TryCreateArc()
        {
            if (_arcTargetObject == null)
            {
                Debug.LogError(
                    $"{nameof(PranamBase)}: " +
                    "扇形メッシュの生成対象が設定されていません。",
                    this);

                return false;
            }

            if (_arcAreaAngle <= 0.0f ||
                _arcDiameter <= 0.0f)
            {
                Debug.LogWarning(
                    $"{nameof(PranamBase)}: " +
                    "扇形メッシュを生成するには、" +
                    "0より大きい角度と範囲が必要です。" +
                    $" Angle={_arcAreaAngle}," +
                    $" Diameter={_arcDiameter}",
                    this);

                return false;
            }

            _angle =
                Mathf.Clamp(
                    _arcAreaAngle,
                    0.0f,
                    360.0f);

            MeshFilter meshFilter =
                GetOrAddComponent<MeshFilter>(
                    _arcTargetObject);

            MeshRenderer meshRenderer =
                GetOrAddComponent<MeshRenderer>(
                    _arcTargetObject);

            _detectionCollider =
                GetOrAddComponent<SphereCollider>(
                    _arcTargetObject);

            Rigidbody rigidbody =
                GetOrAddComponent<Rigidbody>(
                    _arcTargetObject);

            if (meshFilter == null ||
                meshRenderer == null ||
                _detectionCollider == null ||
                rigidbody == null)
            {
                Debug.LogError(
                    $"{nameof(PranamBase)}: " +
                    "扇形範囲に必要なコンポーネントを生成できませんでした。",
                    _arcTargetObject);

                return false;
            }

            InitializeArcTransform();

            DestroyGeneratedArcMesh();

            _generatedArcMesh =
                CreateArcMesh(
                    _angle,
                    _arcQuality);

            if (_generatedArcMesh == null)
            {
                Debug.LogError(
                    $"{nameof(PranamBase)}: " +
                    "扇形メッシュの生成に失敗しました。",
                    _arcTargetObject);

                return false;
            }

            meshFilter.sharedMesh =
                _generatedArcMesh;

            _detectionCollider.isTrigger =
                true;

            _detectionCollider.center =
                Vector3.zero;

            _detectionCollider.radius =
                1.0f;

            _detectionCollider.enabled =
                true;

            rigidbody.isKinematic =
                true;

            rigidbody.useGravity =
                false;

            _diameter =
                CalculateWorldSphereRadius(
                    _detectionCollider);

            _scanTimer =
                0.0f;

            _hasPerformedInitialScan =
                false;

            ScanTargets();

            return meshFilter.sharedMesh != null &&
                _detectionCollider.enabled &&
                _diameter > 0.0f &&
                IsArcTransformInitialized();
        }

        /// <summary>
        /// 扇形を生成するオブジェクトのTransformを
        /// 親オブジェクト基準の初期状態へ設定します。
        /// </summary>
        private void InitializeArcTransform()
        {
            if (_arcTargetObject == null)
            {
                return;
            }

            Transform arcTransform =
                _arcTargetObject.transform;

            arcTransform.localPosition =
                Vector3.zero;

            arcTransform.localRotation =
                Quaternion.Euler(
                    0.0f,
                    _arcStartAngle,
                    0.0f);

            arcTransform.localScale =
                new Vector3(
                    _arcDiameter,
                    k_PranamVisualScale,
                    _arcDiameter);
        }

        /// <summary>
        /// 扇形オブジェクトのローカルTransformが
        /// 初期化済みかどうかを確認します。
        /// </summary>
        /// <returns>
        /// 位置、回転およびスケールが期待値の場合は
        /// <c>true</c>です。
        /// </returns>
        private bool IsArcTransformInitialized()
        {
            if (_arcTargetObject == null)
            {
                return false;
            }

            Transform arcTransform =
                _arcTargetObject.transform;

            Quaternion expectedRotation =
                Quaternion.Euler(
                    0.0f,
                    _arcStartAngle,
                    0.0f);

            Vector3 expectedScale =
                new(
                    _arcDiameter,
                    k_PranamVisualScale,
                    _arcDiameter);

            return arcTransform.localPosition.sqrMagnitude <=
                    0.000001f &&
                Quaternion.Angle(
                    arcTransform.localRotation,
                    expectedRotation) <=
                    0.01f &&
                Vector3.SqrMagnitude(
                    arcTransform.localScale -
                    expectedScale) <=
                    0.000001f;
        }

        /// <summary>
        /// 現在のターゲット候補、扇形範囲内判定および
        /// ハイライト状態を即時再構築します。
        /// </summary>
        public void RefreshTargetsImmediately()
        {
            if (!_isInitialized ||
                !isActiveAndEnabled ||
                !gameObject.activeInHierarchy)
            {
                return;
            }

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null ||
                phaseManager.CurrentPhase != MatchPhase.Micro)
            {
                return;
            }

            CleanupInvalidUnits();

            ScanTargets();

            UpdateArcMembership();

            UpdateTargetHighlights();

            _scanTimer =
                0.0f;

            _hasPerformedInitialScan =
                true;
        }

        /// <summary>
        /// 指定されたオブジェクトと全ての子オブジェクトへ
        /// レイヤーを設定します。
        /// </summary>
        /// <param name="targetObject">
        /// 設定対象のルートオブジェクトです。
        /// </param>
        /// <param name="layer">
        /// 設定するレイヤー番号です。
        /// </param>
        public static void SetLayerRecursively(
            GameObject targetObject,
            int layer)
        {
            if (targetObject == null)
            {
                return;
            }

            targetObject.layer = layer;

            foreach (Transform child in
                    targetObject.transform)
            {
                SetLayerRecursively(
                    child.gameObject,
                    layer);
            }
        }

        /// <summary>
        /// プラーナムの使用を終了します。
        /// </summary>
        public virtual void Exit()
        {
        }

        /// <summary>
        /// プラーナムの使用ボタンが離されたときに呼び出されます。
        /// </summary>
        public virtual void ReleasePranam()
        {
        }

        /// <summary>
        /// コンポーネントが有効になったときに、
        /// 派生クラス固有の処理を実行します。
        /// </summary>
        protected virtual void OnEnableInternal()
        {
        }

        /// <summary>
        /// 指定された対象をハイライトできるかどうかを判定します。
        /// </summary>
        /// <param name="targetUnit">
        /// 判定対象のユニットです。
        /// </param>
        /// <returns>
        /// ハイライトできる場合は<c>true</c>です。
        /// </returns>
        protected virtual bool CanHighlightUnit(
            UnitBehavior targetUnit)
        {
            return IsVisible(
                targetUnit);
        }

        /// <summary>
        /// 所有MicroUnitのCameraから対象MicroUnitのModelまでに
        /// 遮蔽物がないかどうかを現在の3D空間で判定します。
        /// </summary>
        /// <param name="targetUnit">
        /// 視認判定を行う対象です。
        /// </param>
        /// <returns>
        /// Cameraと対象Modelの間に遮蔽物がない場合は
        /// <c>true</c>です。
        /// </returns>
        protected bool IsVisible(
            UnitBehavior targetUnit)
        {
            return CanSeeTarget(targetUnit);
        }

        /// <summary>
        /// 所有MicroUnitに属するVisionSensorを取得します。
        /// </summary>
        /// <param name="visionSensor">
        /// 取得されたVisionSensorです。
        /// </param>
        /// <returns>
        /// 有効なVisionSensorを取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetVisionSensor(
            out VisionSensor visionSensor)
        {
            visionSensor = null;

            if (!TryGetMicroUnit(
                    _unitBehavior,
                    out MicroUnitBehavior ownerMicroUnit))
            {
                _visionSensor = null;

                return false;
            }

            if (_visionSensor == null ||
                _visionSensor.GetComponentInParent<MicroUnitBehavior>() !=
                    ownerMicroUnit)
            {
                _visionSensor =
                    ownerMicroUnit.GetComponentInChildren<VisionSensor>(
                        true);
            }

            visionSensor =
                _visionSensor;

            return visionSensor != null;
        }

        /// <summary>
        /// Unityコンポーネントが有効になったときに参照を取得します。
        /// </summary>
        private void OnEnable()
        {
            CacheReferences();

            _scanTimer =
                0.0f;

            _hasPerformedInitialScan =
                false;

            if (RequiresArcInitialization)
            {
                IsArcInitialized =
                    EnsureArcRuntimeState();
            }

            OnEnableInternal();

            if (_isInitialized &&
                !RequiresArcInitialization)
            {
                EnsureNonArcScale();
            }

            if (RequiresArcInitialization)
            {
                IsArcInitialized =
                    EnsureArcRuntimeState();
            }

            if (RequiresArcInitialization &&
                IsArcInitialized)
            {
                ScanTargets();
            }
        }

        /// <summary>
        /// 初期生成直後の限られたフレームだけ扇形状態を再検証し、
        /// Startやフェイズ切り替えによる上書きを復元します。
        /// </summary>
        private void LateUpdate()
        {
            if (!_isInitialized)
            {
                return;
            }

            if (!RequiresArcInitialization)
            {
                EnsureNonArcScale();

                return;
            }

            if (_arcStabilizationFramesRemaining <= 0)
            {
                return;
            }

            _arcStabilizationFramesRemaining--;

            IsArcInitialized =
                EnsureArcRuntimeState();
        }

        /// <summary>
        /// 防御・ステルスなど扇形を使用しないPranamの
        /// XYZ方向のスケールを一定値へ補正します。
        /// </summary>
        private void EnsureNonArcScale()
        {
            Vector3 expectedScale =
                Vector3.one *
                k_PranamVisualScale;

            if (Vector3.SqrMagnitude(
                    transform.localScale -
                    expectedScale) <=
                0.000001f)
            {
                return;
            }

            transform.localScale =
                expectedScale;
        }

        /// <summary>
        /// コンポーネントが無効になったときに、
        /// 派生クラス固有の処理を実行します。
        /// </summary>
        protected virtual void OnDisableInternal()
        {
        }

        /// <summary>
        /// Unityコンポーネントが無効になったときに、
        /// 派生クラス固有の後処理とターゲット情報の解除を行います。
        /// </summary>
        private void OnDisable()
        {
            OnDisableInternal();

            ClearAllTargetHighlights();

            _candidates.Clear();
            _unitsWithinArc.Clear();
            _highlightedTargets.Clear();
            _attackTargets.Clear();
            _attackTargetCandidates.Clear();

            _scanTimer = 0.0f;
            _hasPerformedInitialScan = false;

        }

        /// <summary>
        /// コンポーネント破棄時に動的生成したMeshを破棄します。
        /// </summary>
        private void OnDestroy()
        {
            DestroyGeneratedArcMesh();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorで変更された値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _scanInterval =
            Mathf.Max(
            k_MinimumScanInterval,
            _scanInterval);
        }

#endif

        /// <summary>
        /// ターゲット検出、ハイライト更新および
        /// 経過時間の更新を行います。
        /// </summary>
        protected virtual void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            CleanupInvalidUnits();

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null ||
                phaseManager.CurrentPhase != MatchPhase.Micro)
            {
                return;
            }

            _time += Time.deltaTime;
            _scanTimer += Time.deltaTime;

            if (!_hasPerformedInitialScan)
            {
                ScanTargets();
            }

            if (_scanTimer >= _scanInterval)
            {
                _scanTimer = 0.0f;
                ScanTargets();
            }

            UpdateArcMembership();
            UpdateTargetHighlights();

        }

        /// <summary>
        /// 所有ユニット、マップユニットおよび
        /// PranamManagerへの参照を取得します。
        /// </summary>
        private void CacheReferences()
        {
            _mapUnitBehavior =
                GetComponentInParent<TacticalMapUnitBehavior>(
                    true);

            if (_mapUnitBehavior != null)
            {
                _unitBehavior =
                    _mapUnitBehavior
                        .GetComponentInParent<UnitBehavior>(
                            true);
            }

            _pranamManager =
                GetComponentInParent<PranamManager>(
                    true);

            if (_detectionCollider == null)
            {
                _detectionCollider =
                    GetComponent<SphereCollider>();
            }

            if (_mapUnitBehavior == null)
            {
                Debug.LogWarning(
                    $"{nameof(PranamBase)}: " +
                    $"{nameof(TacticalMapUnitBehavior)}を取得できません。",
                    this);
            }

            if (_unitBehavior == null)
            {
                Debug.LogWarning(
                    $"{nameof(PranamBase)}: " +
                    $"{nameof(UnitBehavior)}を取得できません。",
                    this);
            }
        }

        /// <summary>
        /// UnitManagerに登録されている敵ユニットを
        /// 攻撃範囲の候補として再取得します。
        /// </summary>
        /// <remarks>
        /// 射程と扇形の最終判定はMapUnitの2D座標で行います。
        /// Physics.OverlapSphereを事前絞り込みに使用すると，
        /// MicroUnitとMapUnitの高さ差やColliderの有効状態によって，
        /// 2D上で範囲内の対象が候補から落ちるため使用しません。
        /// </remarks>
        private void ScanTargets()
        {
            _candidates.Clear();

            if (_unitBehavior == null)
            {
                RemoveTargetsOutsideCandidateSet();

                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                RemoveTargetsOutsideCandidateSet();

                return;
            }

            foreach (UnitBehavior candidateUnit in
                    unitManager.GetAllUnitBehaviors())
            {
                if (IsCandidateUnit(candidateUnit))
                {
                    _candidates.Add(
                        candidateUnit);
                }
            }

            RemoveTargetsOutsideCandidateSet();

            _hasPerformedInitialScan = true;
        }

        /// <summary>
        /// 指定されたユニットが攻撃範囲の
        /// 候補として有効かどうかを判定します。
        /// </summary>
        /// <param name="candidateUnit">
        /// 判定対象のユニットです。
        /// </param>
        /// <returns>
        /// 所有ユニットと異なるチームの有効なユニットの場合は
        /// <c>true</c>です。
        /// </returns>
        private bool IsCandidateUnit(
            UnitBehavior candidateUnit)
        {
            if (candidateUnit == null ||
                _unitBehavior == null)
            {
                return false;
            }

            if (candidateUnit == _unitBehavior)
            {
                return false;
            }

            if (candidateUnit.transform.root ==
                _unitBehavior.transform.root)
            {
                return false;
            }

            if (_unitBehavior.IsSameTeam(
                    candidateUnit.GetTeamId()))
            {
                return false;
            }

            return IsValidTarget(candidateUnit);
        }

        /// <summary>
        /// 現在の候補一覧から外れたユニットを
        /// 追跡対象とハイライト対象から除外します。
        /// </summary>
        private void RemoveTargetsOutsideCandidateSet()
        {
            List<UnitBehavior> removedUnits = new();

            foreach (UnitBehavior targetUnit in
                    _unitsWithinArc)
            {
                if (!_candidates.Contains(targetUnit))
                {
                    removedUnits.Add(
                        targetUnit);
                }
            }

            foreach (UnitBehavior removedUnit in
                    removedUnits)
            {
                RemoveTrackedTarget(
                    removedUnit);
            }
        }

        /// <summary>
        /// 候補ユニットが扇形範囲内に存在するかを更新します。
        /// </summary>
        private void UpdateArcMembership()
        {
            foreach (UnitBehavior targetUnit in
                    _candidates)
            {
                if (!IsValidTarget(targetUnit))
                {
                    continue;
                }

                if (IsWithinArc(targetUnit))
                {
                    _unitsWithinArc.Add(
                        targetUnit);
                }
                else
                {
                    RemoveTrackedTarget(
                        targetUnit);
                }
            }
        }

        /// <summary>
        /// 攻撃条件と選択ルールを適用し、選択された対象だけに表示を同期します。
        /// </summary>
        private void UpdateTargetHighlights()
        {
            UpdateAttackTargetSelection();

            List<UnitBehavior> previousHighlights = new(_highlightedTargets);

            foreach (UnitBehavior target in previousHighlights)
            {
                if (!_attackTargets.Contains(target) || !CanHighlightUnit(target))
                {
                    _highlightedTargets.Remove(target);
                    RemoveTargetHighlight(target);
                }
            }

            foreach (UnitBehavior target in _attackTargets)
            {
                if (CanHighlightUnit(target) && _highlightedTargets.Add(target))
                {
                    AddTargetHighlight(target);
                }
            }
        }

        /// <summary>
        /// 攻撃条件を満たす候補へ選択ルールを適用し、固定済みの対象を優先します。
        /// </summary>
        private void UpdateAttackTargetSelection()
        {
            _attackTargetCandidates.Clear();
            _attackTargets.Clear();

            foreach (UnitBehavior target in _unitsWithinArc)
            {
                if (IsValidTarget(target) && CanSelectAttackTarget(target) &&
                    TryGetTargetSquaredDistance(target, out float squaredDistance) &&
                    !float.IsNaN(squaredDistance) && !float.IsInfinity(squaredDistance))
                {
                    _attackTargetCandidates.Add(new AttackTargetCandidate(target, squaredDistance));
                }
            }

            if (TryGetCommittedAttackTarget(out UnitBehavior committedTarget))
            {
                if (IsValidTarget(committedTarget))
                {
                    _attackTargets.Add(committedTarget);
                }
            }
            else
            {
                TargetSelection.SelectTargets(_attackTargetCandidates, _attackTargets);
            }
        }

        /// <summary>
        /// 射程内の候補が攻撃対象として選択可能か判定します。
        /// </summary>
        /// <remarks>
        /// 対象数や優先順位は<see cref="TargetSelection"/>が担当します。
        /// </remarks>
        protected virtual bool CanSelectAttackTarget(UnitBehavior targetUnit)
        {
            return CanSeeTarget(targetUnit);
        }

        /// <summary>
        /// 照準や接近を開始した後に固定されている攻撃対象を取得します。
        /// </summary>
        /// <returns>
        /// 対象固定中の場合は<c>true</c>です。固定対象が失われた場合も再選択しません。
        /// </returns>
        protected virtual bool TryGetCommittedAttackTarget(out UnitBehavior targetUnit)
        {
            targetUnit = null;
            return false;
        }

        /// <summary>
        /// 実際の戦闘位置を基準に候補までの距離を取得します。
        /// </summary>
        protected virtual bool TryGetTargetSquaredDistance(UnitBehavior targetUnit, out float squaredDistance)
        {
            squaredDistance = 0.0f;

            if (!TryGetMicroUnit(_unitBehavior, out MicroUnitBehavior ownerMicroUnit) ||
                !TryGetMicroUnit(targetUnit, out MicroUnitBehavior targetMicroUnit))
            {
                return false;
            }

            squaredDistance = (targetMicroUnit.transform.position - ownerMicroUnit.transform.position).sqrMagnitude;
            return true;
        }

        /// <summary>
        /// マップ上の水平位置を基準に候補までの距離を取得します。
        /// </summary>
        protected bool TryGetMapTargetSquaredDistance(UnitBehavior targetUnit, out float squaredDistance)
        {
            squaredDistance = 0.0f;
            TacticalMapUnitBehavior ownerMapUnit = _unitBehavior?.GetTacticalMapUnitBehavior();
            TacticalMapUnitBehavior targetMapUnit = targetUnit?.GetTacticalMapUnitBehavior();

            if (ownerMapUnit == null || targetMapUnit == null)
            {
                return false;
            }

            Vector3 direction = targetMapUnit.transform.position - ownerMapUnit.transform.position;
            direction.y = 0.0f;
            squaredDistance = direction.sqrMagnitude;
            return true;
        }

        /// <summary>
        /// 攻撃対象を取得する直前に、現在の位置と射界で対象一覧を更新します。
        /// </summary>
        /// <remarks>
        /// Updateの実行順に依存せず、照準回転や移動の後に射界へ入った敵も
        /// 同じフレームの射撃対象として扱います。遮蔽された敵は除外します。
        /// </remarks>
        private void RefreshAttackTargets()
        {
            if (!_isInitialized || !isActiveAndEnabled)
            {
                return;
            }

            ScanTargets();
            UpdateArcMembership();
            UpdateTargetHighlights();
        }

        /// <summary>
        /// 指定されたユニットが現在の扇形範囲内に
        /// 存在するかどうかを判定します。
        /// </summary>
        /// <param name="targetUnit">
        /// 判定対象のユニットです。
        /// </param>
        /// <returns>
        /// 扇形範囲内に存在する場合は<c>true</c>です。
        /// </returns>
        /// <remarks>
        /// OverlapSphereは敵Colliderの外周が触れただけでも候補へ
        /// 含めます。Pranamの射程はユニット中心間距離として扱い、
        /// Colliderの大きさによる射程延長を許可しません。
        /// </remarks>
        private bool IsWithinArc(
                    UnitBehavior targetUnit)
        {
            TacticalMapUnitBehavior targetMapUnit =
                targetUnit?.GetTacticalMapUnitBehavior();

            if (targetMapUnit == null)
            {
                return false;
            }

            Collider targetCollider =
                targetMapUnit.GetComponentInChildren<Collider>();

            if (targetCollider == null)
            {
                return false;
            }

            if (!TryGetDetectionSphere(
                    out Vector3 detectionCenter,
                    out float detectionRadius))
            {
                return false;
            }

            Vector3 direction =
                targetMapUnit.transform.position -
                detectionCenter;

            direction.y = 0.0f;

            float distance =
                direction.magnitude;

            if (distance >
                detectionRadius +
                k_RangeTolerance)
            {
                return false;
            }

            if (direction.sqrMagnitude <=
                k_MinimumDirectionSqrMagnitude)
            {
                return true;
            }

            Vector3 forward =
                transform.forward;

            forward.y = 0.0f;

            if (forward.sqrMagnitude <=
                k_MinimumDirectionSqrMagnitude)
            {
                return false;
            }

            float centerAngle =
                Vector3.Angle(
                    forward,
                    direction);

            Vector3 colliderExtents =
                targetCollider.bounds.extents;

            float targetRadius =
                Mathf.Max(
                    colliderExtents.x,
                    colliderExtents.z);

            float radiusAngle =
                distance <= targetRadius
                    ? 90.0f
                    : Mathf.Asin(
                        Mathf.Clamp01(
                            targetRadius /
                            distance)) *
                    Mathf.Rad2Deg;

            return centerAngle - radiusAngle <=
                _angle * 0.5f;
        }

        /// <summary>
        /// 指定されたユニットを追跡対象と
        /// ハイライト対象から除外します。
        /// </summary>
        /// <param name="targetUnit">
        /// 除外するユニットです。
        /// </param>
        private void RemoveTrackedTarget(
            UnitBehavior targetUnit)
        {
            _unitsWithinArc.Remove(
                targetUnit);
            _attackTargets.Remove(targetUnit);

            if (_highlightedTargets.Remove(
                    targetUnit))
            {
                RemoveTargetHighlight(
                    targetUnit);
            }
        }

        /// <summary>
        /// 無効または破棄済みのユニットを
        /// 各ターゲット一覧から除外します。
        /// </summary>
        private void CleanupInvalidUnits()
        {
            _attackTargets.RemoveWhere(targetUnit => !IsValidTarget(targetUnit));
            _candidates.RemoveWhere(
                targetUnit =>
                    !IsValidTarget(targetUnit));

            _unitsWithinArc.RemoveWhere(
                targetUnit =>
                    !IsValidTarget(targetUnit));

            List<UnitBehavior> invalidHighlightedTargets =
                new();

            foreach (UnitBehavior targetUnit in
                    _highlightedTargets)
            {
                if (!IsValidTarget(targetUnit))
                {
                    invalidHighlightedTargets.Add(
                        targetUnit);
                }
            }

            foreach (UnitBehavior targetUnit in
                    invalidHighlightedTargets)
            {
                if (targetUnit != null)
                {
                    RemoveTargetHighlight(
                        targetUnit);
                }

                _highlightedTargets.Remove(
                    targetUnit);
            }
        }

        /// <summary>
        /// 全てのハイライト対象から表示を解除します。
        /// </summary>
        private void ClearAllTargetHighlights()
        {
            List<UnitBehavior> highlightedTargets =
                new(_highlightedTargets);

            foreach (UnitBehavior targetUnit in
                    highlightedTargets)
            {
                if (targetUnit != null)
                {
                    RemoveTargetHighlight(
                        targetUnit);
                }
            }
        }

        /// <summary>
        /// 指定された対象へハイライトを追加します。
        /// </summary>
        /// <param name="targetUnit">
        /// ハイライト対象です。
        /// </param>
        private void AddTargetHighlight(
            UnitBehavior targetUnit)
        {
            if (!ShouldShowTargetHighlight() ||
                targetUnit == null ||
                _pranamManager == null)
            {
                return;
            }

            _pranamManager.AddHighlight(
                targetUnit);
        }

        /// <summary>
        /// 指定された対象からハイライトを解除します。
        /// </summary>
        /// <param name="targetUnit">
        /// ハイライトを解除する対象です。
        /// </param>
        private void RemoveTargetHighlight(
            UnitBehavior targetUnit)
        {
            if (!ShouldShowTargetHighlight() ||
                targetUnit == null ||
                _pranamManager == null)
            {
                return;
            }

            _pranamManager.RemoveHighlight(
                targetUnit);
        }

        /// <summary>
        /// このクライアント上で対象ハイライトを
        /// 表示するべきかどうかを判定します。
        /// </summary>
        /// <returns>
        /// ローカルプレイヤーのプラーナムである場合は<c>true</c>です。
        /// </returns>
        private bool ShouldShowTargetHighlight()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            return unitManager != null &&
                _playerId ==
                unitManager.ControlledUnitId;
        }

        /// <summary>
        /// ターゲットとして使用できるユニットかどうかを判定します。
        /// </summary>
        /// <param name="targetUnit">
        /// 判定対象のユニットです。
        /// </param>
        /// <returns>
        /// 必要なコンポーネントが存在する場合は<c>true</c>です。
        /// </returns>
        private static bool IsValidTarget(
            UnitBehavior targetUnit)
        {
            return targetUnit != null &&
                targetUnit.GetMicroUnitBehavior() != null &&
                targetUnit.GetTacticalMapUnitBehavior() != null;
        }

        /// <summary>
        /// UnitBehaviorから有効なMicroUnitBehaviorを取得します。
        /// </summary>
        /// <param name="unitBehavior">
        /// 取得元のユニットです。
        /// </param>
        /// <param name="microUnitBehavior">
        /// 取得されたMicroUnitBehaviorです。
        /// </param>
        /// <returns>
        /// 有効なMicroUnitBehaviorを取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetMicroUnit(
            UnitBehavior unitBehavior,
            out MicroUnitBehavior microUnitBehavior)
        {
            microUnitBehavior =
                unitBehavior != null
                    ? unitBehavior.GetMicroUnitBehavior()
                    : null;

            return microUnitBehavior != null;
        }

        /// <summary>
        /// データから生成した扇形のワールド半径を取得します。
        /// </summary>
        public float GetAttackRangeRadius()
        {
            if (!RequiresArcInitialization || !IsArcInitialized || _arcTargetObject == null)
            {
                return 0.0f;
            }

            Vector3 scale = _arcTargetObject.transform.lossyScale;

            return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        /// <summary>
        /// 現在の検出Colliderからワールド中心と半径を取得します。
        /// </summary>
        /// <param name="center">
        /// ワールド座標上の中心です。
        /// </param>
        /// <param name="radius">
        /// ワールド座標上の半径です。
        /// </param>
        /// <returns>
        /// 有効な検出範囲を取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetDetectionSphere(
            out Vector3 center,
            out float radius)
        {
            center = Vector3.zero;
            radius = 0.0f;

            if (_detectionCollider == null)
            {
                _detectionCollider =
                    GetComponent<SphereCollider>();
            }

            if (_detectionCollider == null ||
                !_detectionCollider.enabled)
            {
                return false;
            }

            center =
                _detectionCollider.transform.TransformPoint(
                    _detectionCollider.center);

            radius =
                CalculateWorldSphereRadius(
                    _detectionCollider);

            return radius > 0.0f;
        }

        /// <summary>
        /// SphereColliderのワールド半径を計算します。
        /// </summary>
        /// <param name="sphereCollider">
        /// 計算対象のColliderです。
        /// </param>
        /// <returns>
        /// ワールドスケール適用後の半径です。
        /// </returns>
        private static float CalculateWorldSphereRadius(
            SphereCollider sphereCollider)
        {
            if (sphereCollider == null)
            {
                return 0.0f;
            }

            Vector3 lossyScale =
                sphereCollider.transform.lossyScale;

            float maximumScale =
                Mathf.Max(
                    Mathf.Abs(lossyScale.x),
                    Mathf.Abs(lossyScale.y),
                    Mathf.Abs(lossyScale.z));

            return sphereCollider.radius *
                maximumScale;
        }

        /// <summary>
        /// 指定された扇形角度と品質からMeshを生成します。
        /// </summary>
        /// <param name="areaAngle">
        /// 扇形の角度です。
        /// </param>
        /// <param name="quality">
        /// 360度あたりの分割品質です。
        /// </param>
        /// <returns>
        /// 生成された扇形Meshです。
        /// </returns>
        private static Mesh CreateArcMesh(
            float areaAngle,
            int quality)
        {
            int sanitizedQuality =
                Mathf.Max(
                    1,
                    quality);

            int segmentCount =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        sanitizedQuality *
                        areaAngle /
                        360.0f));

            List<Vector3> vertices =
                new(segmentCount + 2)
                {
                    Vector3.zero
                };

            List<int> triangles =
                new(segmentCount * 3);

            float halfAngle =
                areaAngle * 0.5f;

            for (int segmentIndex = 0;
                segmentIndex <= segmentCount;
                segmentIndex++)
            {
                float interpolation =
                    (float)segmentIndex /
                    segmentCount;

                float angle =
                    Mathf.Lerp(
                        -halfAngle,
                        halfAngle,
                        interpolation);

                float radians =
                    angle *
                    Mathf.Deg2Rad;

                vertices.Add(
                    new Vector3(
                        Mathf.Sin(radians),
                        0.0f,
                        Mathf.Cos(radians)));

                if (segmentIndex >= segmentCount)
                {
                    continue;
                }

                triangles.Add(0);
                triangles.Add(segmentIndex + 1);
                triangles.Add(segmentIndex + 2);
            }

            Mesh mesh =
                new()
                {
                    name = k_ArcMeshName
                };

            mesh.SetVertices(
                vertices);

            mesh.SetTriangles(
                triangles,
                0);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// 動的に生成した扇形Meshを破棄します。
        /// </summary>
        private void DestroyGeneratedArcMesh()
        {
            if (_generatedArcMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(
                    _generatedArcMesh);
            }
            else
            {
                DestroyImmediate(
                    _generatedArcMesh);
            }

            _generatedArcMesh =
                null;

            IsArcInitialized =
                false;
        }

        /// <summary>
        /// GameObjectから指定されたコンポーネントを取得し、
        /// 存在しない場合は追加します。
        /// </summary>
        /// <typeparam name="T">
        /// 取得または追加するComponent型です。
        /// </typeparam>
        /// <param name="targetObject">
        /// 対象のGameObjectです。
        /// </param>
        /// <returns>
        /// 取得または追加されたComponentです。
        /// </returns>
        private static T GetOrAddComponent<T>(
            GameObject targetObject)
            where T : Component
        {
            if (targetObject == null)
            {
                return null;
            }

            T component =
                targetObject.GetComponent<T>();

            if (component != null)
            {
                return component;
            }

            return targetObject.AddComponent<T>();
        }

    }
}
