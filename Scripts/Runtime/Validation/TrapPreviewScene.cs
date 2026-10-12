using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering.Universal;

using PranaTactics.Audio;
using PranaTactics.Board;
using PranaTactics.Board.Networking.Local;
using PranaTactics.Controls;
using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.Pranam;
using PranaTactics.UI;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;
using PranaTactics.Unit.Networking.Local;

namespace PranaTactics.Validation
{
    /// <summary>
    /// マクロで指定した罠へ、ミクロで生成した敵を移動させる単独確認シーンです。
    /// </summary>
    [DefaultExecutionOrder(-31900)]
    [DisallowMultipleComponent]
    public sealed class TrapPreviewScene : MonoBehaviour
    {
        /// <summary>
        /// 確認する罠の兵装データです。
        /// </summary>
        [SerializeField]
        private TrapperPranamData _trapData;

        /// <summary>
        /// 移動する敵のキャラクター名です。
        /// </summary>
        [SerializeField]
        private string _enemyCharacter = "ボット";

        /// <summary>
        /// 最初にカメラを向けるマスです。罠はクリック後に設置します。
        /// </summary>
        [SerializeField]
        private Vector2Int _initialFocus = new Vector2Int(19, 42);

        /// <summary>
        /// 敵の移動速度です。
        /// </summary>
        [SerializeField, Min(1.0f)]
        private float _enemySpeed = 30.0f;

        /// <summary>
        /// 自由カメラの移動速度です。Shift中は3倍になります。
        /// </summary>
        [SerializeField, Min(1.0f)]
        private float _cameraSpeed = 60.0f;

        /// <summary>
        /// 右ドラッグの回転感度です。
        /// </summary>
        [SerializeField, Min(0.1f)]
        private float _lookSensitivity = 3.0f;

        /// <summary>
        /// 六角形の隣接方向です。
        /// </summary>
        private static readonly Vector2Int[] s_offsets =
        {
            new Vector2Int(0, -2), new Vector2Int(-1, -1), new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(1, 1), new Vector2Int(0, 2)
        };

        /// <summary>
        /// このシーンで生成した常駐サービスです。
        /// </summary>
        private readonly List<GameObject> _services = new();

        /// <summary>
        /// 武器や手動移動を行わない確認用入力です。
        /// </summary>
        private readonly IdleInput _idleInput = new();

        /// <summary>
        /// 表示用の自由カメラです。
        /// </summary>
        private Camera _camera;

        /// <summary>
        /// マップ用フィールドを拡大表示するカメラです。
        /// </summary>
        private Camera _mapCamera;

        /// <summary>
        /// マップ確認用の描画先です。
        /// </summary>
        private RenderTexture _mapTexture;

        /// <summary>
        /// 今回の試行で生成した敵です。罠の設置者は生成しません。
        /// </summary>
        private UnitBehavior _enemy;

        /// <summary>
        /// 一人称確認時に自身の身体だけを描画から除外する対象です。
        /// </summary>
        private Renderer[] _enemyBodyRenderers = Array.Empty<Renderer>();

        /// <summary>
        /// 最後に指定した設置座標です。
        /// </summary>
        private Vector2Int? _placement;

        /// <summary>
        /// Unit視点へ切り替える前の自由カメラ位置です。
        /// </summary>
        private Vector3 _freePosition;

        /// <summary>
        /// Unit視点へ切り替える前の自由カメラ回転です。
        /// </summary>
        private Quaternion _freeRotation;

        /// <summary>
        /// 初期化完了状態です。
        /// </summary>
        private bool _isReady;

        /// <summary>
        /// Unit生成と装備初期化の待機状態です。
        /// </summary>
        private bool _isSpawning;

        /// <summary>
        /// 敵のカメラへ追従するかどうかです。
        /// </summary>
        private bool _useUnitView;

        /// <summary>
        /// 確認パネルに表示する状態です。
        /// </summary>
        private string _status = "Loading...";

