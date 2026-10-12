namespace PranaTactics.Networking
{
    /// <summary>
    /// オンラインルームおよびオンラインプレイヤーの
    /// カスタムプロパティで使用するキーと値を定義します。
    /// </summary>
    public static class NetworkPropertyKeys
    {
        /// <summary>
        /// ホストが選択した今回のチーム数です。
        /// </summary>
        public const string MatchingPartyCount = "matchingPartyCount";

        /// <summary>
        /// ホストが選択した今回のチーム人数です。
        /// </summary>
        public const string MatchingPartySize = "matchingPartySize";

        /// <summary>
        /// パーティー全員が開始を受信し、移動を開始できる状態です。
        /// </summary>
        public const string PartyDeparting = "partyDeparting";

        /// <summary>
        /// マクロフェイズの今回の計画を識別するルーム属性です。
        /// </summary>
        public const string MacroPlanningRound = "macroPlanningRound";

        /// <summary>
        /// プレイヤーが決定した計画の識別子を保持するメンバー属性です。
        /// </summary>
        public const string MacroPlanningReady = "macroPlanningReady";

        /// <summary>
        /// ルームの用途を表すプロパティのキーです。
        /// </summary>
        public const string Type = "type";

        /// <summary>
        /// 試合とパーティが選択したゲームルールの安定した識別子です。
        /// </summary>
        public const string GameModeId = "gameModeId";

        /// <summary>
        /// ルームの現在状態を表すプロパティのキーです。
        /// </summary>
        public const string State = "state";

        /// <summary>
        /// プレイヤーが所属するパーティの識別子を表す
        /// プロパティのキーです。
        /// </summary>
        public const string PartyId = "partyId";

        /// <summary>
        /// マッチング開始時のパーティ人数です。未到着メンバーの席も確保します。
        /// </summary>
        public const string MatchingPartyMemberCount = "matchingPartyMemberCount";

        /// <summary>
        /// プレイヤーまたはルームに関連付けられた
        /// ルーム識別子を表すプロパティのキーです。
        /// </summary>
        public const string RoomId = "roomId";

        /// <summary>
        /// 非公開ルームへ参加するためのパスワードを表す
        /// プロパティのキーです。
        /// </summary>
        public const string Password = "password";

        /// <summary>
        /// 同じマッチへ割り当てられたプレイヤーまたはパーティを
        /// 識別するグループIDのプロパティキーです。
        /// </summary>
        public const string MatchGroupId = "matchGroupId";

        /// <summary>
        /// キャラクター選択などの準備完了状態を表す
        /// プロパティのキーです。
        /// </summary>
        public const string Ready = "ready";

        /// <summary>
        /// 旧処理で使用していた準備完了状態の
        /// プロパティキーです。
        /// </summary>
        /// <remarks>
        /// 新しい処理では<see cref="Ready"/>を使用します。
        /// 既存データおよび旧処理との互換性維持のために残されています。
        /// </remarks>
        public const string IsReady = "isReady";

        /// <summary>
        /// 旧処理で使用していたゲーム開始可能状態の
        /// プロパティキーです。
        /// </summary>
        /// <remarks>
        /// 新しいキャラクター選択処理では
        /// <see cref="Ready"/>を使用します。
        /// </remarks>
        public const string CanStartGame = "canStartGame";

        /// <summary>
        /// マッチング開始の準備完了状態を表す
        /// プロパティのキーです。
        /// </summary>
        public const string MatchingReady = "matchingReady";

        /// <summary>
        /// パーティ単位のマッチングを行っているかどうかを表す
        /// プロパティのキーです。
        /// </summary>
        public const string PartyMatching = "partyMatching";

        /// <summary>
        /// マッチングがキャンセルされたかどうかを表す
        /// プロパティのキーです。
        /// </summary>
        public const string MatchCanceled = "matchCanceled";

        /// <summary>
        /// プレイヤーが所属するチームの識別子を表す
        /// プロパティのキーです。
        /// </summary>
        public const string TeamId = "teamId";

        /// <summary>
        /// プレイヤーが選択したキャラクター名を表す
        /// プロパティのキーです。
        /// </summary>
        public const string CharacterName = "characterName";

        /// <summary>
        /// パーティルームであることを表す
        /// <see cref="Type"/>プロパティの値です。
        /// </summary>
        public const string TypeParty = "party";

        /// <summary>
        /// マッチングルームであることを表す
        /// <see cref="Type"/>プロパティの値です。
        /// </summary>
        public const string TypeMatching = "matching";

        /// <summary>
        /// ルームがプレイヤーの参加を待機していることを表す
        /// <see cref="State"/>プロパティの値です。
        /// </summary>
        public const string StateWaiting = "waiting";

        /// <summary>
        /// ルームがマッチング処理中であることを表す
        /// <see cref="State"/>プロパティの値です。
        /// </summary>
        public const string StateMatching = "matching";

        /// <summary>
        /// ルームで試合が開始されていることを表す
        /// <see cref="State"/>プロパティの値です。
        /// </summary>
        public const string StatePlaying = "playing";

        /// <summary>
        /// 試合終了後のチーム別帰還先ルーム情報が
        /// 生成済みかどうかを表すプロパティのキーです。
        /// </summary>
        public const string ReturnRoomReady = "returnRoomReady";
    }
}
