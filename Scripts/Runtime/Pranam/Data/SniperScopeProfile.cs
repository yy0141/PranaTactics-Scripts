using UnityEngine;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// スナイパーの照準表示に使用する
    /// UIおよびスコープ設定を保持します。
    /// </summary>
    [CreateAssetMenu(
        fileName = "SniperScopeProfile",
        menuName = "Prana Tactics/Pranam/Sniper Scope Profile",
        order = 5)]
    public sealed class SniperScopeProfile : ScriptableObject
    {
        /// <summary>
        /// 照準表示方式です。
        /// </summary>
        [Header("Scope Type")]
        [SerializeField]
        private SniperScopeType _scopeType;

        /// <summary>
        /// 照準時に表示するレティクル画像です。
        /// </summary>
        [Header("Reticle")]
        [SerializeField]
        private Sprite _reticleSprite;

        /// <summary>
        /// レティクルの表示サイズです。
        /// </summary>
        [SerializeField]
        private Vector2 _reticleSize =
            new Vector2(
                256.0f,
                256.0f);

        /// <summary>
        /// スコープ映像を切り抜くための
        /// マスク画像です。
        /// </summary>
        [Header("Scope")]
        [SerializeField]
        private Sprite _scopeMaskSprite;

        /// <summary>
        /// スコープ外周に表示する画像です。
        /// </summary>
        [SerializeField]
        private Sprite _scopeOverlaySprite;

        /// <summary>
        /// スコープ外周画像へ適用する色です。
        /// </summary>
        /// <remarks>
        /// RGBAすべてを設定できます。
        /// Alphaを小さくすると、
        /// スコープ外側のゲーム画面が見えやすくなります。
        /// </remarks>
        [SerializeField]
        private Color _scopeOverlayColor =
            new Color(
                0.0f,
                0.0f,
                0.0f,
                0.8f);

        /// <summary>
        /// スコープ外側のぼかし強度です。
        /// </summary>
        /// <remarks>
        /// 0の場合はぼかしなし、
        /// 1の場合は最大のぼかしを適用します。
        /// </remarks>
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _outsideBlurStrength;

        /// <summary>
        /// スコープ内部の表示サイズです。
        /// </summary>
        [SerializeField]
        private Vector2 _scopeSize =
            new Vector2(
                800.0f,
                800.0f);

        public SniperScopeType ScopeType
        {
            get
            {
                return _scopeType;
            }
        }

        public Sprite ReticleSprite
        {
            get
            {
                return _reticleSprite;
            }
        }

        public Vector2 ReticleSize
        {
            get
            {
                return _reticleSize;
            }
        }

        public Sprite ScopeMaskSprite
        {
            get
            {
                return _scopeMaskSprite;
            }
        }

        public Sprite ScopeOverlaySprite
        {
            get
            {
                return _scopeOverlaySprite;
            }
        }

        public Color ScopeOverlayColor
        {
            get
            {
                return _scopeOverlayColor;
            }
        }

        public float OutsideBlurStrength
        {
            get
            {
                return _outsideBlurStrength;
            }
        }

        public Vector2 ScopeSize
        {
            get
            {
                return _scopeSize;
            }
        }
    }
}
