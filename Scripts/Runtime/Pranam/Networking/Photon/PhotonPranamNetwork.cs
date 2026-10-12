using System;
using Newtonsoft.Json;
using Photon.Pun;
using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Unit;

namespace PranaTactics.Pranam.Networking.Photon
{
    /// <summary>
    /// Photon PUNのRPCを使用して、プラーナの生成、命中、
    /// 破棄および効果音再生を各クライアントへ同期します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(PranamBehavior))]
    public sealed class PhotonPranamNetwork :
        MonoBehaviourPun,
        IPranamNetwork
    {
        /// <summary>
        /// ダメージとして有効と判断する最小値です。
        /// </summary>
        private const float k_MinimumDamage = 0.0f;

        /// <summary>
        /// RPCで受信した処理を適用する
        /// プラーナム管理コンポーネントです。
        /// </summary>
        private PranamBehavior _pranamBehavior;

        /// <summary>
        /// 必要なコンポーネントへの参照を取得します。
        /// </summary>
        private void Awake()
        {
            CacheReferences();
        }

        /// <summary>
        /// 指定されたPhoton Viewをプラーナ格納オブジェクトの
        /// 子として設定するRPCを送信します。
        /// </summary>
        /// <param name="viewId">
        /// 親設定を行うPhoton View IDです。
        /// </param>
        public void SetPranaParent(
            int viewId,
            bool isMountRoot)
        {
            if (viewId <= 0)
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    $"無効なPhoton View IDが指定されました。" +
                    $" ViewId={viewId}",
                    this);

                return;
            }

            if (!CanSendRpc())
            {
                return;
            }

