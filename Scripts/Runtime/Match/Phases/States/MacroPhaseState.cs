using UnityEngine;

using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;
using PranaTactics.Networking;
using PranaTactics.AI.Solo;
using PranaTactics.Board;

namespace PranaTactics.Match.Phases
{
    /// <summary>
    /// プレイヤーがヘックスマップ上で移動経路を計画する
    /// Macro Phaseの進行を管理します。
    /// </summary>
    public sealed class MacroPhaseState : MatchPhaseState
    {
        /// <summary>
        /// Macro Phaseの制限時間などを保持するゲーム設定です。
        /// </summary>
        private GameData _gameData;

        /// <summary>
        /// Micro Phaseへの遷移を要求済みかどうかを示します。
        /// </summary>
        private bool _hasRequestedMicroPhase;

        private MicroPhaseCameraTransition _cameraTransition;

        private MicroUnitBehavior _transitionMicroUnit;

        private float _transitionElapsed;

        private float _transitionDuration;

        private bool _hasStartedPlanning;

        /// <summary>
        /// Macro Phaseを開始し、ターン数とタイマー表示を更新します。
        /// </summary>
        public override void Enter()
        {
            _hasRequestedMicroPhase = false;
            _hasStartedPlanning = false;
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

            _transitionDuration = phaseManager.CurrentPhase == MatchPhase.Micro
                ? phaseManager.MacroTransitionDuration : 0.0f;

            if (_transitionDuration > 0.0f)
            {
                HUDManager.Instance.StopTimer();
                phaseManager.ChangePhase(MatchPhase.MacroTransition);
                UnitManager unitManager = UnitManager.Instance;

                _transitionMicroUnit = unitManager != null ? unitManager.LocalMicroUnitBehavior : null;
                GameObject fullMapCamera = BoardManager.Instance != null
                    ? BoardManager.Instance.GetMinimapCameraObject() : null;

                _cameraTransition = new MicroPhaseCameraTransition(
                    unitManager != null ? unitManager.LocalMacroUnitBehavior : null,
                    _transitionMicroUnit,
                    fullMapCamera != null ? fullMapCamera.transform : null,
                    true);
                HUDManager.Instance.SetMapMode(false);

                return;
            }

            StartPlanning();
        }

        /// <summary>
        /// 上昇演出の終了後にMacroカメラへ切り替え、ターン・経路計画・タイマーを開始します。
        /// </summary>
        private void StartPlanning()
        {
            RestoreTransitionCamera();
            _hasStartedPlanning = true;

            MatchPhaseManager.Instance.ChangePhase(
                MatchPhase.Macro);

            UnitManager.Instance?.LocalMacroUnitBehavior?.SwitchCamera(true);

            HUDManager.Instance.EnablePanel(false);

            MatchManager.Instance.IncrementTurn();
            MatchPhaseManager.Instance.MacroPlanning = new MacroPlanningSession(
                GameFlowManager.Instance != null && GameFlowManager.Instance.IsSoloPlay
                    ? null : NetworkServiceLocator.Online,
                MatchManager.Instance.CurrentTurn);

            HUDManager.Instance.SetTimer(
                _gameData.MacroPhaseDuration);
        }

        /// <summary>
        /// 完了・中断時に演出で使用したFPSカメラを元の姿勢へ戻して非表示にします。
        /// </summary>
        private void RestoreTransitionCamera()
        {
            _cameraTransition?.Restore();
            _cameraTransition = null;

            if (_transitionMicroUnit != null)
            {
                _transitionMicroUnit.SwitchCamera(false);
                _transitionMicroUnit = null;
            }
        }

        /// <summary>
        /// Macro Phaseの制限時間を監視し、
        /// 終了した場合はMicro Phaseへの遷移を要求します。
        /// </summary>
        public override void UpdateState()
        {
            if (_hasRequestedMicroPhase)
            {
                return;
            }

            if (!_hasStartedPlanning)
            {
                if (_gameData == null)
                {
                    return;
                }

                _transitionElapsed += Time.deltaTime;
                _cameraTransition?.Apply(_transitionElapsed / _transitionDuration);

                if (_transitionElapsed >= _transitionDuration)
                {
                    StartPlanning();
                }

                return;
            }

            if (GameFlowManager.Instance == null)
            {
                return;
            }

            MacroPlanningSession planning = MatchPhaseManager.Instance?.MacroPlanning;

            planning?.RefreshConfirmation();

            if (planning == null || !planning.CanAdvance ||
                (!GameFlowManager.Instance.IsTimeEnded &&
                 !planning.AreAllConfirmed(SoloAiDirector.IsMacroPlanningComplete, IsPlayerInactive)))
            {
                return;
            }

            if (MatchManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(MacroPhaseState)}: " +
                    $"{nameof(MatchManager)}が存在しないため、" +
                    $"{nameof(MicroPhaseState)}へ遷移できません。");

                return;
            }

            _hasRequestedMicroPhase = true;

            MatchManager.Instance.ChangeState(
                new MicroPhaseState());
        }

        private static bool IsPlayerInactive(int playerId)
        {
            return UnitManager.Instance != null &&
                UnitManager.Instance.TryGetUnit(playerId, out UnitBehavior unit) &&
                unit.GetUnitStatus() != null && unit.GetUnitStatus().IsDead;
        }

        /// <summary>
        /// Macro Phaseで確定したMicro Unitの座標へ、
        /// ゲームプレイ用ユニットを移動させます。
        /// </summary>
        public override void Exit()
        {
            RestoreTransitionCamera();

            if (!_hasStartedPlanning)
            {
                return;
            }

            if (MatchPhaseManager.Instance != null)
            {
                MatchPhaseManager.Instance.MacroPlanning = null;
            }

            if (UnitManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(MacroPhaseState)}: " +
                    $"{nameof(UnitManager)}が存在しないため、" +
                    "ユニットを移動できません。");

                return;
            }

            var microUnitBehavior =
                UnitManager.Instance.LocalMicroUnitBehavior;

            var unitBehavior =
                UnitManager.Instance.LocalUnit;

            if (microUnitBehavior == null ||
                unitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(MacroPhaseState)}: " +
                    "移動に必要なユニットが生成されていません。");

                return;
            }

            Vector2Int currentCoordinate =
                microUnitBehavior.GetCurrentCoordinate();

            unitBehavior.ApplyTeleportMoveToCoordinate(
                currentCoordinate.x,
                currentCoordinate.y);
        }

        /// <summary>
        /// Macro Phaseの開始に必要なManagerが
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
                    $"{nameof(MacroPhaseState)}: " +
                    $"{nameof(MatchPhaseManager)}が存在しません。");

                return false;
            }

            if (HUDManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(MacroPhaseState)}: " +
                    $"{nameof(HUDManager)}が存在しません。");

                return false;
            }

            if (MatchManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(MacroPhaseState)}: " +
                    $"{nameof(MatchManager)}が存在しません。");

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
                    $"{nameof(MacroPhaseState)}: " +
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
                $"{nameof(MacroPhaseState)}: " +
                $"{nameof(GameData)}を取得できませんでした。");

            return false;
        }
    }
}
