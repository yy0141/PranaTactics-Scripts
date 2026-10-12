#if UNITY_EDITOR || PV_CAPTURE
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using PranaTactics.Audio;
using PranaTactics.Board;
using PranaTactics.Board.Networking.Local;
using PranaTactics.Controls;
using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.UI;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;
using PranaTactics.Unit.Networking.Local;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PranaTactics.Promo
{
    /// <summary>
    /// 製作者用の撮影環境を、通常試合またはJSON演出のモードに応じて組み立てます。
    /// </summary>
    /// <remarks>
    /// JSON演出では通常試合とAIを停止し、通常試合モードではInGameと同じ進行へ制御を渡します。
    /// </remarks>
    [DefaultExecutionOrder(-31900)]
    public sealed class PvDirector : MonoBehaviour
    {
        [SerializeField]
        private TextAsset _scenarioAsset;

        [SerializeField, Tooltip("ON: 通常のソロ試合＋F6でUI切替。OFF: 従来のJSON演出＋Space開始。")]
        private bool _useNormalGameplay;

        [SerializeField]
        private TextAsset _gameplayScenarioAsset;

        [SerializeField]
        private string _gameplayCharacter = "黒瀬 凛華";

        [SerializeField, Min(1)]
        private int _gameplayPlayersPerTeam = 3;

        private readonly Dictionary<string, PvActor> _actors = new Dictionary<string, PvActor>();

        private readonly List<GameObject> _services = new List<GameObject>();

        private readonly PvPresentation _presentation = new PvPresentation();

        private PvScenario _scenario;

        private PvCameraRig _rig;

        private PvDuelSequence _duel;

        private float _time;

        private int _nextCue;

        private float _playbackScale = 1;

        private float _originalTimeScale;

        private float _originalFixedDeltaTime;

        private bool _originalAudioPause;

        private bool _isReady;

        private bool _isPaused;

        private bool _isWaitingForStart;

        private void Awake()
        {
            if (_useNormalGameplay)
            {
                PvGameplayCapture capture = gameObject.AddComponent<PvGameplayCapture>();

                capture.Initialize(_gameplayCharacter, _gameplayPlayersPerTeam, _gameplayScenarioAsset);
                enabled = false;

                return;
            }

            _originalTimeScale = Time.timeScale;
            _originalFixedDeltaTime = Time.fixedDeltaTime;
            _originalAudioPause = AudioListener.pause;

            try
            {
                DisableAutomaticSceneStartup();

                if (GameFlowManager.Instance != null || GameDataRepository.Instance != null ||
                    SoundManager.Instance != null)
                {
                    throw new InvalidOperationException("PVSceneを単独で開いてPlayしてください。通常ゲームからの遷移には対応していません。");
                }

                if (_scenarioAsset == null)
                {
                    throw new InvalidOperationException("Scenario Assetに撮影JSONを指定してください。");
                }

                _scenario = PvScenario.Parse(ReadScenario());

                if (UIManager.Instance == null)
                {
                    CreateService<UIManager>();
                }

                GameFlowManager flow = CreateService<GameFlowManager>();

                flow.enabled = false;
                flow.SetSoloPlayState(true);
                CreateService<GameDataRepository>();
                CreateService<SoundManager>();
                Time.timeScale = 1;
                AudioListener.pause = false;
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        /// <summary>
        /// JSONの検証失敗時にも通常試合と共通UIのStartが走らないよう、初期化前に停止します。
        /// </summary>
        private void DisableAutomaticSceneStartup()
        {
            foreach (MatchManager manager in FindObjectsByType<MatchManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (manager.gameObject.scene == gameObject.scene)
                {
                    manager.enabled = false;
                }
            }

            foreach (GameplayUiSceneLoader loader in FindObjectsByType<GameplayUiSceneLoader>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (loader.gameObject.scene == gameObject.scene)
                {
                    loader.enabled = false;
                }
            }
        }

        private string ReadScenario()
        {
            string[] arguments = Environment.GetCommandLineArgs();

            for (int index = 0; index < arguments.Length; index++)
            {
                if (arguments[index] == "-pvScenario")
                {
                    if (index + 1 >= arguments.Length)
                    {
                        throw new ArgumentException("-pvScenario の後にJSONファイルのパスが必要です。");
                    }

                    return File.ReadAllText(arguments[index + 1]);
                }
            }

            return _scenarioAsset.text;
        }

        private T CreateService<T>() where T : Component
        {
            var service = new GameObject("PV " + typeof(T).Name);

            _services.Add(service);

            return service.AddComponent<T>();
        }

        private async void Start()
        {
            if (!enabled)
            {
                return;
            }

            CancellationToken token = destroyCancellationToken;

            try
            {
                float deadline = Time.realtimeSinceStartup + 60;

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

                if (InputManager.Instance != null && PranamUIManager.Instance != null)
                {
                    PranamUIManager.Instance.Initialize(new BattlePranamSelector(InputManager.Instance.InputContext,
                        () => PranaTactics.Settings.SettingsManager.Current.Controls));
                }

                BoardManager board = BoardManager.Instance;

                if (board == null || UnitManager.Instance == null || MatchPhaseManager.Instance == null)
                {
                    throw new InvalidOperationException("InGame由来のBoard/Unit/MatchPhaseManagerが必要です。");
                }

                LocalBoardNetwork network = board.GetComponent<LocalBoardNetwork>();

                if (network == null)
                {
                    network = board.gameObject.AddComponent<LocalBoardNetwork>();
                }

                board.Initialize(network);
                board.CreateBoard();
                ValidateActors(board);
                CreateActors();
                InputManager.Instance?.SetInputSource(_actors[_scenario.Actors[0].Id]);
                deadline = Time.realtimeSinceStartup + 30;

                foreach (PvActor actor in _actors.Values)
                {
                    while (!actor.Unit.TacticalMapUnitBehavior.IsPranamInitialized)
                    {
                        if (actor.Unit.TacticalMapUnitBehavior.HasPranamInitializationFailed)
                        {
                            throw new InvalidOperationException("撮影ユニットの武器初期化に失敗しました。");
                        }

                        CheckDeadline(deadline);
                        await Awaitable.NextFrameAsync(token);
                    }

                    actor.Unit.MicroUnitBehavior.PrepareForMicroPhase();
                }

                MatchPhaseManager.Instance.ChangePhase(MatchPhase.Micro);
                await Awaitable.NextFrameAsync(token);
                CreateCamera();
                HUDManager.Instance?.EnablePanel(false);
                SoundManager.Instance.StopBGM();
                _presentation.SetVisible(_scenario.ShowUi);
                SetPaused(true);
                ApplyCues();

                if (_scenario.Duel != null)
                {
                    _duel = new PvDuelSequence(_scenario, _actors, _rig, SetPlaybackScale);
                }

                foreach (PvActor actor in _actors.Values)
                {
                    actor.Tick(0);
                }

                _rig.Tick(0);
                _isWaitingForStart = true;
                _isReady = true;
                UnitBehavior.DamageApplied += OnDamageApplied;
                Debug.Log("PV撮影準備完了: Spaceで開始 / F7 再読込 / F8 一時停止 / F9 自由カメラ / F6 UI / F11 カメラ位置。", this);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private static void CheckDeadline(float deadline)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                throw new TimeoutException("PV撮影の初期化がタイムアウトしました。");
            }
        }

        private void ValidateActors(BoardManager board)
        {
            foreach (PvActorDefinition actor in _scenario.Actors)
            {
                if (!board.ContainsHexCell(actor.X, actor.Y) ||
                    GameDataRepository.Instance.GetCharacterData(
                    actor.Character) == null)
                {
                    throw new ArgumentException(
                        $"{actor.Id}: Characterまたは配置セル ({actor.X}, {actor.Y}) が存在しません。");
                }
            }

            var weaponCues = new List<PvCue>(_scenario.Cues);

            if (_scenario.Duel != null)
            {
                PvDuelDefinition duel = _scenario.Duel;

                weaponCues.Add(
                    new PvCue { Action = PvAction.SelectWeapon, Actor = duel.Shooter, Slot = duel.OpeningSlot, Weapon = "ゲラーデ" });
                weaponCues.Add(
                    new PvCue { Action = PvAction.SelectWeapon, Actor = duel.Defender, IsMain = false, Slot = duel.ShieldSlot, Weapon = "パルマ" });
                weaponCues.Add(
                    new PvCue { Action = PvAction.SelectWeapon, Actor = duel.Flanker, IsMain = false, Slot = duel.FlankSlot, Weapon = "ゲラーデ" });
            }

            foreach (PvCue cue in weaponCues)
            {
                if (cue.Action == PvAction.Move)
                {
                    foreach (PvCell cell in cue.Path)
                    {
                        if (!board.ContainsHexCell(cell.X, cell.Y))
                        {
                            throw new ArgumentException($"{cue.Actor}: 移動不可のセル {cell} が経路にあります。");
                        }
                    }
                }

                if (cue.Action == PvAction.Fire || cue.Action == PvAction.SelectWeapon)
                {
                    PvActorDefinition actor = Array.Find(_scenario.Actors, item => item.Id == cue.Actor);
                    List<List<PranaTactics.Pranam.PranamData>> weapons = GameDataRepository.Instance.GetPranams(
                        actor.Character);

                    if (cue.Slot >= weapons[cue.IsMain ? 0 : 1].Count)
                    {
                        throw new ArgumentException($"{cue.Actor}: 武器スロット {cue.Slot} が存在しません。");
                    }

                    if (!string.IsNullOrEmpty(cue.Weapon) &&
                        weapons[cue.IsMain ? 0 : 1][cue.Slot].DisplayName != cue.Weapon)
                    {
                        throw new ArgumentException($"{cue.Actor}: 指定スロットの装備が '{cue.Weapon}' と一致しません。");
                    }
                }
            }
        }

        private void CreateActors()
        {
            var players = new List<PlayerInfo>();

            for (int index = 0; index < _scenario.Actors.Length; index++)
            {
                PvActorDefinition definition = _scenario.Actors[index];

                players.Add(new PlayerInfo
                {
                    PlayerId = index,
                    TeamId = definition.Team,
                    CharacterName = definition.Character,
                    Coordinate = new Vector2Int(definition.X, definition.Y)
                });
            }

            var service = new LocalPlayerService();

            service.Initialize(0, players, false);
            UnitManager.Instance.Initialize(service, new LocalUnitFactory());

            for (int index = 0; index < players.Count; index++)
            {
                PvActorDefinition definition = _scenario.Actors[index];
                UnitBehavior unit = UnitManager.Instance.CreateUnit(new UnitInitializeData
                {
                    OwnerId = index,
                    TeamId = definition.Team,
                    LocalTeamId = players[0].TeamId,
                    CharacterName = definition.Character,
                    StartX = definition.X,
                    StartY = definition.Y
                });

                if (unit == null || unit.MicroUnitBehavior == null)
                {
                    throw new InvalidOperationException($"{definition.Id}: Unitを生成できませんでした。");
                }

                _actors.Add(definition.Id, new PvActor(unit, definition.Speed));
            }
        }

        private void CreateCamera()
        {
            foreach (PvActor actor in _actors.Values)
            {
                actor.Unit.ShowBodyForCapture();
            }

            Camera source = UnitManager.Instance.LocalMicroUnitBehavior.UnitCamera;

            _rig = new GameObject("PV Camera").AddComponent<PvCameraRig>();
            _rig.Initialize(source);
            _rig.transform.position = source.transform.position;
            _rig.transform.rotation = source.transform.rotation;

            foreach (Camera camera in FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (camera != _rig.Camera && camera.targetTexture == null)
                {
                    camera.enabled = false;
                }
            }

            foreach (AudioListener listener in FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                listener.enabled = listener.gameObject == _rig.gameObject;
            }
        }

        private void Update()
        {
            if (!_isReady)
            {
                return;
            }

            if (!_isWaitingForStart && Input.GetKeyDown(KeyCode.F8) && _time < _scenario.Duration)
            {
                SetPaused(!_isPaused);
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                _rig.ToggleFree();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                _presentation.SetVisible(!_presentation.IsVisible);
            }

            if (Input.GetKeyDown(KeyCode.F11))
            {
                Debug.Log(
                    $"PV Camera Position={_rig.transform.position:F3} Rotation={_rig.transform.eulerAngles:F3} FOV={_rig.Camera.fieldOfView}",
                    this);
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                _isReady = false;
                Restart();

                return;
            }

            if (_isWaitingForStart)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    _isWaitingForStart = false;
                    SetPaused(false);
                    Debug.Log("PV撮影開始。", this);
                }

                return;
            }

            if (_isPaused)
            {
                return;
            }

            try
            {
                _time = Mathf.Min(_scenario.Duration, _time + Time.deltaTime);
                ApplyCues();
                _duel?.Tick(_time, Time.unscaledDeltaTime);

                foreach (PvActor actor in _actors.Values)
                {
                    actor.Tick(_time);
                }

                if (_duel != null && _time >= _scenario.Duration && !_duel.IsComplete)
                {
                    throw new TimeoutException("Duelの完了前にDurationへ到達しました。射程と演出時間を確認してください。");
                }

                if (_time >= _scenario.Duration || _duel != null && _duel.IsComplete)
                {
                    SetPaused(true);
                }
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private async void Restart()
        {
            try
            {
                SetPaused(true);

                foreach (GameObject service in _services)
                {
                    Destroy(service);
                }

                await Awaitable.NextFrameAsync(destroyCancellationToken);
#if UNITY_EDITOR
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(gameObject.scene.path,
                    new LoadSceneParameters(LoadSceneMode.Single));
#else
                SceneManager.LoadSceneAsync(0, LoadSceneMode.Single);
#endif
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void LateUpdate()
        {
            if (!_isReady)
            {
                return;
            }

            _presentation.Refresh();
            _rig.Tick(_time, _isPaused ? 0 : Time.unscaledDeltaTime);
            _duel?.ApplyVisibility();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
        }

        private void ApplyCues()
        {
            while (_nextCue < _scenario.Cues.Length && _scenario.Cues[_nextCue].Time <= _time)
            {
                PvCue cue = _scenario.Cues[_nextCue++];
                PvActor actor = cue.Actor != null && _actors.TryGetValue(cue.Actor, out PvActor found) ? found : null;
                PvActor target = !string.IsNullOrEmpty(cue.Target) ? _actors[cue.Target] : null;

                switch (cue.Action)
                {
                    case PvAction.Move:
                        ValidateRoute(actor, cue);
                        var path = new List<Vector2Int>();
                        foreach (PvCell cell in cue.Path)
                        {
                            path.Add(new Vector2Int(cell.X, cell.Y));
                        }
                        actor.Unit.Status.SetMigrationPath(path);
                        actor.Unit.Status.BeginMigrationExecution();
                        break;
                    case PvAction.Speed:
                        actor.SetSpeed(cue.Value);
                        break;
                    case PvAction.Aim:
                        actor.Aim(target, cue.Yaw, cue.Pitch);
                        break;
                    case PvAction.Fire:
                        actor.Fire(cue);
                        break;
                    case PvAction.SelectWeapon:
                        actor.SelectWeapon(cue);
                        break;
                    case PvAction.CheckDamage:
                        actor.CheckDamage(cue.Enabled, target);
                        Debug.Log(
                            $"PV検証 {cue.Time:0.00}s {cue.Actor}: {(cue.Enabled ? "被弾を確認" : "HP維持を確認")}",
                            this);
                        break;
                    case PvAction.Camera:
                        var targets = new List<PvActor>();
                        if (cue.Targets != null)
                        {
                            foreach (string id in cue.Targets)
                            {
                                targets.Add(_actors[id]);
                            }
                        }
                        _rig.SetShot(cue, target, targets.ToArray());
                        break;
                    case PvAction.Ui:
                        _presentation.SetVisible(cue.Enabled);
                        break;
                    case PvAction.Audio:
                        SoundManager.Instance.SEMasterVolume = cue.Value;
                        break;
                    case PvAction.TimeScale:
                        SetPlaybackScale(cue.Value);
                        break;
                }
            }
        }

        private void OnDamageApplied(UnitBehavior unit, int attackerId)
        {
            _duel?.RecordDamage(unit, attackerId);

            foreach (PvActor actor in _actors.Values)
            {
                if (actor.Unit == unit)
                {
                    actor.RecordDamage(attackerId);
                }
            }

            if (_isReady && !_isPaused)
            {
                _rig.ReactToDamage(unit, _time);
            }
        }

        private static void ValidateRoute(PvActor actor, PvCue cue)
        {
            Vector2Int previous = actor.Unit.MicroUnitBehavior.GetCurrentCoordinate();

            if (actor.Unit.Status.IsMovable)
            {
                throw new ArgumentException($"{cue.Actor}: 移動中は経路を差し替えられません。到着後にMoveを指定してください。");
            }

            foreach (PvCell point in cue.Path)
            {
                var cell = new Vector2Int(point.X, point.Y);

                if (!BoardManager.Instance.GetAdjacentHexCellCoordinates(previous.x, previous.y).Contains(
                    cell))
                {
                    throw new ArgumentException(
                        $"{cue.Actor}: {previous} から {cell} は隣接していません。移動中の経路差し替えは到着後に指定してください。");
                }

                previous = cell;
            }
        }

        private void SetPlaybackScale(float scale)
        {
            _playbackScale = scale;
            Time.fixedDeltaTime = _originalFixedDeltaTime * scale;
            Time.timeScale = _isPaused ? 0 : scale;
        }

        private void SetPaused(bool paused)
        {
            _isPaused = paused;
            Time.timeScale = paused ? 0 : _playbackScale;
            AudioListener.pause = paused;

            foreach (PvActor actor in _actors.Values)
            {
                if (actor.Unit != null)
                {
                    actor.Unit.MicroUnitBehavior.enabled = !paused;
                }
            }
        }

        private void Fail(Exception exception)
        {
            Debug.LogException(exception, this);
            _isReady = false;
            enabled = false;
            Time.timeScale = _originalTimeScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            AudioListener.pause = _originalAudioPause;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(1);
#endif
        }

        private void OnDestroy()
        {
            if (_useNormalGameplay)
            {
                return;
            }

            _duel?.Dispose();
            UnitBehavior.DamageApplied -= OnDamageApplied;
            Time.timeScale = _originalTimeScale;
            Time.fixedDeltaTime = _originalFixedDeltaTime;
            AudioListener.pause = _originalAudioPause;
            _presentation.Restore();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_rig != null)
            {
                Destroy(_rig.gameObject);
            }

            foreach (GameObject service in _services)
            {
                if (service != null)
                {
                    Destroy(service);
                }
            }
        }
    }
}
#endif