        /// <summary>
        /// 通常試合の自動開始を停止し、単独起動用のサービスを準備します。
        /// </summary>
        private void Awake()
        {
            foreach (MatchManager manager in FindObjectsByType<MatchManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                manager.enabled = false;
            }
            foreach (GameplayUiSceneLoader loader in FindObjectsByType<GameplayUiSceneLoader>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                loader.enabled = false;
            }

            if (GameFlowManager.Instance != null || GameDataRepository.Instance != null || SoundManager.Instance != null)
            {
                _status = "Open TrapPreviewScene alone, then enter Play mode.";
                Debug.LogError(_status, this);
                enabled = false;
                return;
            }

            if (UIManager.Instance == null)
            {
                CreateService<UIManager>();
            }
            GameFlowManager flow = CreateService<GameFlowManager>();
            flow.enabled = false;
            flow.SetSoloPlayState(true);
            CreateService<GameDataRepository>();
            CreateService<SoundManager>();
        }

        /// <summary>
        /// シーンが所有するサービスを生成します。
        /// </summary>
        private T CreateService<T>() where T : Component
        {
            var service = new GameObject("Trap Preview " + typeof(T).Name);
            _services.Add(service);
            return service.AddComponent<T>();
        }

        /// <summary>
        /// データ、ボードと確認用カメラを初期化します。
        /// </summary>
        private async void Start()
        {
            CancellationToken token = destroyCancellationToken;
            try
            {
                float deadline = Time.realtimeSinceStartup + 60.0f;
                while (!GameDataRepository.Instance.IsLoaded)
                {
                    if (GameDataRepository.Instance.InitializationError != null)
                    {
                        throw GameDataRepository.Instance.InitializationError;
                    }
                    CheckDeadline(deadline);
                    await Awaitable.NextFrameAsync(token);
                }
                await GameplayUiSceneLoader.EnsureLoadedAsync(token);
                foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    canvas.enabled = false;
                }
                BoardManager board = BoardManager.Instance;
                if (board == null || UnitManager.Instance == null || MatchPhaseManager.Instance == null || _trapData == null)
                {
                    throw new InvalidOperationException("Board、Unit、MatchPhaseManagerまたは罠データがありません。");
                }
                LocalBoardNetwork network = board.GetComponent<LocalBoardNetwork>();
                board.Initialize(network != null ? network : board.gameObject.AddComponent<LocalBoardNetwork>());
                board.CreateBoard();
                var players = new List<PlayerInfo>
                {
                    new PlayerInfo { PlayerId = 0, TeamId = 0, CharacterName = _enemyCharacter, Coordinate = _initialFocus },
                    new PlayerInfo { PlayerId = 1, TeamId = 1, CharacterName = _enemyCharacter, Coordinate = _initialFocus }
                };
                var service = new LocalPlayerService();
                service.Initialize(0, players, false);
                UnitManager.Instance.Initialize(service, new LocalUnitFactory());
                CreateCameras();
                MatchPhaseManager.Instance.ChangePhase(MatchPhase.Macro);
                FocusCamera(_initialFocus);
                HUDManager.Instance?.EnablePanel(false);
                SoundManager.Instance.StopBGM();
                _status = "Macro: Left-click a cell to place the trap.";
                _isReady = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// 自由視点と、罠の周囲を表示するマップ用カメラを生成します。
        /// </summary>
        private void CreateCameras()
        {
            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                camera.enabled = false;
            }
            foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                listener.enabled = false;
            }
            var cameraObject = new GameObject("Trap Preview Free Camera");
            cameraObject.transform.SetParent(transform);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 1500.0f;
            _camera.fieldOfView = 60.0f;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.AddComponent<AudioListener>();

