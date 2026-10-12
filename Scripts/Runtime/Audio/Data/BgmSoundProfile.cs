using System;
using UnityEngine;

using PranaTactics.Match.Results;

namespace PranaTactics.Audio
{
    /// <summary>
    /// ゲーム全体のシーンと勝敗に対応するBGMキューを保持します。
    /// </summary>
    [CreateAssetMenu(menuName = "Prana Tactics/Audio/BGM Sound Profile")]
    public sealed class BgmSoundProfile : ScriptableObject
    {
        /// <summary>
        /// シーンごとのキューです。未設定の登録シーンではBGMを停止します。
        /// </summary>
        [SerializeField]
        private SceneBgmEntry[] _scenes = Array.Empty<SceneBgmEntry>();

        /// <summary>
        /// 勝利時のキューです。
        /// </summary>
        [SerializeField]
        private SoundCue _victoryCue;

        /// <summary>
        /// 引き分け時のキューです。
        /// </summary>
        [SerializeField]
        private SoundCue _drawCue;

        /// <summary>
        /// 敗北時のキューです。
        /// </summary>
        [SerializeField]
        private SoundCue _defeatCue;

        /// <summary>
        /// シーンの登録を検索します。未登録の追加UIシーンでは再生を維持します。
        /// </summary>
        /// <returns>
        /// シーンが登録されている場合は<c>true</c>です。
        /// </returns>
        public bool TryGetSceneCue(string sceneName, out SoundCue cue)
        {
            cue = null;
            if (_scenes == null)
            {
                return false;
            }

            foreach (SceneBgmEntry entry in _scenes)
            {
                if (entry != null && entry.SceneName == sceneName)
                {
                    cue = entry.Cue;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 表示される勝敗に対応するキューを取得します。
        /// </summary>
        public SoundCue GetResultCue(MatchOutcome outcome)
        {
            return outcome switch
            {
                MatchOutcome.Victory => _victoryCue,
                MatchOutcome.Draw => _drawCue,
                MatchOutcome.Defeat => _defeatCue,
                _ => null
            };
        }

        /// <summary>
        /// シーン名とBGMキューの対応を保持します。
        /// </summary>
        [Serializable]
        private sealed class SceneBgmEntry
        {
            /// <summary>
            /// 対象シーン名です。
            /// </summary>
            [SerializeField]
            private string _sceneName;

            /// <summary>
            /// シーン読み込み時に適用するキューです。
            /// </summary>
            [SerializeField]
            private SoundCue _cue;

            public string SceneName => _sceneName;
            public SoundCue Cue => _cue;
        }
    }
}
