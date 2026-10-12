using UnityEngine;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 全方向からの弾を防御する球状シールドとして、標準シールドより長い寿命を持ち、
    /// サブ側から生成された場合に大型化する防御プラーナです。
    /// </summary>
    public sealed class Scutum : ShieldPranaBase
    {
        /// <summary>
        /// サブ側から生成されたシールドへ適用する
        /// スケール倍率です。
        /// </summary>
        private const float k_SubScaleMultiplier = 1.3f;

        /// <summary>
        /// このシールドが自動消滅するまでの時間です。
        /// </summary>
        [Header("Scutum")]
        [Min(0.0f)]
        [SerializeField]
        private float _shieldLifeTime = 2.0f;

        /// <summary>
        /// スケール倍率を適用する前のローカルスケールです。
        /// </summary>
        private Vector3 _baseLocalScale;

        /// <summary>
        /// 基準スケールを取得済みかどうかを示します。
        /// </summary>
        private bool _hasCachedBaseScale;

        public override bool BlocksFromAllDirections => true;

        /// <summary>
        /// シールドの基本初期化完了後に、
        /// 寿命と表示サイズを設定します。
        /// </summary>
        protected override void OnShieldInitialized()
        {
            base.OnShieldInitialized();

            CacheBaseLocalScale();

            SetLifeTime(_shieldLifeTime);

            float scaleMultiplier =
                IsMain
                    ? 1.0f
                    : k_SubScaleMultiplier;

            transform.localScale =
                _baseLocalScale *
                scaleMultiplier;
        }

        /// <summary>
        /// シールドへ倍率を適用する前の
        /// ローカルスケールを保存します。
        /// </summary>
        private void CacheBaseLocalScale()
        {
            if (_hasCachedBaseScale)
            {
                return;
            }

            _baseLocalScale =
                transform.localScale;

            _hasCachedBaseScale = true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorから設定された値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _shieldLifeTime =
            Mathf.Max(
            0.0f,
            _shieldLifeTime);
        }
#endif
    }
}
