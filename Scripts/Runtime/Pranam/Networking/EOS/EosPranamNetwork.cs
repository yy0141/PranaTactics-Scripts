using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Networking;
using PranaTactics.Networking.EOS;
using PranaTactics.Unit;

namespace PranaTactics.Pranam.Networking.EOS
{
    /// <summary>
    /// EOS P2Pを使用して、プラーナの生成、命中、破棄および
    /// プラーナム効果音を各クライアントへ同期します。
    /// </summary>
    /// <remarks>
    /// プラーナの生成、破棄および効果音は、
    /// そのプラーナムの実行権限を持つクライアントから送信します。
    ///
    /// プレイヤーへの命中は攻撃対象の所有クライアント、
    /// AIへの命中はAIを管理するホストが確定し、
    /// 確定した結果を全クライアントへ共有します。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PranamBehavior))]
    public sealed class EosPranamNetwork :
        MonoBehaviour,
        IPranamNetwork
    {
        /// <summary>
        /// 有効なプレイヤーIDが存在しないことを表す値です。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// 有効なプラーナIDが存在しないことを表す値です。
        /// </summary>
        private const int k_InvalidPranaId = -1;

        /// <summary>
        /// 有効なダメージとして扱う最小値です。
        /// </summary>
        private const float k_MinimumDamage = 0.0f;

        /// <summary>
        /// 受信した処理をゲームへ適用する
        /// プラーナム管理コンポーネントです。
        /// </summary>
        private PranamBehavior _pranamBehavior;

        /// <summary>
        /// EOSのロビーおよびP2P通信を管理するサービスです。
        /// </summary>
        private EosOnlineService _onlineService;

        /// <summary>
        /// この通信コンポーネントが
        /// メインプラーナムに対応するかどうかを示します。
        /// </summary>
        private bool _isMain;

        /// <summary>
        /// 通信コンポーネントの初期設定が完了しているかどうかを示します。
        /// </summary>
        private bool _isConfigured;

        /// <summary>
        /// 送信メッセージへ設定する連番です。
        /// </summary>
        private int _sendSequence;

        /// <summary>
        /// 送信元とメッセージ種別ごとに、
        /// 最後に処理した受信シーケンス番号を保持します。
        /// </summary>
        private readonly Dictionary<string, int>
            _lastReceivedSequenceByStream = new();

        /// <summary>
        /// 必要なコンポーネントへの参照を取得します。
        /// </summary>
        private void Awake()
        {
            CacheReferences();
        }

        /// <summary>
        /// EOSオンラインサービスのイベントを購読します。
        /// </summary>
        private void OnEnable()
        {
            TryBindOnlineService();
        }

        /// <summary>
        /// EOSサービスの生成が遅れた場合に、
        /// サービスへの接続を再試行します。
        /// </summary>
        private void Update()
        {
            if (_onlineService == null)
            {
                TryBindOnlineService();
            }
        }

        /// <summary>
        /// EOSオンラインサービスのイベント購読を解除します。
        /// </summary>
        private void OnDisable()
        {
            UnbindOnlineService();
        }

        /// <summary>
        /// オブジェクト破棄時にイベント購読を解除します。
        /// </summary>
        private void OnDestroy()
        {
            UnbindOnlineService();
        }

        /// <summary>
        /// 所有するプラーナムとメイン・サブの種別を設定します。
        /// </summary>
        /// <param name="pranamBehavior">
        /// この通信コンポーネントが処理する
        /// プラーナム管理コンポーネントです。
        /// </param>
        /// <param name="isMain">
        /// メインプラーナムに対応する場合は<c>true</c>です。
        /// </param>
        public void Configure(
            PranamBehavior pranamBehavior,
            bool isMain)
        {
            _pranamBehavior =
                pranamBehavior != null
                    ? pranamBehavior
                    : GetComponent<PranamBehavior>();

            _isMain = isMain;
            _isConfigured = _pranamBehavior != null;

            _sendSequence = 0;
            _lastReceivedSequenceByStream.Clear();

            TryBindOnlineService();

            if (!_isConfigured)
            {
                Debug.LogError(
                    $"{nameof(EosPranamNetwork)}: " +
                    $"{nameof(PranamBehavior)}を設定できませんでした。",
                    this);

                return;
            }

            Debug.Log(
                $"{nameof(EosPranamNetwork)} configured: " +
                $"owner={GetOwnerPlayerId()}, " +
                $"isMain={_isMain}, " +
                $"object={gameObject.name}",
                this);
        }

        /// <summary>
        /// 指定されたオブジェクトをプラーナ格納先へ設定します。
        /// </summary>
        /// <param name="viewId">
        /// 親設定に使用するオブジェクト識別子です。
        /// </param>
        /// <param name="isMountRoot">
        /// <c>true</c>の場合はMountRoot、
        /// <c>false</c>の場合はWorldRootへ設定します。
        /// </param>
        /// <remarks>
        /// EOSでは通常、生成時点でプラーナ格納オブジェクトの
        /// 子として設定されます。
        /// この処理は既存実装との互換性のために残しています。
        /// 送信元クライアントにも即時反映します。
        /// </remarks>
        public void SetPranaParent(
                    int pranaId,
                    bool isMountRoot)
        {
            if (!HasSendAuthority() ||
                !TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            pranamBehavior.ApplySetPranaParent(
                pranaId,
                isMountRoot);

            EosPranamP2PMessage message =
                CreateMessage(
                    EosPranamP2PMessageType.SetParent);

            message.PranaId = pranaId;
            message.IsMountRoot = isMountRoot;

            TryBroadcast(message);
        }

        /// <summary>
        /// プラーナをローカルへ生成し、
        /// 生成情報を他のクライアントへ送信します。
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
                Debug.LogWarning(
                    $"{nameof(EosPranamNetwork)}: " +
                    "プラーナのPrefabパスが設定されていません。",
                    this);

                return;
            }

            if (context == null)
            {
                Debug.LogWarning(
                    $"{nameof(EosPranamNetwork)}: " +
                    $"{nameof(PranamContext)}が設定されていません。" +
                    $" Owner={GetOwnerPlayerId()}, " +
                    $"IsMain={_isMain}, " +
                    $"Prefab={prefabPath}",
                    this);

                return;
            }

            if (!HasLocalExecutionAuthority())
            {
                Debug.LogWarning(
                    $"{nameof(EosPranamNetwork)}: " +
                    "プラーナのローカル実行権限がありません。" +
                    $" Owner={GetOwnerPlayerId()}, " +
                    $"GameplayPlayer={GetLocalGameplayPlayerId()}, " +
                    $"OnlinePlayer={_onlineService?.LocalPlayerId ?? k_InvalidPlayerId}, " +
                    $"IsMain={_isMain}, " +
                    $"Prefab={prefabPath}",
                    this);

                return;
            }

            if (!TrySerializeContext(
                    context,
                    out string contextJson))
            {
                return;
            }

            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            pranamBehavior.ApplyInstantiate(
                prefabPath,
                position,
                rotation,
                context);

            EosPranamP2PMessage message =
                CreateMessage(
                    EosPranamP2PMessageType.Instantiate);

            message.PrefabPath = prefabPath;
            message.Position = position;
            message.Rotation = rotation;
            message.PranaId = context.Id;
            message.ContextJson = contextJson;

            if (TryBroadcast(
                    message,
                    false))
            {
                Debug.Log(
                    $"{nameof(EosPranamNetwork)} spawn sent: " +
                    $"owner={message.OwnerId}, " +
                    $"isMain={message.IsMain}, " +
                    $"pranaId={message.PranaId}, " +
                    $"prefab={prefabPath}, " +
                    $"sequence={message.Sequence}",
                    this);

                return;
            }

            Debug.LogWarning(
                $"{nameof(EosPranamNetwork)}: " +
                "プラーナはローカルに生成されましたが、" +
                "P2P送信の準備が完了していません。" +
                $" Owner={message.OwnerId}, " +
                $"IsMain={message.IsMain}, " +
                $"PranaId={message.PranaId}, " +
                $"Prefab={prefabPath}",
                this);
        }

