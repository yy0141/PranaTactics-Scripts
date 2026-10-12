using UnityEngine;

using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;
using PranaTactics.Board;

namespace PranaTactics.Match.Phases
{
    /// <summary>
    /// 計画した移動経路に沿って移動しながら、
    /// リアルタイムで戦闘を行うMicro Phaseを管理します。
    /// </summary>
    public sealed class MicroPhaseState : MatchPhaseState
    {
        /// <summary>
        /// Micro Phaseの制限時間と最大ターン数を保持するゲーム設定です。
        /// </summary>
        private GameData _gameData;

        /// <summary>
        /// 次のフェイズまたはリザルトへの遷移を
        /// 要求済みかどうかを示します。
        /// </summary>
        private bool _hasRequestedTransition;

        private MicroPhaseCameraTransition _cameraTransition;

        private float _transitionElapsed;

        private float _transitionDuration;

        private bool _hasStartedCombat;

        /// <summary>
        /// Micro Phaseを開始し、現在のフェイズと
        /// タイマー表示を更新します。
        /// </summary>
        public override void Enter()
        {
            _hasRequestedTransition = false;
            _hasStartedCombat = false;
            _transitionElapsed = 0.0f;

            if (!TryGetRequiredManagers())
            {
                return;
            }

            if (!TryLoadGameData())
            {
                return;
            }

            MatchPhaseManager phaseManager = MatchPhaseManager.Instance;

            _transitionDuration = phaseManager.CurrentPhase == MatchPhase.Macro
                ? phaseManager.MicroTransitionDuration : 0.0f;

            if (_transitionDuration > 0.0f)
            {
                HUDManager.Instance.StopTimer();
                phaseManager.ChangePhase(MatchPhase.MicroTransition);
                UnitManager unitManager = UnitManager.Instance;
                GameObject fullMapCamera = BoardManager.Instance != null
                    ? BoardManager.Instance.GetMinimapCameraObject() : null;

                _cameraTransition = new MicroPhaseCameraTransition(
                    unitManager != null ? unitManager.LocalMacroUnitBehavior : null,
                    unitManager != null ? unitManager.LocalMicroUnitBehavior : null,
                    fullMapCamera != null ? fullMapCamera.transform : null);
                HUDManager.Instance.SetMapMode(false);

                return;
            }

            StartCombat();
        }

        /// <summary>
        /// カメラをFPS位置へ確定してから戦闘と制限時間の計測を開始します。
        /// </summary>
        private void StartCombat()
        {
            _cameraTransition?.Restore();
            _cameraTransition = null;
            _hasStartedCombat = true;
            MatchPhaseManager.Instance.ChangePhase(MatchPhase.Micro);

            HUDManager.Instance.SetTimer(
                _gameData.MicroPhaseDuration);
        }

        /// <summary>
        /// Micro Phaseの制限時間を監視します。
        /// 最終ターンの場合はリザルトへ遷移し、
        /// それ以外の場合は次のMacro Phaseへ遷移します。
        /// </summary>
        public override void UpdateState()
        {
            if (_hasRequestedTransition)
            {
                return;
            }

            if (!_hasStartedCombat)
            {
                if (_gameData == null)
                {
                    return;
                }

                _transitionElapsed += Time.deltaTime;
                _cameraTransition?.Apply(_transitionElapsed / _transitionDuration);

                if (_transitionElapsed >= _transitionDuration)
                {
                    StartCombat();
                }

                return;
            }

            if (GameFlowManager.Instance == null ||
                !GameFlowManager.Instance.IsTimeEnded)
            {
                return;
            }

            if (MatchManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(MicroPhaseState)}: " +
                    $"{nameof(MatchManager)}が存在しないため、" +
                    "次の状態へ遷移できません。");

                return;
            }

            if (_gameData == null &&
                !TryLoadGameData())
            {
                return;
            }

            _hasRequestedTransition = true;

            if (HasReachedMaximumTurn())
            {
                MatchManager.Instance
                    .TransitionToResultScene();

                return;
            }

