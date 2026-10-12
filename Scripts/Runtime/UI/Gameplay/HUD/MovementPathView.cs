using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using PranaTactics.Board;
using PranaTactics.Core;
using PranaTactics.Match.Phases;
using PranaTactics.Settings;
using PranaTactics.Unit;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 味方の確定経路を操作用マクロ盤面と、ミニマップ・全体マップ共用盤面へ表示します。
    /// </summary>
    /// <remarks>
    /// 表示用スナップショットを保持し、ミクロで経路が消費されても全体の移動順を維持します。
    /// ホバー候補や通信データを変更せず、ローカルチームの経路だけを描画します。
    /// </remarks>
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoardManager))]
    public sealed class MovementPathView : MonoBehaviour
    {
        private sealed class UnitPathView
        {
            public MovementPathSnapshot Snapshot;
            public int ProgressOrigin;
            public GameObject MacroRoot;
            public GameObject MapRoot;
            public Mesh MacroMesh;
            public Mesh MapMesh;
            public Mesh MacroMarkers;
            public Mesh MapMarkers;
        }

        /// <summary>
        /// カメラが既に描画対象としているマップ表示用レイヤーです。
        /// </summary>
        private const string k_PathLayerName = "Visible";

        /// <summary>
        /// 経路の変更を確認する間隔です。
        /// </summary>
        private const float k_RefreshInterval = 0.1f;

        /// <summary>
        /// ゲーム設定から共有する移動経路の表示データです。
        /// </summary>
        private MovementPathData _data;

        /// <summary>
        /// 現在の操作盤面カメラです。
        /// </summary>
        private Camera _macroCamera;

        private Matrix4x4 _lastCameraMatrix;
        private int _lastPixelHeight;
        private int _lastPixelWidth;
        private bool _needsMacroRebuild;
        private int _lastConfigurationHash;
        private float _nextAnimationTime;
        private bool _needsMarkerRefresh;
        private readonly Dictionary<Mesh, MovementPathRenderPath> _renderPaths = new Dictionary<Mesh, MovementPathRenderPath>();

        private readonly Dictionary<int, UnitPathView> _views = new Dictionary<int, UnitPathView>();
        private readonly List<MovementPathSnapshot> _paths = new List<MovementPathSnapshot>();
        private readonly MovementPathMesh _meshBuilder = new MovementPathMesh();
        private BoardManager _board;
        private Material _material;
        private MacroPlanningSession _session;
        private int _localTeamId = -1;
        private float _nextRefreshTime;
        private MatchPhase _lastPhase;
        private bool _needsRebuild;

        /// <summary>
        /// 専用マテリアルを取得し、盤面の再生成を購読します。
        /// </summary>
        private void Awake()
        {
            _board = GetComponent<BoardManager>();
            _material = Resources.Load<Material>("UI/MovementPath");
            if (_material == null)
            {
                Debug.LogError("MovementPathView: UI/MovementPathのマテリアルがありません。", this);
                enabled = false;
                return;
            }

            if (LayerMask.NameToLayer(k_PathLayerName) < 0)
            {
                Debug.LogError("MovementPathView: Visibleレイヤーがありません。", this);
                enabled = false;
                return;
            }

            _board.BoardCreated += HandleBoardCreated;
            SettingsManager.SettingsChanged += HandleColorSettingsChanged;
        }

        /// <summary>
        /// 盤面の座標が変わった場合、すべての表示を作り直します。
        /// </summary>
        private void HandleBoardCreated()
        {
            ClearViews();
            _session = null;
            _needsRebuild = true;
            _nextRefreshTime = 0.0f;
        }

        /// <summary>
        /// フェーズと経路の変化を確認し、変更がある場合だけメッシュを更新します。
        /// </summary>
        private void LateUpdate()
        {
            MatchPhaseManager phases = MatchPhaseManager.Instance;
            UnitManager units = UnitManager.Instance;
            if (phases == null || units == null)
            {
                SetVisibility(false, false);
                return;
            }

            if (!TryGetDisplayData())
            {
                SetVisibility(false, false);
                return;
            }

            RefreshDisplaySettings();
            MatchPhase phase = phases.CurrentPhase;
            bool isMacro = phase == MatchPhase.Macro;
            bool isMicro = phase == MatchPhase.Micro;
            SetVisibility(isMacro, isMacro || isMicro);
            if (!isMacro && !isMicro)
            {
                return;
            }

            if (isMacro)
            {
                RefreshMacroCamera(units);
            }

            if (Time.unscaledTime < _nextRefreshTime && phase == _lastPhase)
            {
                if (_needsMacroRebuild)
                {
                    RebuildMeshes(true);
                    _needsMacroRebuild = false;
                }

                UpdateAnimations(isMacro);
                return;
            }

            _nextRefreshTime = Time.unscaledTime + k_RefreshInterval;
            _needsMarkerRefresh |= phase != _lastPhase;
            _lastPhase = phase;
            int teamId = units.LocalPlayerId >= 0 ? units.GetTeamId(units.LocalPlayerId) : -1;
            if (teamId < 0)
            {
                ClearViews();
                return;
            }

            if (_localTeamId != teamId || (isMacro && _session != phases.MacroPlanning))
            {
                ClearViews();
                _session = phases.MacroPlanning;
                _localTeamId = teamId;
            }

            if (isMacro && phases.MacroPlanning == null)
            {
                SetVisibility(false, false);
                return;
            }

            RefreshSnapshots(units, teamId, isMacro);
            if (_needsRebuild)
            {
                RebuildMeshes();
                _needsRebuild = false;
                _needsMacroRebuild = false;
            }
            else if (_needsMacroRebuild)
            {
                RebuildMeshes(true);
                _needsMacroRebuild = false;
            }

            UpdateAnimations(isMacro);
            SetVisibility(isMacro, true);
        }

        /// <summary>
        /// 味方の編集済み経路または移動済み区間を表示用に反映します。
        /// </summary>
        private void RefreshSnapshots(UnitManager units, int teamId, bool isMacro)
        {
            var activeIds = new HashSet<int>();
            foreach (UnitBehavior unit in units.GetAllUnitBehaviors())
            {
                if (unit == null || unit.GetTeamId() != teamId || unit.GetMicroUnitBehavior() == null)
                {
                    continue;
                }

                UnitStatus status = unit.GetUnitStatus();
                if (status == null || !status.IsAlive)
                {
                    continue;
                }

                activeIds.Add(unit.PlayerId);
                Vector2Int start = unit.GetMicroUnitBehavior().GetCurrentCoordinate();
                if (!_views.TryGetValue(unit.PlayerId, out UnitPathView view))
                {
                    view = CreateUnitView(unit.PlayerId);
                    view.Snapshot = new MovementPathSnapshot(unit.PlayerId, start, status.MigrationPath);
                    view.ProgressOrigin = isMacro ? 0 : status.CompletedMigrationStepCount;
                    _views.Add(unit.PlayerId, view);
                    _needsRebuild = true;
                }
                else if ((isMacro || (status.CompletedMigrationStepCount == 0 &&
                    status.MigrationPath.Count == status.PlannedMigrationStepCount)) &&
                    !view.Snapshot.Matches(start, status.MigrationPath))
                {
                    view.Snapshot = new MovementPathSnapshot(unit.PlayerId, start, status.MigrationPath);
                    view.ProgressOrigin = 0;
                    _needsRebuild = true;
                }

                if (!isMacro && status.PlannedMigrationStepCount == 0 && view.Snapshot.Coordinates.Count > 1)
                {
                    view.Snapshot = new MovementPathSnapshot(unit.PlayerId, start, null);
                    view.ProgressOrigin = 0;
                    _needsRebuild = true;
                }

                _needsRebuild |= view.Snapshot.UpdateProgress(isMacro ? 0 :
                    status.CompletedMigrationStepCount - view.ProgressOrigin);
            }

            var removedIds = new List<int>();
            foreach (int playerId in _views.Keys)
            {
                if (!activeIds.Contains(playerId))
                {
                    removedIds.Add(playerId);
                }
            }

            foreach (int playerId in removedIds)
            {
                DestroyView(_views[playerId]);
                _views.Remove(playerId);
                _needsRebuild = true;
            }
        }

        /// <summary>
        /// 一体分の操作盤面と共用マップのメッシュを作成します。
        /// </summary>
        private UnitPathView CreateUnitView(int playerId)
        {
            var view = new UnitPathView();
            view.MacroRoot = CreateMeshObject($"Macro Route U{playerId}", out view.MacroMesh);
            view.MapRoot = CreateMeshObject($"Map Route U{playerId}", out view.MapMesh);
            GameObject macroMarkers = CreateMeshObject($"Macro Direction U{playerId}", out view.MacroMarkers, 1);
            macroMarkers.transform.SetParent(view.MacroRoot.transform, true);
            GameObject mapMarkers = CreateMeshObject($"Map Direction U{playerId}", out view.MapMarkers, 1);
            mapMarkers.transform.SetParent(view.MapRoot.transform, true);
            return view;
        }

        /// <summary>
        /// コライダーを持たない描画専用オブジェクトを生成します。
        /// </summary>
        private GameObject CreateMeshObject(string objectName, out Mesh mesh, int sortingOrder = 0)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(transform, true);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.layer = LayerMask.NameToLayer(k_PathLayerName);
            mesh = new Mesh { name = objectName };
            mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return root;
        }

        /// <summary>
        /// チーム全体の重複レーンを計算し、両方の盤面へ同じ表示規則を適用します。
        /// </summary>
        private void RebuildMeshes(bool macroOnly = false)
        {
            _needsMarkerRefresh = true;
            _paths.Clear();
            foreach (UnitPathView view in _views.Values)
            {
                _paths.Add(view.Snapshot);
            }

            int localPlayerId = UnitManager.Instance != null ? UnitManager.Instance.LocalPlayerId : -1;
            _paths.Sort((left, right) =>
            {
                if (left.PlayerId == localPlayerId)
                {
                    return right.PlayerId == localPlayerId ? 0 : -1;
                }

                return right.PlayerId == localPlayerId ? 1 : left.PlayerId.CompareTo(right.PlayerId);
            });
            IReadOnlyList<MovementPathSegment> segments = MovementPathLayout.Build(_paths);
            for (int index = 0; index < _paths.Count; index++)
            {
                MovementPathSnapshot path = _paths[index];
                UnitPathView view = _views[path.PlayerId];
                BuildSurfaceMesh(path, segments, true, index, view.MacroMesh);
                if (!macroOnly)
                {
                    BuildSurfaceMesh(path, segments, false, index, view.MapMesh);
                }
            }
        }

        /// <summary>
        /// 盤面の高さとスケールに合わせて区間・折り返し・端点を描画します。
        /// </summary>
        private void BuildSurfaceMesh(MovementPathSnapshot path,
            IReadOnlyList<MovementPathSegment> segments, bool isMacro, int unitIndex, Mesh mesh)
        {
            GetRouteColors(path.PlayerId, out Color lineColor, out Color directionColor);
            if (!_data.UseSavedColors)
            {
                lineColor = _data.DefaultLineColor;
                directionColor = _data.DefaultDirectionColor;
            }

            var renderPath = new MovementPathRenderPath
            {
                PlayerId = path.PlayerId, CompletedSteps = path.CompletedSteps,
                LineColor = lineColor, DirectionColor = directionColor, IsPlayer = IsPlayerRoute(path.PlayerId)
            };
            float multiplier = isMacro ? _data.MacroLineWidthMultiplier : _data.MapLineWidthMultiplier;
            Vector3 previousEnd = Vector3.zero;
            Vector3 previousDirection = Vector3.zero;
            int previousStep = -2;
            Vector3 origin = Vector3.zero;
            Vector3 destination = Vector3.zero;
            float lastWidth = 0.0f;
            float lastOutline = 0.0f;
            float lastSize = 1.0f;
            float lastPixels = 80.0f;
            float firstSize = 1.0f;
            float firstPixels = 80.0f;
            foreach (MovementPathSegment segment in segments)
            {
                if (segment.PlayerId != path.PlayerId ||
                    !TryGetSurfacePoint(segment.Start, isMacro, out Vector3 start, out float spacing) ||
                    !TryGetSurfacePoint(segment.End, isMacro, out Vector3 end, out float endSpacing))
                {
                    continue;
                }

                Vector3 direction = end - start;
                direction.y = 0.0f;
                if (direction.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                direction.Normalize();
                float pixels = GetPixelsPerCell(start, spacing, isMacro);
                var display = new MovementPathDisplayScale(pixels, multiplier, segment.LaneCount, _data);
                Vector3 canonical = MovementPathLayout.CompareCoordinates(segment.Start, segment.End) <= 0
                    ? direction : -direction;
                Vector3 side = Vector3.Cross(Vector3.up, canonical);
                float offset = (segment.LaneIndex - (segment.LaneCount - 1) * 0.5f) * display.LaneSpacing * spacing;
                float width = display.Width * spacing;
                float outline = display.OutlineWidth * spacing;
                Vector3 laneStart = Vector3.Lerp(start, end, 0.12f) + side * offset;
                Vector3 laneEnd = Vector3.Lerp(start, end, 0.88f) + side * offset;
                bool isTurn = previousStep == segment.StepIndex - 1 &&
                    Vector3.Dot(previousDirection, direction) < Mathf.Cos(_data.TurnAngleDegrees * Mathf.PI / 180.0f);
                if (previousStep == segment.StepIndex - 1)
                {
                    Vector3 junction = start + side * offset * 0.5f;
                    renderPath.Add(previousEnd, junction, spacing, pixels, width, outline, segment.StepIndex);
                    renderPath.Add(junction, laneStart, spacing, pixels, width, outline, segment.StepIndex);
                }

                if (previousStep == -2)
                {
                    origin = start;
                    firstSize = spacing;
                    firstPixels = pixels;
                    renderPath.StartRadius = display.StartRadius * spacing;
                    renderPath.ShowLabels = display.ShowLabels;
                    renderPath.Add(origin, laneStart, spacing, pixels, width, outline, segment.StepIndex);
                }

                renderPath.Add(laneStart, laneEnd, spacing, pixels, width, outline, segment.StepIndex, isTurn);
                lastWidth = width;
                lastOutline = outline;
                lastSize = spacing;
                lastPixels = pixels;
                destination = end;
                renderPath.GoalRadius = display.GoalRadius * endSpacing;
                previousEnd = laneEnd;
                previousDirection = direction;
                previousStep = segment.StepIndex;
            }

            if (previousStep >= 0)
            {
                renderPath.Add(previousEnd, destination, lastSize, lastPixels, lastWidth, lastOutline, previousStep);
            }

            renderPath.LabelPosition = origin + Vector3.forward *
                (_data.LabelSpacingPixels * unitIndex / Mathf.Max(1.0f, firstPixels)) * firstSize;
            renderPath.Origin = origin;
            renderPath.Destination = destination;
            renderPath.BuildLines(_meshBuilder, _data);
            _meshBuilder.ApplyTo(mesh);
            _renderPaths[mesh] = renderPath;
        }

        /// <summary>
        /// プレイヤーの経路と味方AIの経路の表示を区別します。
        /// </summary>
        private bool IsPlayerRoute(int playerId)
        {
            return UnitManager.Instance != null && UnitManager.Instance.LocalPlayerId == playerId;
        }

        /// <summary>
        /// 経路本体を再生成せず、方向マーカーとゴールだけをデータ指定の頻度で更新します。
        /// </summary>
        private void UpdateAnimations(bool isMacro)
        {
            bool animate = isMacro ? _data.AnimateInMacro : _data.AnimateInMicro;
            bool hasAnimation = animate && ((_data.ShowDirectionMarkers && _data.AnimateDirectionMarkers &&
                _data.DirectionSpeedCellsPerSecond > 0.0f) ||
                (_data.ShowGoalMarker && _data.PulseGoal && _data.GoalPulseAmplitude > 0.0f && _data.GoalPulseCyclesPerSecond > 0.0f));
            if (!_needsMarkerRefresh && (!hasAnimation || Time.unscaledTime < _nextAnimationTime))
            {
                return;
            }

            float time = _data.UseUnscaledTime ? Time.unscaledTime : Time.time;
            foreach (UnitPathView view in _views.Values)
            {
                if (isMacro && _renderPaths.TryGetValue(view.MacroMesh, out MovementPathRenderPath macro))
                {
                    macro.BuildMarkers(_meshBuilder, _data, time, animate);
                    _meshBuilder.ApplyTo(view.MacroMarkers);
                }

                if (_renderPaths.TryGetValue(view.MapMesh, out MovementPathRenderPath map))
                {
                    map.BuildMarkers(_meshBuilder, _data, time, animate);
                    _meshBuilder.ApplyTo(view.MapMarkers);
                }
            }

            _needsMarkerRefresh = false;
            _nextAnimationTime = Time.unscaledTime + 1.0f / _data.AnimationFramesPerSecond;
        }

        /// <summary>
        /// 味方順序から線と矢じりの保存色を取得します。
        /// </summary>
        private void GetRouteColors(int playerId, out Color line, out Color arrow)
        {
            GameplayColorSettings.GetRouteColors(playerId, out line, out arrow);
        }

        /// <summary>
        /// 保存色が変わった場合、両盤面の経路を更新します。
        /// </summary>
        private void HandleColorSettingsChanged()
        {
            _needsRebuild = true;
            _nextRefreshTime = 0.0f;
        }

        /// <summary>
        /// ゲームデータの読み込みを待ち、移動経路の共有表示設定を取得します。
        /// </summary>
        private bool TryGetDisplayData()
        {
            if (_data != null)
            {
                return true;
            }

            GameDataRepository repository = GameDataRepository.Instance;
            if (repository == null || !repository.IsLoaded)
            {
                return false;
            }

            _data = repository.GetGameData()?.MovementPathData;
            if (_data == null)
            {
                Debug.LogError("MovementPathView: GameDataにMovementPathDataが設定されていません。", this);
                enabled = false;
                return false;
            }

            _needsRebuild = true;
            return true;
        }

        /// <summary>
        /// 共有データの編集を検出して、両方の盤面へ反映します。
        /// </summary>
        private void RefreshDisplaySettings()
        {
            int hash = _data.GetConfigurationHash();
            if (_lastConfigurationHash != hash)
            {
                _lastConfigurationHash = hash;
                _needsRebuild = true;
                _nextRefreshTime = 0.0f;
            }
        }

        /// <summary>
        /// カメラ移動とズームを検出し、操作盤面の表示サイズを更新します。
        /// </summary>
        private void RefreshMacroCamera(UnitManager units)
        {
            Camera camera = units.LocalMacroUnitBehavior?.UnitCamera;
            if (camera == null)
            {
                return;
            }

            Matrix4x4 matrix = camera.projectionMatrix * camera.worldToCameraMatrix;
            if (_macroCamera != camera || _lastCameraMatrix != matrix ||
                _lastPixelHeight != camera.pixelHeight || _lastPixelWidth != camera.pixelWidth)
            {
                _macroCamera = camera;
                _lastCameraMatrix = matrix;
                _lastPixelHeight = camera.pixelHeight;
                _lastPixelWidth = camera.pixelWidth;
                _needsMacroRebuild = true;
            }
        }

        /// <summary>
        /// セルを画面へ投影し、ズームに依存しない線幅を計算する基準を取得します。
        /// </summary>
        /// <remarks>
        /// 共用マップは複数カメラで描画するため、固定のセル基準を使用します。
        /// </remarks>
        private float GetPixelsPerCell(Vector3 position, float spacing, bool isMacro)
        {
            if (!isMacro || _macroCamera == null)
            {
                return _data.MapReferenceCellPixels;
            }

            Vector3 center = _macroCamera.WorldToScreenPoint(position);
            Vector3 right = _macroCamera.WorldToScreenPoint(position + Vector3.right * spacing);
            Vector3 forward = _macroCamera.WorldToScreenPoint(position + Vector3.forward * spacing);
            if (center.z <= 0.0f || right.z <= 0.0f || forward.z <= 0.0f)
            {
                return _data.MapReferenceCellPixels;
            }

            float horizontal = new Vector2(right.x - center.x, right.y - center.y).magnitude;
            float vertical = new Vector2(forward.x - center.x, forward.y - center.y).magnitude;
            return Mathf.Max(1.0f, Mathf.Max(horizontal, vertical));
        }

        /// <summary>
        /// 存在するセルの上面位置と水平スケールを取得します。
        /// </summary>
        private bool TryGetSurfacePoint(Vector2Int coordinate, bool isMacro, out Vector3 position, out float spacing)
        {
            HexCell cell = isMacro ? _board.GetMacroPhaseHexCell(coordinate.x, coordinate.y)
                : _board.GetMapHexCell(coordinate.x, coordinate.y);
            position = Vector3.zero;
            spacing = 1.0f;
            if (cell == null)
            {
                return false;
            }

            Vector3 scale = cell.transform.lossyScale;
            spacing = Mathf.Max(0.1f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            position = cell.transform.position + Vector3.up * (Mathf.Abs(scale.y) * 0.5f + spacing * 0.01f);
            return true;
        }

        /// <summary>
        /// フェーズに合った盤面だけを描画します。
        /// </summary>
        private void SetVisibility(bool showMacro, bool showMap)
        {
            foreach (UnitPathView view in _views.Values)
            {
                bool hasPath = view.Snapshot != null && view.Snapshot.Coordinates.Count > 1;
                view.MacroRoot.SetActive(showMacro && hasPath);
                view.MapRoot.SetActive(showMap && hasPath);
            }
        }

        /// <summary>
        /// 全ユニットの表示用オブジェクトとメッシュを破棄します。
        /// </summary>
        private void ClearViews()
        {
            foreach (UnitPathView view in _views.Values)
            {
                DestroyView(view);
            }

            _views.Clear();
            _needsRebuild = true;
        }

        /// <summary>
        /// Unityが自動解放しない生成メッシュも含めて破棄します。
        /// </summary>
        private void DestroyView(UnitPathView view)
        {
            view.MacroRoot.SetActive(false);
            view.MapRoot.SetActive(false);
            Destroy(view.MacroRoot);
            Destroy(view.MapRoot);
            _renderPaths.Remove(view.MacroMesh);
            _renderPaths.Remove(view.MapMesh);
            Destroy(view.MacroMesh);
            Destroy(view.MapMesh);
            Destroy(view.MacroMarkers);
            Destroy(view.MapMarkers);
        }

        /// <summary>
        /// 無効化した表示を隠します。
        /// </summary>
        private void OnDisable()
        {
            SetVisibility(false, false);
        }

        /// <summary>
        /// イベント購読と生成した描画リソースを解放します。
        /// </summary>
        private void OnDestroy()
        {
            SettingsManager.SettingsChanged -= HandleColorSettingsChanged;
            if (_board != null)
            {
                _board.BoardCreated -= HandleBoardCreated;
            }

            ClearViews();
        }
    }
}
