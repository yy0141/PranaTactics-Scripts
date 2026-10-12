using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Combat;
using PranaTactics.Pranam;

namespace PranaTactics.Prana
{
    /// <summary>
    /// シールドとして生成されるプラーナの
    /// 耐久値、寿命および破棄処理を管理する基底クラスです。
    /// </summary>
    public abstract class ShieldPranaBase :
        PranaBase,
        IDamageable
    {
        /// <summary>
        /// 弾の方向を有効と判断する最小二乗長です。
        /// </summary>
        private const float k_MinimumDirectionSqrMagnitude = 0.0001f;

        /// <summary>
        /// シールドが自動的に消滅するまでの時間です。
        /// </summary>
        [Header("Shield Lifetime")]
        [Min(0.0f)]
        [SerializeField]
        private float _lifeTime = 1.5f;

        /// <summary>
        /// シールドの最大防御力です。
        /// </summary>
        private float _maximumDefense;

        /// <summary>
        /// シールドの現在の防御力です。
        /// </summary>
        private float _currentDefense;

        /// <summary>
        /// シールドが初期化されてからの経過時間です。
        /// </summary>
        private float _elapsedTime;

        /// <summary>
        /// シールドが既に破棄処理へ入っているかどうかを示します。
        /// </summary>
        private bool _isDestroying;

        public float SpawnTime
        {
            get;
            private set;
        }

        public int PlayerId
        {
            get
            {
                return OwnerPlayerId;
            }
        }

        protected float MaximumDefense
        {
            get
            {
                return _maximumDefense;
            }
        }

        protected float CurrentDefense
        {
            get
            {
                return _currentDefense;
            }
        }

        protected float ElapsedTime
        {
            get
            {
                return _elapsedTime;
            }
        }

        protected bool IsDestroying
        {
            get
            {
                return _isDestroying;
            }
        }

        public virtual bool BlocksFromAllDirections => false;

        /// <summary>
        /// シールドの形状と向きから弾を防御できるか判定します。
        /// </summary>
        /// <param name="projectileDirection">
        /// 弾の進行方向です。
        /// </param>
        /// <param name="faceDotThreshold">
        /// 板状シールドが防御可能とする内積の上限です。
        /// </param>
        /// <returns>
        /// 有効な耐久値と寿命を持ち、防御可能な方向からの弾である場合は<c>true</c>です。
        /// </returns>
        public bool CanBlockProjectile(Vector3 projectileDirection, float faceDotThreshold)
        {
            if (!IsInitialized || _isDestroying || _currentDefense <= 0.0f ||
                projectileDirection.sqrMagnitude < k_MinimumDirectionSqrMagnitude)
            {
                return false;
            }

            if (BlocksFromAllDirections)
            {
                return true;
            }

            Vector3 shieldForward = transform.forward;

            return shieldForward.sqrMagnitude >= k_MinimumDirectionSqrMagnitude &&
                Vector3.Dot(projectileDirection.normalized, shieldForward.normalized) <= faceDotThreshold;
        }

        /// <summary>
        /// 派生シールドの寿命設定を自動消滅判定へ反映します。
        /// </summary>
        protected void SetLifeTime(float lifeTime)
        {
            _lifeTime = Mathf.Max(0.0f, lifeTime);
        }

        /// <summary>
        /// シールドへダメージを適用します。
        /// </summary>
        /// <param name="attackerPlayerId">
        /// ダメージを与えたプレイヤーのIDです。
        /// </param>
        /// <param name="value">
        /// 適用するダメージ量です。
        /// </param>
        /// <param name="animation">
        /// 命中演出を実行する場合は<c>true</c>です。
        /// 現在は命中効果音の再生有無として使用します。
        /// </param>
        public void ApplyDamage(
            int attackerPlayerId,
            float value,
            bool animation = true)
        {
            ApplyDamageInternal(attackerPlayerId, value, animation, CombatFeedbackSide.Unknown);
        }

        /// <summary>
        /// 攻撃の発動元を保持してシールドの耐久値を減らします。
        /// </summary>
        public void ApplyCombatDamage(int attackerPlayerId, float value, bool isMain)
        {
            ApplyDamageInternal(
                attackerPlayerId,
                value,
                true,
                isMain ? CombatFeedbackSide.Main : CombatFeedbackSide.Sub);
        }

        /// <summary>
        /// 有効な防御ダメージを適用し、吸収結果を通知します。
        /// </summary>
        private void ApplyDamageInternal(
            int attackerPlayerId,
            float value,
            bool animation,
            CombatFeedbackSide side)
        {
            if (!IsInitialized ||
                _isDestroying || _currentDefense <= 0.0f ||
                float.IsNaN(value) || float.IsInfinity(value))
            {
                return;
            }

            if (attackerPlayerId ==
                OwnerPlayerId)
            {
                return;
            }

            float damage =
                Mathf.Max(
                    0.0f,
                    value);

            if (damage <= 0.0f)
            {
                return;
            }

            _currentDefense =
                Mathf.Max(
                    0.0f,
                    _currentDefense -
                    damage);

            if (animation)
            {
                CombatFeedbackEvents.Publish(new CombatFeedbackResult(
                    attackerPlayerId,
                    OwnerPlayerId,
                    CombatFeedbackKind.DefenseHit,
                    side));
                RequestPranaSound(
                    PranamSoundEvent.Hit,
                    transform.position);
            }

            if (_currentDefense <= 0.0f)
            {
                DestroyShield(
                    true);
            }
        }

        /// <summary>
        /// シールドの現在の耐久値を取得します。
        /// </summary>
        /// <returns>
        /// 現在の防御力です。
        /// </returns>
        public override float GetPranaHealth()
        {
            return Mathf.Max(
                0.0f,
                _currentDefense);
        }

        /// <summary>
        /// <see cref="PranaBase"/>の初期化後に、
        /// シールド固有の状態を初期化します。
        /// </summary>
        protected override void OnInitialized()
        {
            base.OnInitialized();

            _maximumDefense =
                Mathf.Max(
                    0.0f,
                    Context.Defense);

            _currentDefense =
                _maximumDefense;

            _elapsedTime = 0.0f;
            _isDestroying = false;

            SpawnTime =
                Time.time;

            OnShieldInitialized();
        }

        /// <summary>
        /// シールドの基本初期化が完了した後に呼び出されます。
        /// </summary>
        /// <remarks>
        /// 派生クラスで表示、形状、位置などを初期化する場合に
        /// オーバーライドします。
        /// </remarks>
        protected virtual void OnShieldInitialized()
        {
        }

        /// <summary>
        /// シールドの耐久値と寿命を更新します。
        /// </summary>
        protected virtual void Update()
        {
            if (!IsInitialized ||
                _isDestroying)
            {
                return;
            }

            _elapsedTime +=
                Time.deltaTime;

            if (_currentDefense <= 0.0f)
            {
                DestroyShield(
                    true);

                return;
            }

            if (_elapsedTime >=
                _lifeTime)
            {
                DestroyShield(
                    false);
            }
        }

        /// <summary>
        /// シールドの現在の防御力を指定された値へ変更します。
        /// </summary>
        /// <param name="defense">
        /// 設定する防御力です。
        /// </param>
        /// <remarks>
        /// 派生クラスが初期防御力を補正する場合に使用します。
        /// </remarks>
        protected void SetCurrentDefense(
            float defense)
        {
            _currentDefense =
                Mathf.Clamp(
                    defense,
                    0.0f,
                    _maximumDefense);
        }

        /// <summary>
        /// シールドの最大防御力と現在の防御力を変更します。
        /// </summary>
        /// <param name="maximumDefense">
        /// 新しく設定する最大防御力です。
        /// </param>
        protected void SetMaximumDefense(
            float maximumDefense)
        {
            _maximumDefense =
                Mathf.Max(
                    0.0f,
                    maximumDefense);

            _currentDefense =
                Mathf.Min(
                    _currentDefense,
                    _maximumDefense);
        }

        /// <summary>
        /// シールドの破棄処理を要求します。
        /// </summary>
        /// <param name="playDestroySound">
        /// シールド破壊音を再生する場合は<c>true</c>です。
        /// 寿命による消滅では<c>false</c>を指定します。
        /// </param>
        protected void DestroyShield(
            bool playDestroySound)
        {
            if (_isDestroying)
            {
                return;
            }

            _isDestroying = true;

            if (playDestroySound)
            {
                RequestPranaSound(
                    PranamSoundEvent.Destroy,
                    transform.position);
            }

            if (PranamBehavior != null &&
                Context != null)
            {
                PranamBehavior.DestroyPrana(
                    Context.Id);

                return;
            }

            Destroy(
                gameObject);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorから設定された値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _lifeTime =
            Mathf.Max(
            0.0f,
            _lifeTime);
        }
#endif
    }
}