            MatchManager.Instance.ChangeState(
                new MacroPhaseState());
        }

        /// <summary>
        /// Micro Phase終了時のユニット座標を取得し、
        /// ゲームプレイ用ユニットの座標を同期します。
        /// </summary>
        public override void Exit()
        {
            _cameraTransition?.Restore();
            _cameraTransition = null;

            if (!_hasStartedCombat)
            {
                UnitManager.Instance?.LocalMicroUnitBehavior?.SwitchCamera(false);

                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                Debug.LogError(
                    $"{nameof(MicroPhaseState)}: " +
                    $"{nameof(UnitManager)}が存在しないため、" +
                    "ユニットの座標を同期できません。");

                return;
            }

            unitManager.FinalizeMicroMovement();
            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.IsSoloPlay)
            {
                foreach (UnitBehavior unitBehavior in
                    unitManager.GetAllUnitBehaviors())
                {
                    TrySynchronizeUnitCoordinate(
                        unitBehavior);
                }

                return;
            }

            UnitBehavior localUnitBehavior =
                unitManager.LocalUnit;

            if (localUnitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(MicroPhaseState)}: " +
                    "座標同期に必要なユニットが生成されていません。");

                return;
            }

            TrySynchronizeUnitCoordinate(
                localUnitBehavior);
        }

        /// <summary>
        /// 生存しているユニットのMicro座標を
        /// Macro、Microおよび戦術マップへ同期します。
        /// </summary>
        /// <param name="unitBehavior">
        /// 座標を同期するユニットです。
        /// </param>
        /// <returns>
        /// 座標同期を要求できた場合は<c>true</c>です。
        /// </returns>
        /// <remarks>
        /// 撃破時のBailOutではMacroUnitが先に破棄されるため、
        /// Unityの破棄済みObjectを明示的なnull比較で判定します。
        /// </remarks>
        private static bool TrySynchronizeUnitCoordinate(
            UnitBehavior unitBehavior)
        {
            if (unitBehavior == null)
            {
                return false;
            }

            MicroUnitBehavior microUnitBehavior =
                unitBehavior.GetMicroUnitBehavior();

            if (microUnitBehavior == null ||
                microUnitBehavior.IsDead)
            {
                return false;
            }

            MacroUnitBehavior macroUnitBehavior =
                unitBehavior.GetMacroUnitBehavior();

            if (macroUnitBehavior == null)
            {
                Debug.LogWarning(
                    $"{nameof(MicroPhaseState)}: " +
                    "MacroUnitBehaviorが存在しないため、" +
                    "Micro終了時の座標同期をスキップします。" +
                    $" PlayerId={unitBehavior.PlayerId}",
                    unitBehavior);

                return false;
            }

            Vector2Int coordinate =
                microUnitBehavior.GetCurrentCoordinate();

            unitBehavior.ApplyTeleportMoveToCoordinate(
                coordinate.x,
                coordinate.y);

            UnitStatus unitStatus =
                unitBehavior.GetUnitStatus();

            if (unitStatus != null)
            {
                unitStatus.EndMigrationExecution(
                    true);
            }

            return true;
        }

        /// <summary>
        /// Micro Phaseの開始に必要なManagerが
        /// 存在するかどうかを確認します。
        /// </summary>
        /// <returns>
        /// 必要なManagerがすべて存在する場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool TryGetRequiredManagers()
        {
            if (MatchPhaseManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(MicroPhaseState)}: " +
                    $"{nameof(MatchPhaseManager)}が存在しません。");

                return false;
            }

            if (HUDManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(MicroPhaseState)}: " +
                    $"{nameof(HUDManager)}が存在しません。");

                return false;
            }

            return true;
        }

        /// <summary>
        /// GameDataRepositoryからゲーム設定を取得します。
        /// </summary>
        /// <returns>
        /// ゲーム設定を取得できた場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool TryLoadGameData()
        {
            if (_gameData != null)
            {
                return true;
            }

            if (GameDataRepository.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(MicroPhaseState)}: " +
                    $"{nameof(GameDataRepository)}が存在しません。");

                return false;
            }

            _gameData =
                GameDataRepository.Instance.GetGameData();

            if (_gameData != null)
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(MicroPhaseState)}: " +
                $"{nameof(GameData)}を取得できませんでした。");

            return false;
        }

        /// <summary>
        /// 現在のターンが最大ターン数に到達しているか確認します。
        /// </summary>
        /// <returns>
        /// 最大ターン数に到達または超過している場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool HasReachedMaximumTurn()
        {
#if UNITY_EDITOR || PV_CAPTURE
            if (MatchManager.Instance.CaptureScenario != null &&
                !MatchManager.Instance.CaptureScenario.AllowMatchEnd)
            {
                return false;
            }

#endif
            return MatchManager.Instance.CurrentTurn >=
                _gameData.MaximumTurnCount;
        }
    }
}
