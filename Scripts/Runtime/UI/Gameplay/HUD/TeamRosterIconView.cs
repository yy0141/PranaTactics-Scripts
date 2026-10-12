using PranaTactics.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 一人分の選択画像、陣営枠、HPによる透過と死亡マークを表示します。
    /// </summary>
    [RequireComponent(typeof(MacroHudVisibility))]
    public sealed class TeamRosterIconView : MonoBehaviour
    {
        [SerializeField]
        private Image _portrait;

        [SerializeField]
        private Image _frame;

        [SerializeField]
        private GameObject _deathMark;

        [SerializeField]
        private CanvasGroup _missingPortrait;

        [SerializeField, Min(0.0f)]
        private float _opacityChangeSpeed = 2.0f;

        private UnitStatus _status;

        public int PlayerId => _status.PlayerId;

        public int TeamId => _status.TeamId;

        public bool IsDead => _status.IsDead || _status.CurrentHealth <= 0.0f;

        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>
        /// 既存プレハブにもマクロ時の透過表示を適用します。
        /// </summary>
        private void Awake()
        {
            if (!TryGetComponent<MacroHudVisibility>(out _))
            {
                gameObject.AddComponent<MacroHudVisibility>();
            }
        }

        /// <summary>
        /// ユニットのステータスを保持し、キャラ選択画面と同じ画像を設定します。
        /// </summary>
        /// <remarks>
        /// 表示モデル破棄後も死亡アイコンを残せるよう、MonoBehaviourではなくステータスを保持します。
        /// </remarks>
        public void Bind(UnitStatus status)
        {
            if (_status == status)
            {
                return;
            }

            _status = status;
            UnitParams character = status.CharacterData;

            _portrait.sprite = character != null ? character.GameplaySprite : null;
            _portrait.enabled = _portrait.sprite != null;
            _portrait.preserveAspect = true;
            _missingPortrait.gameObject.SetActive(_portrait.sprite == null);
        }

        /// <summary>
        /// 枠色と死亡マークを更新し、生存する味方のHP変化は滑らかに反映します。
        /// </summary>
        public void Refresh(bool isAlly, Color frameColor, bool immediately = false)
        {
            _frame.color = frameColor;
            bool isDead = IsDead;

            _deathMark.SetActive(isDead);
            float targetAlpha = TeamRosterAppearance.CalculateOpacity(isAlly, isDead, _status.HealthRate);
            Color portraitColor = Color.white;

            portraitColor.a = immediately || isDead || !isAlly || _opacityChangeSpeed <= 0.0f
                ? targetAlpha
                : Mathf.MoveTowards(
                    _portrait.color.a,
                    targetAlpha,
                    _opacityChangeSpeed * Time.unscaledDeltaTime);
            _portrait.color = portraitColor;
            _missingPortrait.alpha = portraitColor.a;
        }
    }
}
