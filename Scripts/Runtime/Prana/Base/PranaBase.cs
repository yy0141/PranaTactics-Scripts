using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Combat;
using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.Prana
{
    /// <summary>
    /// プラーナムによって生成される弾、近接攻撃および
    /// シールドなどのプラーナに共通する基底クラスです。
    /// </summary>
    /// <remarks>
    /// プラーナの所有者情報、生成コンテキストおよび
    /// 効果音要求に関する共通処理を提供します。
    ///
    /// ネットワーク通信は<see cref="PranamBehavior"/>と
    /// <see cref="Pranam.Networking.IPranamNetwork"/>が担当するため、
    /// このクラスはPhotonへ直接依存しません。
    /// </remarks>
    public abstract class PranaBase : MonoBehaviour
    {
        /// <summary>
        /// このPranaの固定性能を定義する兵装データです。
        /// </summary>
        [SerializeField]
        private PranamData _pranamData;

        /// <summary>
        /// 同じPrefabを共有する別兵装の固定設定です。
        /// </summary>
        [SerializeField]
        private PranamData[] _alternativePranamData = Array.Empty<PranamData>();

        protected PranamData Definition => _pranamData;

        /// <summary>
        /// Prefabの設定から発射時の性能を確定し、送信可能な生成情報を返します。
        /// </summary>
        /// <remarks>
        /// 受信側はこの処理を実行せず、送信側で確定した性能を使用します。
        /// </remarks>
        /// <param name="generation">
        /// 固定設定の識別子と発射ごとの所有者、対象および位置です。
        /// </param>
        /// <param name="attackDivisionCount">
        /// 射出弾の合計威力を分配する弾数です。
        /// </param>
        /// <param name="snapshot">
        /// 発射時の性能を確定した通信用の生成情報です。
        /// </param>
        /// <returns>
        /// 固定設定を取得して性能を確定できた場合は<c>true</c>です。
        /// </returns>
        internal bool TryCreateSpawnSnapshot(
            PranamContext generation,
            int attackDivisionCount,
            out PranamContext snapshot)
        {
            snapshot = null;
            if (generation == null || attackDivisionCount < 1)
            {
                return false;
            }

            PranamData data = _pranamData;
            if (data == null || data.name != generation.DefinitionId)
            {
                data = null;
                foreach (PranamData alternative in _alternativePranamData ?? Array.Empty<PranamData>())
                {
                    if (alternative != null && alternative.name == generation.DefinitionId)
                    {
                        data = alternative;
                        break;
                    }
                }
            }

            if (data == null)
            {
                Debug.LogError($"Pranaの固定設定が登録されていません。DefinitionId={generation.DefinitionId}", this);
                return false;
            }

            snapshot = PranaSpawnSettings.CreateSnapshot(data, generation, attackDivisionCount);
            return true;
        }

        /// <summary>
        /// 接触先に応じた兵装固有の効果音を要求します。
        /// </summary>
        /// <remarks>
        /// 敵Unitへの接触音は、確定したダメージ結果によるFeedbackの確認音と併せて再生します。
        /// シールド接触音はシールド側が担当します。
        /// </remarks>
        protected virtual void RequestHitSound(Collider hitCollider)
        {
            bool isUnit = hitCollider != null &&
                hitCollider.GetComponentInParent<PranaBase>() == null &&
                hitCollider.GetComponentInParent<UnitBehavior>() != null;

            bool isShield = hitCollider != null &&
                hitCollider.GetComponentInParent<ShieldPranaBase>() != null;
            if (isUnit)
            {
                if (!IsOwnerOrAllyCollider(hitCollider))
                {
                    RequestPranaSound(PranamSoundEvent.UnitHit, transform.position);
                }
            }
            else if (!isShield)
            {
                RequestPranaSound(PranamSoundEvent.Hit, transform.position);
            }
        }

        /// <summary>
        /// 有効なプレイヤーIDが存在しないことを表す値です。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// 有効なプラーナIDが存在しないことを表す値です。
        /// </summary>
        private const int k_InvalidPranaId = -1;

        /// <summary>
        /// 命中位置を内包するシールドを検索する球の半径です。
        /// </summary>
        private const float k_ContainingShieldSearchRadius = 0.001f;

        /// <summary>
        /// このプラーナの生成、命中、破棄および
        /// 効果音同期を管理するコンポーネントです。
        /// </summary>
        private PranamBehavior _pranamBehavior;

        /// <summary>
        /// 生成情報から複製した、このプラーナだけが所有する実行状態です。
        /// </summary>
        private PranamContext _runtimeState;

        /// <summary>
        /// このプラーナの初期化が完了しているかどうかを示します。
        /// </summary>
        private bool _isInitialized;

        protected PranamBehavior PranamBehavior
        {
            get
            {
                return _pranamBehavior;
            }
        }

        public int SourcePranamNumber => _runtimeState != null ? _runtimeState.PranamNumber : -1;

        protected PranamContext Context
        {
            get
            {
                return _runtimeState;
            }
        }

        public bool IsInitialized
        {
            get
            {
                return _isInitialized;
            }
        }

        public int OwnerPlayerId
        {
            get
            {
                return Context != null
                    ? Context.PlayerId
                    : k_InvalidPlayerId;
            }
        }

        public int PranaId
        {
            get
            {
                return Context != null
                    ? Context.Id
                    : k_InvalidPranaId;
            }
        }

        public bool IsMain
        {
            get
            {
                return Context != null &&
                    Context.IsMain;
            }
        }

        /// <summary>
        /// このプラーナを管理するコンポーネントと
        /// 生成コンテキストを設定します。
        /// </summary>
        /// <param name="pranamBehavior">
        /// プラーナの生成、命中、破棄および
        /// 効果音同期を管理するコンポーネントです。
        /// </param>
        /// <param name="context">
        /// プラーナの所有者、性能および対象情報です。
        /// </param>
        public virtual void Initialize(
            PranamBehavior pranamBehavior,
            PranamContext context)
        {
            _isInitialized = false;
            _pranamBehavior = null;
            _runtimeState = null;

            if (pranamBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(PranaBase)}: " +
                    $"{nameof(pranamBehavior)}が設定されていません。",
                    this);

                return;
            }

            if (context == null)
            {
                Debug.LogError(
                    $"{nameof(PranaBase)}: " +
                    $"{nameof(PranamContext)}が設定されていません。",
                    this);

                return;
            }

            _pranamBehavior =
                pranamBehavior;

            _runtimeState =
                context.CreateSnapshot();

            _isInitialized = true;

            OnInitialized();
        }

        /// <summary>
        /// 基底クラスによる初期化が完了した後に呼び出されます。
        /// </summary>
        /// <remarks>
        /// 派生クラス固有の初期位置、進行方向または
        /// パラメータを設定する場合にオーバーライドします。
        /// </remarks>
        protected virtual void OnInitialized()
        {
        }

        /// <summary>
        /// ボードなどが所有する永続プラーナを、ユニットに属する管理コンポーネントから独立して初期化します。
        /// </summary>
        protected void InitializeIndependentPrana(PranamContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            _isInitialized = false;
            _pranamBehavior = null;
            _runtimeState = context.CreateSnapshot();
            _isInitialized = true;
            OnInitialized();
        }

        /// <summary>
        /// このプラーナの現在の耐久値を取得します。
        /// </summary>
        /// <returns>
        /// デフォルトでは生成コンテキストの攻撃力を返します。
        /// シールドなど、個別の耐久値を持つクラスでは
        /// このメソッドをオーバーライドします。
        /// </returns>
        public virtual float GetPranaHealth()
        {
            if (Context == null)
            {
                return 0.0f;
            }

            return Mathf.Max(
                0.0f,
                Context.Attack);
        }

        /// <summary>
        /// 指定された効果音を現在のプラーナの向きで再生するよう
        /// プラーナム管理コンポーネントへ要求します。
        /// </summary>
        /// <param name="soundEvent">
        /// 再生するプラーナムサウンドイベントです。
        /// </param>
        /// <param name="position">
        /// 効果音を再生するワールド座標です。
        /// </param>
        protected void RequestPranaSound(
            PranamSoundEvent soundEvent,
            Vector3 position)
        {
            RequestPranaSound(
                soundEvent,
                position,
                transform.forward);
        }

        /// <summary>
        /// 指定された位置と方向で効果音を再生するよう
        /// プラーナム管理コンポーネントへ要求します。
        /// </summary>
        /// <param name="soundEvent">
        /// 再生するプラーナムサウンドイベントです。
        /// </param>
        /// <param name="position">
        /// 効果音を再生するワールド座標です。
        /// </param>
        /// <param name="forward">
        /// 3Dサウンドの向きとして使用する前方ベクトルです。
        /// </param>
        protected void RequestPranaSound(
            PranamSoundEvent soundEvent,
            Vector3 position,
            Vector3 forward)
        {
            if (!_isInitialized ||
                PranamBehavior == null ||
                Context == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    Context.SoundProfileId))
            {
                return;
            }

            Vector3 soundForward =
                NormalizeDirection(
                    forward);

            PranamBehavior.RequestPranamSoundByProfileId(
                Context.SoundProfileId,
                soundEvent,
                position,
                soundForward);
        }

        /// <summary>
        /// 指定されたColliderがこのプラーナの
        /// 所有者または味方チームに属するか判定します。
        /// </summary>
        /// <param name="hitCollider">
        /// 所有関係を確認するColliderです。
        /// </param>
        /// <returns>
        /// 所有者自身または味方のColliderの場合は
        /// <c>true</c>です。
        /// </returns>
        protected bool IsOwnerOrAllyCollider(
            Collider hitCollider)
        {
            return IsOwnerOrAllyPlayer(GetColliderPlayerId(hitCollider));
        }

        /// <summary>
        /// 攻撃者自身のユニットまたはシールドへの接触か判定します。
        /// </summary>
        /// <returns>
        /// 有効な所有者IDが一致する場合は<c>true</c>です。
        /// </returns>
        protected bool IsOwnerCollider(Collider hitCollider)
        {
            return OwnerPlayerId >= 0 && GetColliderPlayerId(hitCollider) == OwnerPlayerId;
        }

        /// <summary>
        /// 子Colliderからシールドまたはユニットの所有者IDを取得します。
        /// </summary>
        private static int GetColliderPlayerId(Collider hitCollider)
        {
            if (hitCollider == null)
            {
                return k_InvalidPlayerId;
            }

            ShieldPranaBase shield =
                hitCollider.GetComponentInParent<ShieldPranaBase>();

            if (shield != null &&
                shield.OwnerPlayerId >= 0)
            {
                return shield.OwnerPlayerId;
            }

            IDamageable damageable =
                hitCollider.GetComponentInParent<IDamageable>();

            if (damageable != null &&
                damageable.PlayerId >= 0)
            {
                return damageable.PlayerId;
            }

            UnitBehavior unitBehavior =
                hitCollider.GetComponentInParent<UnitBehavior>();

            return unitBehavior != null ? unitBehavior.PlayerId : k_InvalidPlayerId;
        }

        /// <summary>
        /// 命中位置を内包する全方位シールドを取得します。
        /// </summary>
        /// <remarks>
        /// 球の内部から開始するRaycastでは球面を検出できないため、
        /// 高速弾が球面を越えて本体へ接触した場合をOverlapで補正します。
        /// </remarks>
        /// <param name="targetPlayerId">
        /// 防御対象のプレイヤーIDです。
        /// </param>
        /// <param name="position">
        /// ユニットへの命中位置です。
        /// </param>
        /// <param name="projectileDirection">
        /// 弾の進行方向です。
        /// </param>
        /// <param name="layerMask">
        /// シールドColliderの検索対象レイヤーです。
        /// </param>
        /// <param name="processedShieldIds">
        /// この弾が既にダメージを適用したシールドIDです。
        /// </param>
        /// <param name="shield">
        /// 取得された全方位シールドです。
        /// </param>
        /// <returns>
        /// 対象所有者の未処理シールドに命中位置が含まれる場合は<c>true</c>です。
        /// </returns>
        protected bool TryGetContainingShield(
            int targetPlayerId,
            Vector3 position,
            Vector3 projectileDirection,
            int layerMask,
            ISet<EntityId> processedShieldIds,
            out ShieldPranaBase shield)
        {
            shield = null;

            foreach (Collider collider in Physics.OverlapSphere(
                position,
                k_ContainingShieldSearchRadius,
                layerMask,
                QueryTriggerInteraction.Collide))
            {
                ShieldPranaBase candidate = collider.GetComponentInParent<ShieldPranaBase>();

                if (candidate == null || !candidate.BlocksFromAllDirections ||
                    candidate.OwnerPlayerId != targetPlayerId ||
                    IsOwnerOrAllyPlayer(candidate.OwnerPlayerId) ||
                    !candidate.CanBlockProjectile(projectileDirection, 0.0f) ||
                    processedShieldIds.Contains(candidate.GetEntityId()) ||
                    (collider.ClosestPoint(position) - position).sqrMagnitude > Mathf.Epsilon)
                {
                    continue;
                }

                shield = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 指定されたプレイヤーがこのプラーナの
        /// 所有者または味方チームに属するか判定します。
        /// </summary>
        /// <param name="targetPlayerId">
        /// 判定対象のプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 所有者自身または味方の場合は<c>true</c>です。
        /// </returns>
        protected bool IsOwnerOrAllyPlayer(
            int targetPlayerId)
        {
            int ownerPlayerId =
                OwnerPlayerId;

            if (ownerPlayerId < 0 ||
                targetPlayerId < 0)
            {
                return false;
            }

            if (ownerPlayerId ==
                targetPlayerId)
            {
                return true;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return false;
            }

            int ownerTeamId =
                ResolveTeamId(
                    unitManager,
                    ownerPlayerId);

            int targetTeamId =
                ResolveTeamId(
                    unitManager,
                    targetPlayerId);

            return ownerTeamId >= 0 &&
                targetTeamId >= 0 &&
                ownerTeamId ==
                targetTeamId;
        }

        /// <summary>
        /// UnitManagerまたは登録済みUnitから
        /// 指定プレイヤーのチームIDを取得します。
        /// </summary>
        private static int ResolveTeamId(
            UnitManager unitManager,
            int playerId)
        {
            if (unitManager == null ||
                playerId < 0)
            {
                return -1;
            }

            int teamId =
                unitManager.GetTeamId(
                    playerId);

            if (teamId >= 0)
            {
                return teamId;
            }

            if (!unitManager.TryGetUnit(
                    playerId,
                    out UnitBehavior unitBehavior) ||
                unitBehavior == null)
            {
                return -1;
            }

            return unitBehavior.GetTeamId();
        }

        /// <summary>
        /// 移行期間中の互換性を維持するため、
        /// このプラーナを生成したプレイヤーのIDを返します。
        /// </summary>
        /// <returns>
        /// このプラーナを生成したプレイヤーのIDです。
        /// </returns>
        [Obsolete(
            "PlayerId()は非推奨です。OwnerPlayerIdプロパティを使用してください。")]
        public int PlayerId()
        {
            return OwnerPlayerId;
        }

        /// <summary>
        /// 指定された方向を正規化します。
        /// </summary>
        /// <param name="direction">
        /// 正規化する方向です。
        /// </param>
        /// <returns>
        /// 正規化済みの方向です。
        /// 指定された方向が0の場合はTransformの前方を使用します。
        /// </returns>
        private Vector3 NormalizeDirection(
            Vector3 direction)
        {
            if (direction.sqrMagnitude >
                Mathf.Epsilon)
            {
                return direction.normalized;
            }

            Vector3 transformForward =
                transform.forward;

            if (transformForward.sqrMagnitude >
                Mathf.Epsilon)
            {
                return transformForward.normalized;
            }

            return Vector3.forward;
        }
    }
}
