using UnityEngine;

using PranaTactics.UI.Gameplay;

namespace PranaTactics.GameFlow
{
    /// <summary>
    /// ゲームの各フェイズの制限時間と、
    /// 1試合における最大ターン数と移動経路の表示データを保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "GameData",
    menuName = "Prana Tactics/Game Flow/Game Data",
    order = 1)]

    public sealed class GameData : ScriptableObject
    {
        /// <summary>
        /// マクロ・フェイズの制限時間です。
        /// </summary>
        [Header("Phase Duration")]
        [Min(0)]
        [SerializeField]
        private int _macroPhaseDuration;

        /// <summary>
        /// ミクロ・フェイズの制限時間です。
        /// </summary>
        [Min(0)]
        [SerializeField]
        private int _microPhaseDuration;

        /// <summary>
        /// 1試合における最大ターン数です。
        /// </summary>
        [Header("Match")]
        [Min(1)]
        [SerializeField]
        private int _maximumTurnCount = 1;

        /// <summary>
        /// マップ上の移動経路に使用する共有表示データです。
        /// </summary>
        [SerializeField]
        private MovementPathData _movementPathData;

        public MovementPathData MovementPathData => _movementPathData;

        public int MacroPhaseDuration
        {
            get
            {
                return _macroPhaseDuration;
            }
        }

        public int MicroPhaseDuration
        {
            get
            {
                return _microPhaseDuration;
            }
        }

        public int MaximumTurnCount
        {
            get
            {
                return _maximumTurnCount;
            }
        }

#if UNITY_EDITOR

        /// <summary>
        /// Inspector上で変更された値を有効な範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _macroPhaseDuration =
                Mathf.Max(
                0,
                _macroPhaseDuration);

            _microPhaseDuration =
                Mathf.Max(
                    0,
                    _microPhaseDuration);

            _maximumTurnCount =
                Mathf.Max(
                    1,
                    _maximumTurnCount);
        }

#endif
    }
}
