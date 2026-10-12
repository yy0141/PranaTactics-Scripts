using UnityEngine;

using PranaTactics.Settings;
using PranaTactics.Tutorial;
using PranaTactics.Unit;

namespace PranaTactics.GameFlow.States
{
    /// <summary>
    /// 専用シーンを準備し、訓練内の進行をTutorialSessionへ委譲します。
    /// </summary>
    public sealed class TutorialState : LocalSimulationState
    {
        protected override string SceneName => "TutorialScene";

        private TutorialSession _session;

        private TutorialView _loading;

        private float _startedAt;

        private bool _isLeaving;

        private bool _isInitializationFailed;

        private bool _isLoadingMenuOpen;

        private ITutorialInput _input;

        public override void Enter()
        {
            _input = new TutorialPlayerInput(TutorialUiResources.Current.InputBindings);
            _startedAt = Time.realtimeSinceStartup;
            _loading = TutorialView.Create(GameFlowManager.Instance.transform, TutorialUiResources.Font);
            _loading.Leave += () => Finish(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            base.Enter();
        }

        protected override void OnSimulationReady(UnitBehavior player, UnitBehavior target)
        {
            _session = TutorialSession.Instance;

            if (_session == null)
            {
                _loading.Loading("訓練シーンにTutorialSessionがありません。ロビーに戻ってください。");

                return;
            }

            _session.Initialize(player, target, Finish);
            _loading.gameObject.SetActive(false);
            Object.Destroy(_loading.gameObject);
            _loading = null;
        }

        protected override void OnSimulationFailed(System.Exception exception)
        {
            _isInitializationFailed = true;

            if (_loading != null)
            {
                _loading.Loading("訓練場を準備できませんでした。ロビーに戻って再実行してください。");
            }
        }

        public override void UpdateState()
        {
            if (_loading != null && _input.Read().MenuPressed)
            {
                _isLoadingMenuOpen = !_isLoadingMenuOpen;
                _loading.SetMenu(_isLoadingMenuOpen);
            }

            if (!_isInitializationFailed && _loading != null && Time.realtimeSinceStartup - _startedAt > 45)
            {
                _loading.Loading("訓練場の準備に時間がかかっています。終了してロビーから再実行できます。");
            }
        }

        private void Finish(bool skipped)
        {
            if (_isLeaving)
            {
                return;
            }

            _isLeaving = true;
            SettingsManager.Current.HasFinishedTutorial = true;
            SettingsManager.Save();
            Debug.Log(skipped ? "Tutorial skipped by player" : "Tutorial completed");
            GameFlowManager.Instance.ChangeState(new LobbyState());
        }

        public override void Exit()
        {
            _session?.Stop();

            if (_loading != null)
            {
                _loading.gameObject.SetActive(false);
                Object.Destroy(_loading.gameObject);
            }

            base.Exit();
        }
    }
}