            var mapObject = new GameObject("Trap Preview Map Camera");
            mapObject.transform.SetParent(transform);
            _mapCamera = mapObject.AddComponent<Camera>();
            _mapCamera.CopyFrom(BoardManager.Instance.GetMinimapCameraObject().GetComponent<Camera>());
            _mapCamera.enabled = true;
            _mapCamera.orthographic = true;
            _mapCamera.orthographicSize = 75.0f;
            mapObject.AddComponent<UniversalAdditionalCameraData>();
            _mapTexture = new RenderTexture(512, 512, 24);
            _mapCamera.targetTexture = _mapTexture;
        }

        /// <summary>
        /// フェイズに対応するフィールドへカメラを向け、マップも同じ座標へ寄せます。
        /// </summary>
        private void FocusCamera(Vector2Int coordinate)
        {
            BoardManager board = BoardManager.Instance;
            HexCell cell = MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Macro
                ? board.GetMacroPhaseHexCell(coordinate.x, coordinate.y)
                : board.GetMicroPhaseHexCell(coordinate.x, coordinate.y);
            if (cell == null)
            {
                throw new ArgumentException($"セル {coordinate} が存在しません。");
            }
            if (!cell.TryGetSurfacePosition(out Vector3 focusPosition))
            {
                throw new InvalidOperationException($"セル {coordinate} の床面を取得できません。");
            }
            _camera.transform.position = focusPosition + new Vector3(0.0f, 70.0f, -65.0f);
            _camera.transform.LookAt(focusPosition);
            HexCell map = board.GetMapHexCell(coordinate.x, coordinate.y);
            _mapCamera.transform.position = map.transform.position + Vector3.up * 150.0f;
            _mapCamera.transform.rotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
        }