            photonView.RPC(
                nameof(RpcSetPranaParent),
                RpcTarget.All,
                viewId,
                isMountRoot);
        }

        /// <summary>
        /// 指定された情報を使用してプラーナを生成するRPCを送信します。
        /// </summary>
        /// <param name="prefabPath">
        /// ResourcesフォルダからのPrefab相対パスです。
        /// </param>
        /// <param name="position">
        /// プラーナを生成するワールド座標です。
        /// </param>
        /// <param name="rotation">
        /// プラーナを生成する回転です。
        /// </param>
        /// <param name="context">
        /// プラーナの所有者、性能および対象情報です。
        /// </param>
        public void InstantiatePrana(
            string prefabPath,
            Vector3 position,
            Quaternion rotation,
            PranamContext context)
        {
            if (string.IsNullOrWhiteSpace(
                    prefabPath))
            {
                Debug.LogError(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    "プラーナのPrefabパスが設定されていません。",
                    this);

                return;
            }

            if (context == null)
            {
                Debug.LogError(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    $"{nameof(PranamContext)}が設定されていません。",
                    this);

                return;
            }

            if (!CanSendRpc())
            {
                return;
            }

            if (!TrySerializeContext(
                    context,
                    out string contextJson))
            {
                return;
            }

            photonView.RPC(
                nameof(RpcInstantiatePrana),
                RpcTarget.All,
                prefabPath,
                position,
                rotation,
                contextJson);
        }

        /// <summary>
        /// 指定された対象ユニットへダメージを適用するRPCを送信します。
        /// </summary>
        /// <param name="attackerPlayerId">
        /// 攻撃を行ったプレイヤーのIDです。
        /// </param>
        /// <param name="targetUnitId">
        /// ダメージを受ける対象ユニットのIDです。
        /// </param>
        /// <param name="damage">
        /// 対象へ適用するダメージ量です。
        /// </param>
        /// <param name="hitSlowdownRatio">
        /// 通常・歩行速度を減らす割合です。0から1で指定します。
        /// </param>
        /// <param name="hitSlowdownDurationSeconds">
        /// 減速が持続するゲーム内秒数です。
        /// </param>
        /// <param name="sourcePranamSlot">
        /// 発射時の兵装スロットです。不明な場合は-1です。
        /// </param>
        public void HitUnit(
            int attackerPlayerId,
            int targetUnitId,
            float damage,
            float hitSlowdownRatio = 0.0f,
            float hitSlowdownDurationSeconds = 0.0f,
            int sourcePranamSlot = -1)
        {
            if (targetUnitId < 0)
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    $"無効な対象ユニットIDが指定されました。" +
                    $" TargetUnitId={targetUnitId}",
                    this);

                return;
            }

            float sanitizedDamage =
                Mathf.Max(
                    k_MinimumDamage,
                    damage);

            if (sanitizedDamage <=
                k_MinimumDamage)
            {
                return;
            }

            if (!CanSendRpc())
            {
                return;
            }

            photonView.RPC(
                nameof(RpcHitUnit),
                RpcTarget.All,
                attackerPlayerId,
                targetUnitId,
                sanitizedDamage,
                hitSlowdownRatio,
                hitSlowdownDurationSeconds,
                sourcePranamSlot);
        }

        /// <summary>
        /// 指定されたプラーナを破棄するRPCを送信します。
        /// </summary>
        /// <param name="pranaId">
        /// 破棄するプラーナの識別子です。
        /// </param>
        public void DestroyPrana(int pranaId)
        {
            if (!CanSendRpc())
            {
                return;
            }

            photonView.RPC(
                nameof(RpcDestroyPrana),
                RpcTarget.All,
                pranaId);
        }

        /// <summary>
        /// プラーナム効果音を各クライアントで
        /// 再生するRPCを送信します。
        /// </summary>
        /// <param name="soundProfileId">
        /// 使用するサウンドプロファイルの識別子です。
        /// </param>
        /// <param name="soundEvent">
        /// 再生するプラーナムサウンドイベントです。
        /// </param>
        /// <param name="position">
        /// 効果音を再生するワールド座標です。
        /// </param>
        /// <param name="forward">
        /// 3Dサウンドの向きとして使用する前方ベクトルです。
        /// </param>
        public void PlayPranamSound(
            string soundProfileId,
            PranamSoundEvent soundEvent,
            Vector3 position,
            Vector3 forward)
        {
            if (string.IsNullOrWhiteSpace(
                    soundProfileId))
            {
                return;
            }

            if (!CanSendRpc())
            {
                return;
            }

            photonView.RPC(
                nameof(RpcPlayPranamSound),
                RpcTarget.All,
                soundProfileId,
                (int)soundEvent,
                position,
                forward);
        }

        /// <summary>
        /// 指定されたPhoton Viewをプラーナ格納オブジェクトの
        /// 子として設定します。
        /// </summary>
        /// <param name="viewId">
        /// 親設定を行うPhoton View IDです。
        /// </param>
        [PunRPC]
        private void RpcSetPranaParent(
            int viewId,
            bool isMountRoot)
        {
            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            pranamBehavior.ApplySetPranaParent(
                viewId,
                isMountRoot);
        }

        /// <summary>
        /// RPCで受信した情報を復元し、
        /// プラーナを各クライアントへ生成します。
        /// </summary>
        /// <param name="prefabPath">
        /// ResourcesフォルダからのPrefab相対パスです。
        /// </param>
        /// <param name="position">
        /// プラーナを生成するワールド座標です。
        /// </param>
        /// <param name="rotation">
        /// プラーナを生成する回転です。
        /// </param>
        /// <param name="contextJson">
        /// JSON形式へ変換されたプラーナ生成情報です。
        /// </param>
        [PunRPC]
        private void RpcInstantiatePrana(
            string prefabPath,
            Vector3 position,
            Quaternion rotation,
            string contextJson)
        {
            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    prefabPath))
            {
                Debug.LogError(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    "RPCで受信したPrefabパスが空です。",
                    this);

                return;
            }

            if (!TryDeserializeContext(
                    contextJson,
                    out PranamContext context))
            {
                return;
            }

            pranamBehavior.ApplyInstantiate(
                prefabPath,
                position,
                rotation,
                context);
        }

        /// <summary>
        /// 対象ユニットの処理権限を持つクライアントで、
        /// 受信したダメージを適用します。
        /// </summary>
        /// <param name="attackerPlayerId">
        /// 攻撃を行ったプレイヤーのIDです。
        /// </param>
        /// <param name="targetUnitId">
        /// ダメージを受ける対象ユニットのIDです。
        /// </param>
        /// <param name="damage">
        /// 対象へ適用するダメージ量です。
        /// </param>
        /// <param name="hitSlowdownRatio">
        /// 通常・歩行速度を減らす割合です。0から1で指定します。
        /// </param>
        /// <param name="hitSlowdownDurationSeconds">
        /// 減速が持続するゲーム内秒数です。
        /// </param>
        /// <param name="sourcePranamSlot">
        /// 発射時の兵装スロットです。不明な場合は-1です。
        /// </param>
        [PunRPC]
        private void RpcHitUnit(
            int attackerPlayerId,
            int targetUnitId,
            float damage,
            float hitSlowdownRatio,
            float hitSlowdownDurationSeconds,
            int sourcePranamSlot)
        {
            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    $"{nameof(UnitManager)}が存在しないため、" +
                    "命中処理を適用できません。",
                    this);

                return;
            }

            GameObject targetUnitObject =
                unitManager.GetUnitObject(
                    targetUnitId);

            if (targetUnitObject == null)
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    "命中対象のユニットを取得できません。" +
                    $" TargetUnitId={targetUnitId}",
                    this);

                return;
            }

            if (!targetUnitObject.TryGetComponent(
                    out UnitBehavior targetUnitBehavior))
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    $"対象に{nameof(UnitBehavior)}がありません。" +
                    $" TargetUnitId={targetUnitId}",
                    targetUnitObject);

                return;
            }

            if (!unitManager.CanExecuteUnitLogic(
                    targetUnitBehavior.PlayerId))
            {
                return;
            }

            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            pranamBehavior.ApplyHit(
                attackerPlayerId,
                targetUnitId,
                Mathf.Max(
                    k_MinimumDamage,
                    damage),
                hitSlowdownRatio,
                hitSlowdownDurationSeconds,
                sourcePranamSlot);
        }

        /// <summary>
        /// 指定されたプラーナを各クライアントで破棄します。
        /// </summary>
        /// <param name="pranaId">
        /// 破棄するプラーナの識別子です。
        /// </param>
        [PunRPC]
        private void RpcDestroyPrana(int pranaId)
        {
            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            pranamBehavior.ApplyDestroyPrana(
                pranaId);
        }

        /// <summary>
        /// 指定されたプラーナム効果音を
        /// 各クライアントで再生します。
        /// </summary>
        /// <param name="soundProfileId">
        /// 使用するサウンドプロファイルの識別子です。
        /// </param>
        /// <param name="soundEventValue">
        /// 整数へ変換されたサウンドイベントです。
        /// </param>
        /// <param name="position">
        /// 効果音を再生するワールド座標です。
        /// </param>
        /// <param name="forward">
        /// 3Dサウンドの向きとして使用する前方ベクトルです。
        /// </param>
        [PunRPC]
        private void RpcPlayPranamSound(
            string soundProfileId,
            int soundEventValue,
            Vector3 position,
            Vector3 forward)
        {
            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    soundProfileId))
            {
                return;
            }

            if (!Enum.IsDefined(
                    typeof(PranamSoundEvent),
                    soundEventValue))
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    "未定義のサウンドイベントを受信しました。" +
                    $" EventValue={soundEventValue}",
                    this);

                return;
            }

            pranamBehavior.ApplyPranamSound(
                soundProfileId,
                (PranamSoundEvent)soundEventValue,
                position,
                forward);
        }

        /// <summary>
        /// プラーナ生成情報をJSON文字列へ変換します。
        /// </summary>
        /// <param name="context">
        /// JSONへ変換する生成情報です。
        /// </param>
        /// <param name="contextJson">
        /// 変換されたJSON文字列です。
        /// </param>
        /// <returns>
        /// 変換に成功した場合は<c>true</c>です。
        /// </returns>
        private bool TrySerializeContext(
            PranamContext context,
            out string contextJson)
        {
            contextJson = string.Empty;

            try
            {
                contextJson =
                    JsonConvert.SerializeObject(
                        context);

                return true;
            }
            catch (JsonException exception)
            {
                Debug.LogError(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    $"{nameof(PranamContext)}のJSON変換に失敗しました。\n" +
                    exception,
                    this);

                return false;
            }
        }

        /// <summary>
        /// JSON文字列からプラーナ生成情報を復元します。
        /// </summary>
        /// <param name="contextJson">
        /// 復元するJSON文字列です。
        /// </param>
        /// <param name="context">
        /// 復元されたプラーナ生成情報です。
        /// </param>
        /// <returns>
        /// 復元に成功した場合は<c>true</c>です。
        /// </returns>
        private bool TryDeserializeContext(
            string contextJson,
            out PranamContext context)
        {
            context = null;

            if (string.IsNullOrWhiteSpace(
                    contextJson))
            {
                Debug.LogError(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    "受信したプラーナ生成情報が空です。",
                    this);

                return false;
            }

            try
            {
                context =
                    JsonConvert.DeserializeObject<PranamContext>(
                        contextJson);

                if (context != null)
                {
                    return true;
                }

                Debug.LogError(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    $"{nameof(PranamContext)}の復元結果がnullです。",
                    this);

                return false;
            }
            catch (JsonException exception)
            {
                Debug.LogError(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    $"{nameof(PranamContext)}の復元に失敗しました。\n" +
                    exception,
                    this);

                return false;
            }
        }

        /// <summary>
        /// RPCの送信に必要なPhoton接続状態を確認します。
        /// </summary>
        /// <returns>
        /// RPCを送信できる状態の場合は<c>true</c>です。
        /// </returns>
        private bool CanSendRpc()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    "Photonルームへ参加していないため、" +
                    "RPCを送信できません。",
                    this);

                return false;
            }

            if (photonView == null ||
                photonView.ViewID == 0)
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPranamNetwork)}: " +
                    "有効なPhotonViewが設定されていません。",
                    this);

                return false;
            }

            return true;
        }

        /// <summary>
        /// 必要なコンポーネントへの参照を取得します。
        /// </summary>
        private void CacheReferences()
        {
            _pranamBehavior =
                GetComponent<PranamBehavior>();

            if (_pranamBehavior != null)
            {
                return;
            }

            Debug.LogError(
                $"{nameof(PhotonPranamNetwork)}: " +
                $"{nameof(_pranamBehavior)}を取得できません。",
                this);
        }

        /// <summary>
        /// 有効な<see cref="PranamBehavior"/>の取得を試みます。
        /// </summary>
        /// <param name="pranamBehavior">
        /// 取得されたプラーナム管理コンポーネントです。
        /// </param>
        /// <returns>
        /// 有効なコンポーネントを取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetPranamBehavior(
            out PranamBehavior pranamBehavior)
        {
            if (_pranamBehavior == null)
            {
                CacheReferences();
            }

            pranamBehavior =
                _pranamBehavior;

            return pranamBehavior != null;
        }
    }
}