        /// <summary>
        /// 対象ユニットへの命中確定処理を要求します。
        /// </summary>
        /// <param name="attackerPlayerId">
        /// 攻撃を行ったプレイヤーのIDです。
        /// </param>
        /// <param name="targetPlayerId">
        /// 攻撃対象となるプレイヤーまたはAIのIDです。
        /// </param>
        /// <param name="damage">
        /// 適用するダメージ量です。
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
            int targetPlayerId,
            float damage,
            float hitSlowdownRatio = 0.0f,
            float hitSlowdownDurationSeconds = 0.0f,
            int sourcePranamSlot = -1)
        {
            ProcessAuthoritativeHit(
                attackerPlayerId,
                k_InvalidPranaId,
                targetPlayerId,
                damage,
                hitSlowdownRatio,
                hitSlowdownDurationSeconds,
                sourcePranamSlot);
        }

        /// <summary>
        /// 対象への命中と、命中したプラーナの破棄を
        /// 1つの信頼性ありメッセージとして同期します。
        /// </summary>
        /// <param name="attackerPlayerId">
        /// 攻撃を行ったプレイヤーのIDです。
        /// </param>
        /// <param name="pranaId">
        /// 命中後に破棄するプラーナのIDです。
        /// </param>
        /// <param name="targetPlayerId">
        /// 攻撃対象となるプレイヤーまたはAIのIDです。
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
        public void HitAndDestroy(
            int attackerPlayerId,
            int pranaId,
            int targetPlayerId,
            float damage,
            float hitSlowdownRatio = 0.0f,
            float hitSlowdownDurationSeconds = 0.0f,
            int sourcePranamSlot = -1)
        {
            ProcessAuthoritativeHit(
                attackerPlayerId,
                pranaId,
                targetPlayerId,
                damage,
                hitSlowdownRatio,
                hitSlowdownDurationSeconds,
                sourcePranamSlot);
        }

        /// <summary>
        /// 指定されたプラーナをローカルで破棄し、
        /// 破棄情報を他のクライアントへ送信します。
        /// </summary>
        /// <param name="pranaId">
        /// 破棄するプラーナのIDです。
        /// </param>
        public void DestroyPrana(int pranaId)
        {
            if (!HasSendAuthority() ||
                !TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            pranamBehavior.ApplyDestroyPrana(
                pranaId);

            EosPranamP2PMessage message =
                CreateMessage(
                    EosPranamP2PMessageType.Destroy);

            message.PranaId = pranaId;

            TryBroadcast(
                message);

            Debug.Log(
                $"{nameof(EosPranamNetwork)} destroy sent: " +
                $"owner={message.OwnerId}, " +
                $"isMain={message.IsMain}, " +
                $"pranaId={pranaId}, " +
                $"sequence={message.Sequence}",
                this);
        }

        /// <summary>
        /// プラーナム効果音をローカルで再生し、
        /// 再生情報を他のクライアントへ送信します。
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

            if (!HasSendAuthority() ||
                !TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            pranamBehavior.ApplyPranamSound(
                soundProfileId,
                soundEvent,
                position,
                forward);

            EosPranamP2PMessage message =
                CreateMessage(
                    EosPranamP2PMessageType.PlaySound);

            message.SoundProfileId = soundProfileId;
            message.SoundEvent = (int)soundEvent;
            message.Position = position;
            message.Forward = forward;

            TryBroadcast(
                message);
        }

        /// <summary>
        /// 対象所有クライアントまたはAI管理ホストとして、
        /// 命中結果を確定します。
        /// </summary>
        /// <param name="attackerPlayerId">
        /// 攻撃を行ったプレイヤーのIDです。
        /// </param>
        /// <param name="pranaId">
        /// 命中後に破棄するプラーナのIDです。
        /// </param>
        /// <param name="targetPlayerId">
        /// ダメージを受けるプレイヤーまたはAIのIDです。
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
        private void ProcessAuthoritativeHit(
            int attackerPlayerId,
            int pranaId,
            int targetPlayerId,
            float damage,
            float hitSlowdownRatio = 0.0f,
            float hitSlowdownDurationSeconds = 0.0f,
            int sourcePranamSlot = -1)
        {
            TryBindOnlineService();

            if (!HasHitAuthority(
                    targetPlayerId))
            {
                return;
            }

            if (damage <= k_MinimumDamage)
            {
                Debug.LogWarning(
                    $"{nameof(EosPranamNetwork)}: " +
                    "0以下のダメージは適用できません。" +
                    $" Attacker={attackerPlayerId}, " +
                    $"Target={targetPlayerId}, " +
                    $"Damage={damage}",
                    this);

                return;
            }

            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            pranamBehavior.ApplyHit(
                attackerPlayerId,
                targetPlayerId,
                damage,
                hitSlowdownRatio,
                hitSlowdownDurationSeconds,
                sourcePranamSlot);

            if (pranaId >= 0)
            {
                pranamBehavior.ApplyDestroyPrana(
                    pranaId);
            }

            EosPranamP2PMessage message =
                CreateMessage(
                    EosPranamP2PMessageType.Hit);

            message.AttackerId = attackerPlayerId;
            message.TargetId = targetPlayerId;
            message.Damage = damage;
            message.HitSlowdownRatio = hitSlowdownRatio;
            message.HitSlowdownDurationSeconds = hitSlowdownDurationSeconds;
            message.SourcePranamSlot = sourcePranamSlot;
            message.PranaId = pranaId;

            if (!TryBroadcast(
                    message))
            {
                Debug.LogWarning(
                    $"{nameof(EosPranamNetwork)}: " +
                    "命中結果をローカルへ適用しましたが、" +
                    "他のクライアントへ送信できませんでした。" +
                    $" Attacker={attackerPlayerId}, " +
                    $"Target={targetPlayerId}, " +
                    $"PranaId={pranaId}",
                    this);

                return;
            }

            Debug.Log(
                $"{nameof(EosPranamNetwork)} authoritative hit: " +
                $"localPlayer={_onlineService?.LocalPlayerId ?? k_InvalidPlayerId}, " +
                $"attacker={attackerPlayerId}, " +
                $"target={targetPlayerId}, " +
                $"damage={damage:0.###}, " +
                $"pranaId={pranaId}, " +
                $"owner={message.OwnerId}, " +
                $"isMain={message.IsMain}, " +
                $"sequence={message.Sequence}",
                this);
        }

        /// <summary>
        /// EOS P2Pから受信したプラーナ同期メッセージを処理します。
        /// </summary>
        /// <param name="message">
        /// 受信したプラーナ同期メッセージです。
        /// </param>
        private void HandlePranamMessage(
            EosPranamP2PMessage message)
        {
            if (message == null ||
                !_isConfigured ||
                _pranamBehavior == null)
            {
                return;
            }

            if (message.OwnerId !=
                GetOwnerPlayerId())
            {
                return;
            }

            if (message.IsMain !=
                _isMain)
            {
                return;
            }

            if (IsDuplicateOrOldMessage(
                    message))
            {
                return;
            }

            switch (message.MessageType)
            {
                case EosPranamP2PMessageType.Instantiate:
                    ApplyInstantiateMessage(
                        message);
                    break;

                case EosPranamP2PMessageType.Hit:
                    ApplyHitMessage(
                        message);
                    break;

                case EosPranamP2PMessageType.Destroy:
                    _pranamBehavior.ApplyDestroyPrana(
                        message.PranaId);
                    break;

                case EosPranamP2PMessageType.PlaySound:
                    ApplySoundMessage(
                        message);
                    break;

                case EosPranamP2PMessageType.SetParent:
                    _pranamBehavior.ApplySetPranaParent(
                        message.ParentId,
                        message.IsMountRoot);
                    break;

                default:
                    Debug.LogWarning(
                        $"{nameof(EosPranamNetwork)}: " +
                        "未対応のメッセージ種別を受信しました。" +
                        $" Type={message.MessageType}",
                        this);
                    break;
            }
        }

        /// <summary>
        /// 受信したプラーナ生成メッセージを適用します。
        /// </summary>
        /// <param name="message">
        /// 受信した生成メッセージです。
        /// </param>
        private void ApplyInstantiateMessage(
            EosPranamP2PMessage message)
        {
            if (string.IsNullOrWhiteSpace(
                    message.PrefabPath))
            {
                Debug.LogWarning(
                    $"{nameof(EosPranamNetwork)}: " +
                    "受信したプラーナ生成メッセージに" +
                    "Prefabパスがありません。",
                    this);

                return;
            }

            if (!TryDeserializeContext(
                    message.ContextJson,
                    out PranamContext context))
            {
                return;
            }

            context.Id =
                message.PranaId;

            _pranamBehavior.ApplyInstantiate(
                message.PrefabPath,
                message.Position,
                message.Rotation,
                context);

            Debug.Log(
                $"{nameof(EosPranamNetwork)} spawn received: " +
                $"sender={message.SenderPlayerId}, " +
                $"owner={message.OwnerId}, " +
                $"isMain={message.IsMain}, " +
                $"pranaId={message.PranaId}, " +
                $"prefab={message.PrefabPath}, " +
                $"sequence={message.Sequence}",
                this);
        }

        /// <summary>
        /// 受信した命中確定メッセージを適用します。
        /// </summary>
        /// <param name="message">
        /// 受信した命中メッセージです。
        /// </param>
        private void ApplyHitMessage(
            EosPranamP2PMessage message)
        {
            if (message.Damage <=
                k_MinimumDamage)
            {
                return;
            }

            _pranamBehavior.ApplyHit(
                message.AttackerId,
                message.TargetId,
                message.Damage,
                message.HitSlowdownRatio,
                message.HitSlowdownDurationSeconds,
                message.SourcePranamSlot);

            if (message.PranaId >= 0)
            {
                _pranamBehavior.ApplyDestroyPrana(
                    message.PranaId);
            }

            Debug.Log(
                $"{nameof(EosPranamNetwork)} hit received: " +
                $"sender={message.SenderPlayerId}, " +
                $"attacker={message.AttackerId}, " +
                $"target={message.TargetId}, " +
                $"damage={message.Damage:0.###}, " +
                $"pranaId={message.PranaId}, " +
                $"sequence={message.Sequence}",
                this);
        }

        /// <summary>
        /// 受信したプラーナム効果音メッセージを適用します。
        /// </summary>
        /// <param name="message">
        /// 受信した効果音メッセージです。
        /// </param>
        private void ApplySoundMessage(
            EosPranamP2PMessage message)
        {
            if (string.IsNullOrWhiteSpace(
                    message.SoundProfileId))
            {
                return;
            }

            if (!Enum.IsDefined(
                    typeof(PranamSoundEvent),
                    message.SoundEvent))
            {
                Debug.LogWarning(
                    $"{nameof(EosPranamNetwork)}: " +
                    "未定義のサウンドイベントを受信しました。" +
                    $" Event={message.SoundEvent}",
                    this);

                return;
            }

            _pranamBehavior.ApplyPranamSound(
                message.SoundProfileId,
                (PranamSoundEvent)message.SoundEvent,
                message.Position,
                message.Forward);
        }

        /// <summary>
        /// 受信メッセージが既に処理済み、
        /// または古いシーケンス番号かどうかを判定します。
        /// </summary>
        /// <param name="message">
        /// 判定対象の受信メッセージです。
        /// </param>
        /// <returns>
        /// 処理済みまたは古い場合は<c>true</c>です。
        /// </returns>
        private bool IsDuplicateOrOldMessage(
            EosPranamP2PMessage message)
        {
            string streamKey =
                $"{message.SenderPlayerId}:" +
                $"{message.OwnerId}:" +
                $"{message.IsMain}:" +
                $"{message.MessageType}";

            if (_lastReceivedSequenceByStream.TryGetValue(
                    streamKey,
                    out int lastSequence) &&
                message.Sequence <= lastSequence)
            {
                return true;
            }

            _lastReceivedSequenceByStream[streamKey] =
                message.Sequence;

            return false;
        }

        /// <summary>
        /// 共通情報を設定したEOS P2Pメッセージを生成します。
        /// </summary>
        /// <param name="messageType">
        /// 生成するメッセージの種別です。
        /// </param>
        /// <returns>
        /// 共通情報が設定されたメッセージです。
        /// </returns>
        private EosPranamP2PMessage CreateMessage(
            string messageType)
        {
            int senderPlayerId =
                _onlineService != null
                    ? _onlineService.LocalPlayerId
                    : GetLocalGameplayPlayerId();

            return new EosPranamP2PMessage
            {
                MessageType = messageType,
                OwnerId = GetOwnerPlayerId(),
                SenderPlayerId = senderPlayerId,
                Sequence = ++_sendSequence,
                IsMain = _isMain,
                PranaId = k_InvalidPranaId
            };
        }

        /// <summary>
        /// メッセージをEOS P2Pで全参加者へ送信します。
        /// </summary>
        /// <param name="message">
        /// 送信するメッセージです。
        /// </param>
        /// <param name="writeWarning">
        /// 送信できない場合に警告を出力する場合は<c>true</c>です。
        /// </param>
        /// <returns>
        /// 送信処理を開始できた場合は<c>true</c>です。
        /// </returns>
        private bool TryBroadcast(
            EosPranamP2PMessage message,
            bool writeWarning = true)
        {
            if (message == null)
            {
                return false;
            }

            TryBindOnlineService();

            if (_onlineService == null ||
                !_onlineService.IsInRoom)
            {
                if (writeWarning)
                {
                    Debug.LogWarning(
                        $"{nameof(EosPranamNetwork)}: " +
                        "EOS P2Pメッセージを送信できません。" +
                        $" Owner={message.OwnerId}, " +
                        $"Type={message.MessageType}",
                        this);
                }

                return false;
            }

            _onlineService.BroadcastPranamMessage(
                message,
                NetworkSendMode.Reliable);

            return true;
        }

        /// <summary>
        /// プラーナ生成情報をJSON文字列へ変換します。
        /// </summary>
        /// <param name="context">
        /// JSONへ変換するプラーナ生成情報です。
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
                        context,
                        Formatting.None);

                return true;
            }
            catch (JsonException exception)
            {
                Debug.LogError(
                    $"{nameof(EosPranamNetwork)}: " +
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
                Debug.LogWarning(
                    $"{nameof(EosPranamNetwork)}: " +
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

                Debug.LogWarning(
                    $"{nameof(EosPranamNetwork)}: " +
                    $"{nameof(PranamContext)}の復元結果がnullです。",
                    this);

                return false;
            }
            catch (JsonException exception)
            {
                Debug.LogWarning(
                    $"{nameof(EosPranamNetwork)}: " +
                    $"{nameof(PranamContext)}の復元に失敗しました。\n" +
                    exception,
                    this);

                return false;
            }
        }

        /// <summary>
        /// この通信コンポーネントが担当する
        /// プラーナムの所有プレイヤーIDを取得します。
        /// </summary>
        /// <returns>
        /// 所有プレイヤーIDです。
        /// </returns>
        private int GetOwnerPlayerId()
        {
            return _pranamBehavior != null
                ? _pranamBehavior.OwnerPlayerId
                : k_InvalidPlayerId;
        }

        /// <summary>
        /// ゲーム内で使用されているローカルプレイヤーIDを取得します。
        /// </summary>
        /// <returns>
        /// ローカルプレイヤーIDです。
        /// </returns>
        private int GetLocalGameplayPlayerId()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager != null)
            {
                return unitManager.LocalPlayerId;
            }

            return _onlineService != null
                ? _onlineService.LocalPlayerId
                : k_InvalidPlayerId;
        }

        /// <summary>
        /// 所有ユニットのプラーナム処理を
        /// このクライアントで実行できるか判定します。
        /// </summary>
        /// <remarks>
        /// ゲーム中の所有判定は<see cref="UnitManager"/>を優先します。
        /// EOSロビー側のプレイヤーID更新が遅れている場合でも、
        /// ローカル所有ユニットの攻撃を停止させないためです。
        /// </remarks>
        /// <returns>
        /// 実行権限がある場合は<c>true</c>です。
        /// </returns>
        private bool HasLocalExecutionAuthority()
        {
            TryBindOnlineService();

            if (!_isConfigured ||
                _pranamBehavior == null)
            {
                return false;
            }

            int ownerPlayerId =
                GetOwnerPlayerId();

            if (ownerPlayerId < 0)
            {
                return false;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager != null)
            {
                if (ownerPlayerId ==
                    unitManager.LocalPlayerId)
                {
                    return true;
                }

                if (unitManager.CanExecuteUnitLogic(
                        ownerPlayerId))
                {
                    return true;
                }
            }

            if (_onlineService != null &&
                ownerPlayerId ==
                _onlineService.LocalPlayerId)
            {
                return true;
            }

            return _onlineService != null &&
                _onlineService.IsHost &&
                unitManager != null &&
                unitManager.IsAIControlledUnit(
                    ownerPlayerId);
        }

        /// <summary>
        /// プラーナ同期メッセージを送信する権限があるか判定します。
        /// </summary>
        /// <returns>
        /// 送信権限がある場合は<c>true</c>です。
        /// </returns>
        private bool HasSendAuthority()
        {
            return HasLocalExecutionAuthority();
        }

        /// <summary>
        /// 指定された対象への命中を
        /// このクライアントで確定できるか判定します。
        /// </summary>
        /// <param name="targetPlayerId">
        /// 攻撃対象となるプレイヤーまたはAIのIDです。
        /// </param>
        /// <returns>
        /// 命中確定権限がある場合は<c>true</c>です。
        /// </returns>
        private bool HasHitAuthority(
            int targetPlayerId)
        {
            TryBindOnlineService();

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager != null &&
                targetPlayerId ==
                unitManager.LocalPlayerId)
            {
                return true;
            }

            if (_onlineService != null &&
                targetPlayerId ==
                _onlineService.LocalPlayerId)
            {
                return true;
            }

            return _onlineService != null &&
                _onlineService.IsHost &&
                unitManager != null &&
                unitManager.IsAIControlledUnit(
                    targetPlayerId);
        }

        /// <summary>
        /// 現在登録されているEOSオンラインサービスへ接続します。
        /// </summary>
        private void TryBindOnlineService()
        {
            EosOnlineService currentService =
                NetworkServiceLocator.Online
                    as EosOnlineService;

            if (currentService ==
                _onlineService)
            {
                return;
            }

            UnbindOnlineService();

            _onlineService =
                currentService;

            if (_onlineService == null)
            {
                return;
            }

            _onlineService.PranamMessageReceived -=
                HandlePranamMessage;

            _onlineService.PranamMessageReceived +=
                HandlePranamMessage;
        }

        /// <summary>
        /// EOSオンラインサービスのイベント購読を解除します。
        /// </summary>
        private void UnbindOnlineService()
        {
            if (_onlineService != null)
            {
                _onlineService.PranamMessageReceived -=
                    HandlePranamMessage;
            }

            _onlineService = null;
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
                $"{nameof(EosPranamNetwork)}: " +
                $"{nameof(PranamBehavior)}を取得できません。",
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