        /// <summary>
        /// フェイズ切替、設置マス選択と自由カメラ入力を処理します。
        /// </summary>
        private void Update()
        {
            if (!_isReady || _isSpawning)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                SwitchPhase();
            }
            if (Input.GetKeyDown(KeyCode.V))
            {
                ToggleUnitView();
            }
            if (Input.GetMouseButtonDown(0) && MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Macro && !IsPointerOverPanel())
            {
                SelectPlacement();
            }
            if (!_useUnitView)
            {
                MoveFreeCamera();
            }
            if (_enemy != null)
            {
                _status = $"Micro | Enemy HP: {_enemy.Status.CurrentHealth:0.0} | Cell: {_enemy.MicroUnitBehavior.GetCurrentCoordinate()}";
            }
        }

        /// <summary>
        /// マクロの実際のセルColliderからクリック先を取得します。
        /// </summary>
        private void SelectPlacement()
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 2000.0f, LayerMask.GetMask("Space"), QueryTriggerInteraction.Collide))
            {
                return;
            }
            HexCell cell = hit.collider.GetComponentInParent<HexCell>();
            if (cell == null)
            {
                return;
            }
            Vector2Int coordinate = cell.GetCoordinate();
            if (BoardManager.Instance.GetMacroPhaseHexCell(coordinate.x, coordinate.y) != cell)
            {
                return;
            }
            BoardManager.Instance.Traps.ClearTraps();
            if (BoardManager.Instance.Traps.TryPlacePreviewTrap(_trapData, coordinate, 0))
            {
                _placement = coordinate;
                _status = $"Trap: {coordinate}, floor {cell.GetFloor()} | Tab / Micro to start.";
                HexCell map = BoardManager.Instance.GetMapHexCell(coordinate.x, coordinate.y);
                _mapCamera.transform.position = map.transform.position + Vector3.up * 150.0f;
            }
        }

        /// <summary>
        /// マクロへ戻ると試行をリセットし、ミクロへ進むと敵を生成します。
        /// </summary>
        private void SwitchPhase()
        {
            if (!_isReady || _isSpawning)
            {
                return;
            }
            if (MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Macro)
            {
                if (!_placement.HasValue)
                {
                    _status = "Place a trap before switching to Micro.";
                    return;
                }
                _ = StartTrialAsync();
                return;
            }
            _useUnitView = false;
            MatchPhaseManager.Instance.ChangePhase(MatchPhase.Macro);
            RemoveEnemy();
            BoardManager.Instance.Traps.ClearTraps();
            BoardManager.Instance.Traps.TryPlacePreviewTrap(_trapData, _placement.Value, 0);
            FocusCamera(_placement.Value);
            _status = "Macro: Click to replace the trap. Tab / Micro to retry.";
        }

        /// <summary>
        /// 同じ高さの隣接マスを探索し、起動範囲のすぐ外から設置地点への経路を作ります。
        /// </summary>
        private List<Vector2Int> CreateApproachPath(Vector2Int center)
        {
            BoardManager board = BoardManager.Instance;
            int floor = board.GetMicroPhaseHexCell(center.x, center.y).GetFloor();
            var queue = new Queue<Vector2Int>();
            var parents = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(center);
            parents.Add(center, center);
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (BoardTrapRegistry.GetHexDistance(center, current) > _trapData.TriggerRange)
                {
                    var path = new List<Vector2Int> { current };
                    while (current != center)
                    {
                        current = parents[current];
                        path.Add(current);
                    }
                    return path;
                }
                foreach (Vector2Int offset in s_offsets)
                {
                    Vector2Int next = current + offset;
                    HexCell cell = board.GetMicroPhaseHexCell(next.x, next.y);
                    if (cell != null && cell.GetFloor() == floor && parents.TryAdd(next, current))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
            throw new InvalidOperationException("同じ高さで起動範囲外から進入できる経路がありません。別のマスを選択してください。");
        }

        /// <summary>
        /// 敵一体だけを生成し、通常のマス移動によって検知と発動を確認します。
        /// </summary>
        private async Awaitable StartTrialAsync()
        {
            _isSpawning = true;
            try
            {
                List<Vector2Int> path = CreateApproachPath(_placement.Value);
                Vector2Int start = path[0];
                path.RemoveAt(0);
                MatchPhaseManager.Instance.ChangePhase(MatchPhase.Micro);
                FocusCamera(_placement.Value);
                _enemy = UnitManager.Instance.CreateUnit(new UnitInitializeData
                {
                    OwnerId = 1, TeamId = 1, LocalTeamId = 0, CharacterName = _enemyCharacter,
                    StartX = start.x, StartY = start.y
                });
                if (_enemy == null)
                {
                    throw new InvalidOperationException("確認用の敵Unitを生成できません。");
                }
                _enemy.MicroUnitBehavior.SetUnitInput(_idleInput);
                _enemyBodyRenderers = _enemy.MicroUnitBehavior.GetModel().GetComponentsInChildren<Renderer>(true);
                float deadline = Time.realtimeSinceStartup + 30.0f;
                while (!_enemy.TacticalMapUnitBehavior.IsPranamInitialized)
                {
                    if (_enemy.TacticalMapUnitBehavior.HasPranamInitializationFailed)
                    {
                        throw new InvalidOperationException("敵Unitの兵装初期化に失敗しました。");
                    }
                    CheckDeadline(deadline);
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
                _enemy.MicroUnitBehavior.PrepareForMicroPhase();
                _enemy.Status.SetActionPoint(100000);
                _enemy.MicroUnitBehavior.SetProactiveness(100000);
                _enemy.Status.SetMoveSpeed(_enemySpeed);
                _enemy.Status.SetWalkMoveSpeed(_enemySpeed);
                _enemy.Status.SetDashMoveSpeed(_enemySpeed);
                _enemy.Status.SetCurrentMoveSpeed(_enemySpeed);
                _enemy.Status.SetMigrationPath(path);
                _enemy.Status.BeginMigrationExecution();
                Vector3 direction = BoardManager.Instance.GetMicroPhaseHexCellPosition(path[0].x, path[0].y) - _enemy.MicroUnitBehavior.transform.position;
                _enemy.MicroUnitBehavior.AlignPerspectiveAndPranamYaw(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
                _enemy.MicroUnitBehavior.SetPitch(0.0f);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                RemoveEnemy();
                MatchPhaseManager.Instance.ChangePhase(MatchPhase.Macro);
                FocusCamera(_placement.Value);
                _status = exception.Message;
                Debug.LogWarning(_status, this);
            }
            finally
            {
                _isSpawning = false;
            }
        }

        /// <summary>
        /// 右ボタン中にUnityエディター相当の移動と回転を行います。
        /// </summary>
        private void MoveFreeCamera()
        {
            if (!Input.GetMouseButton(1))
            {
                return;
            }
            Vector3 angles = _camera.transform.eulerAngles;
            float pitch = Mathf.DeltaAngle(0.0f, angles.x) - Input.GetAxisRaw("Mouse Y") * _lookSensitivity;
            float yaw = angles.y + Input.GetAxisRaw("Mouse X") * _lookSensitivity;
            _camera.transform.rotation = Quaternion.Euler(Mathf.Clamp(pitch, -89.0f, 89.0f), yaw, 0.0f);
            float horizontal = (Input.GetKey(KeyCode.D) ? 1.0f : 0.0f) - (Input.GetKey(KeyCode.A) ? 1.0f : 0.0f);
            float forward = (Input.GetKey(KeyCode.W) ? 1.0f : 0.0f) - (Input.GetKey(KeyCode.S) ? 1.0f : 0.0f);
            float vertical = (Input.GetKey(KeyCode.E) ? 1.0f : 0.0f) - (Input.GetKey(KeyCode.Q) ? 1.0f : 0.0f);
            Vector3 direction = _camera.transform.right * horizontal + _camera.transform.forward * forward + Vector3.up * vertical;
            float speed = _cameraSpeed * (Input.GetKey(KeyCode.LeftShift) ? 3.0f : 1.0f);
            _camera.transform.position += Vector3.ClampMagnitude(direction, 1.0f) * speed * Time.unscaledDeltaTime;
        }

        /// <summary>
        /// 自由視点を保存・復元し、敵の目線への追従を切り替えます。
        /// </summary>
        private void ToggleUnitView()
        {
            if (_enemy == null || _enemy.MicroUnitBehavior.IsDead)
            {
                return;
            }
            if (!_useUnitView)
            {
                _freePosition = _camera.transform.position;
                _freeRotation = _camera.transform.rotation;
            }
            else
            {
                _camera.transform.SetPositionAndRotation(_freePosition, _freeRotation);
            }
            _useUnitView = !_useUnitView;
        }

        /// <summary>
        /// 既存カメラとの競合を避け、選択した視点とカーソル状態を維持します。
        /// </summary>
        private void LateUpdate()
        {
            if (!_isReady)
            {
                return;
            }
            if (_useUnitView)
            {
                if (_enemy == null || _enemy.MicroUnitBehavior.IsDead)
                {
                    _useUnitView = false;
                    _camera.transform.SetPositionAndRotation(_freePosition, _freeRotation);
                }
                else
                {
                    Transform source = _enemy.MicroUnitBehavior.UnitCamera.transform;
                    _camera.transform.SetPositionAndRotation(source.position, source.rotation);
                }
            }
            if (_enemy != null)
            {
                if (_enemy.Status.MigrationPath.Count > 0)
                {
                    Vector2Int next = _enemy.Status.MigrationPath[0];
                    Vector3 direction = BoardManager.Instance.GetMicroPhaseHexCellPosition(next.x, next.y) - _enemy.MicroUnitBehavior.transform.position;
                    if (direction.sqrMagnitude > 0.01f)
                    {
                        _enemy.MicroUnitBehavior.AlignPerspectiveAndPranamYaw(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
                    }
                }
                _enemy.MicroUnitBehavior.UnitCamera.enabled = false;
            }
            foreach (Renderer renderer in _enemyBodyRenderers)
            {
                if (renderer != null)
                {
                    renderer.forceRenderingOff = _useUnitView;
                }
            }
            Cursor.lockState = Input.GetMouseButton(1) && !_useUnitView ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = Cursor.lockState == CursorLockMode.None;
        }

        /// <summary>
        /// 操作パネルおよびマップ表示上のクリックを設置対象から除外します。
        /// </summary>
        private static bool IsPointerOverPanel()
        {
            Vector2 point = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return new Rect(10.0f, 10.0f, 520.0f, 160.0f).Contains(point) ||
                new Rect(Screen.width - 270.0f, 10.0f, 260.0f, 285.0f).Contains(point);
        }

        /// <summary>
        /// フェイズ、視点、再試行の操作とマップ用フィールドを表示します。
        /// </summary>
        private void OnGUI()
        {
            GUI.Box(new Rect(10.0f, 10.0f, 520.0f, 160.0f), "Trap Preview");
            GUI.Label(new Rect(20.0f, 35.0f, 500.0f, 45.0f), _status);
            GUI.enabled = _isReady && !_isSpawning;
            if (GUI.Button(new Rect(20.0f, 85.0f, 150.0f, 28.0f), "Switch Phase [Tab]"))
            {
                SwitchPhase();
            }
            GUI.enabled = _isReady && !_isSpawning && _enemy != null;
            if (GUI.Button(new Rect(180.0f, 85.0f, 150.0f, 28.0f), _useUnitView ? "Free View [V]" : "Unit View [V]"))
            {
                ToggleUnitView();
            }
            GUI.enabled = _isReady && !_isSpawning;
            if (GUI.Button(new Rect(340.0f, 85.0f, 170.0f, 28.0f), "Focus Trap"))
            {
                _useUnitView = false;
                FocusCamera(_placement ?? _initialFocus);
            }
            GUI.enabled = true;
            GUI.Label(new Rect(20.0f, 120.0f, 500.0f, 45.0f), "RMB + WASD: move | Q/E: down/up | Shift: faster\nMacro click: replace trap | Micro: spawn one approaching enemy");
            if (_mapTexture != null)
            {
                GUI.Box(new Rect(Screen.width - 270.0f, 10.0f, 260.0f, 285.0f), "Map field (allied trap display)");
                GUI.DrawTexture(new Rect(Screen.width - 265.0f, 35.0f, 250.0f, 250.0f), _mapTexture, ScaleMode.ScaleToFit);
            }
        }

        /// <summary>
        /// 生成済みUnitを管理表から外し、次の試行に備えて破棄します。
        /// </summary>
        private void RemoveEnemy()
        {
            if (UnitManager.Instance != null && UnitManager.Instance.TryGetUnit(1, out UnitBehavior unit))
            {
                UnitManager.Instance.UnregisterUnit(1);
                Destroy(unit.gameObject);
            }
            _enemy = null;
            _enemyBodyRenderers = Array.Empty<Renderer>();
        }

        /// <summary>
        /// 初期化待機が終了しない場合に失敗を報告します。
        /// </summary>
        private static void CheckDeadline(float deadline)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                throw new TimeoutException("確認シーンの初期化がタイムアウトしました。");
            }
        }

        /// <summary>
        /// 初期化失敗をパネルとConsoleに表示します。
        /// </summary>
        private void Fail(Exception exception)
        {
            _status = exception.Message;
            Debug.LogException(exception, this);
            enabled = false;
        }

        /// <summary>
        /// 確認シーン専用の常駐サービスと描画先を解放します。
        /// </summary>
        private void OnDestroy()
        {
            foreach (GameObject service in _services)
            {
                if (service != null)
                {
                    Destroy(service);
                }
            }
            if (_mapTexture != null)
            {
                _mapTexture.Release();
                Destroy(_mapTexture);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// 確認中の敵が移動経路以外の操作を行わないようにします。
        /// </summary>
        private sealed class IdleInput : IUnitInput
        {
            public UnitInputState CurrentState => UnitInputState.CreateDefault();
        }
    }
}
