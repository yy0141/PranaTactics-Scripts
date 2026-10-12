using System;
using UnityEngine.SceneManagement;

using PranaTactics.Match.Results;

namespace PranaTactics.Audio
{
    public sealed partial class SoundManager
    {
        /// <summary>
        /// 現在のBGM割り当てを決めたシーン名です。
        /// </summary>
        private string _bgmSceneName;

        /// <summary>
        /// 現在表示中の勝敗です。
        /// </summary>
        private MatchOutcome? _bgmOutcome;

        /// <summary>
        /// 現在再生しているキューです。
        /// </summary>
        private SoundCue _currentBgmCue;

        /// <summary>
        /// シーン通知を購読し、起動シーンのBGMを適用します。
        /// </summary>
        private void InitializeSceneBgm()
        {
            SceneManager.sceneLoaded += OnBgmSceneLoaded;
            OnBgmSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        /// <summary>
        /// シーン通知の購読を解除します。
        /// </summary>
        private void ShutdownSceneBgm()
        {
            SceneManager.sceneLoaded -= OnBgmSceneLoaded;
        }

        /// <summary>
        /// 登録シーンの読み込み時に対応するキューを適用します。
        /// </summary>
        private void OnBgmSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_bgmProfile == null || !_bgmProfile.TryGetSceneCue(scene.name, out SoundCue cue))
            {
                return;
            }

            _bgmSceneName = scene.name;
            _bgmOutcome = null;
            ApplyBgmCue(cue);
        }

        /// <summary>
        /// ゲーム全体のBGMプロファイルを差し替え、現在の画面へ即時適用します。
        /// </summary>
        public void SetBgmProfile(BgmSoundProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            _bgmProfile = profile;
            if (_bgmOutcome.HasValue)
            {
                PlayResultBgm(_bgmOutcome.Value);
            }
            else
            {
                _bgmProfile.TryGetSceneCue(_bgmSceneName, out SoundCue cue);
                ApplyBgmCue(cue);
            }
        }

        /// <summary>
        /// リザルト表示と同じ勝敗に対応するBGMを適用します。
        /// </summary>
        public void PlayResultBgm(MatchOutcome outcome)
        {
            _bgmOutcome = outcome;
            ApplyBgmCue(_bgmProfile != null ? _bgmProfile.GetResultCue(outcome) : null);
        }

        /// <summary>
        /// キュー未設定時は停止し、設定済みの場合はループ再生します。
        /// </summary>
        private void ApplyBgmCue(SoundCue cue)
        {
            if (cue == null)
            {
                StopBGM();
                return;
            }

            PlayBGM(cue);
        }
    }
}
