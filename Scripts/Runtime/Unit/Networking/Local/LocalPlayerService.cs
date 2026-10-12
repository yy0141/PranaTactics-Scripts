using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.Unit.Networking.Local
{
    /// <summary>
    /// ローカルゲームで使用するプレイヤー情報を管理します。
    /// </summary>
    /// <remarks>
    /// ネットワーク通信を使用せず、メモリ上のプレイヤー情報を
    /// <see cref="Dictionary{TKey,TValue}"/>で管理します。
    /// </remarks>
    public sealed class LocalPlayerService : IPlayerService
    {
        /// <summary>
        /// 方向ベクトルが有効か判定するための最小値です。
        /// </summary>
        private const float k_DirectionEpsilon = 0.001f;

        /// <summary>
        /// プレイヤーIDをキーとしたプレイヤー情報です。
        /// </summary>
        private readonly Dictionary<int, PlayerInfo> _players = new();

        public int LocalPlayerId
        {
            get;
            private set;
        }

        public int PlayerCount
        {
            get
            {
                return _players.Count;
            }
        }

        /// <summary>
        /// ローカルプレイヤー情報サービスを初期化します。
        /// </summary>
        /// <param name="localPlayerId">
        /// ローカルプレイヤーのIDです。
        /// </param>
        /// <param name="players">
        /// 登録するプレイヤー情報の一覧です。
        /// </param>
        /// <param name="randomRotation">
        /// 各プレイヤーへランダムなY回転を設定する場合は
        /// <c>true</c>です。
        /// <c>false</c>の場合、ローカルプレイヤー以外を
        /// ローカルプレイヤーの方向へ向けます。
        /// </param>
        public void Initialize(
            int localPlayerId,
            List<PlayerInfo> players,
            bool randomRotation)
        {
            LocalPlayerId = localPlayerId;

            _players.Clear();

            RegisterPlayers(
                players);

            if (!_players.ContainsKey(
                    LocalPlayerId))
            {
                Debug.LogError(
                    $"{nameof(LocalPlayerService)}: " +
                    $"LocalPlayerId={LocalPlayerId}のプレイヤー情報が" +
                    "登録されていません。");

                return;
            }

            if (randomRotation)
            {
                SetRandomRotations();

                return;
            }

            SetLookAtRotations();
        }

        /// <summary>
        /// 指定されたプレイヤーをローカルプレイヤーの方向へ向けます。
        /// </summary>
        /// <param name="playerId">
        /// 回転を計算するプレイヤーIDです。
        /// </param>
        public void CalculateLookAtRotation(
            int playerId)
        {
            if (playerId == LocalPlayerId)
            {
                return;
            }

            if (!_players.TryGetValue(
                    LocalPlayerId,
                    out PlayerInfo localPlayer))
            {
                Debug.LogWarning(
                    $"{nameof(LocalPlayerService)}: " +
                    $"LocalPlayerId={LocalPlayerId}が登録されていないため、" +
                    "回転を計算できません。");

                return;
            }

            if (!_players.TryGetValue(
                    playerId,
                    out PlayerInfo targetPlayer))
            {
                Debug.LogWarning(
                    $"{nameof(LocalPlayerService)}: " +
                    $"PlayerId={playerId}が登録されていないため、" +
                    "回転を計算できません。");

                return;
            }

            Vector3 direction =
                new(
                    localPlayer.Coordinate.x -
                    targetPlayer.Coordinate.x,
                    0.0f,
                    localPlayer.Coordinate.y -
                    targetPlayer.Coordinate.y);

            if (direction.sqrMagnitude <
                k_DirectionEpsilon)
            {
                return;
            }

            float yaw =
                Quaternion.LookRotation(
                    direction,
                    Vector3.up).eulerAngles.y;

            targetPlayer.Rotation =
                new Vector3(
                    0.0f,
                    yaw,
                    0.0f);
        }

        /// <summary>
        /// 指定されたプレイヤーのチームIDを取得します。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のプレイヤーIDです。
        /// </param>
        /// <returns>
        /// プレイヤーが所属するチームIDです。
        /// </returns>
        public int GetTeamId(
            int playerId)
        {
            return GetRequiredPlayer(
                playerId).TeamId;
        }

        /// <summary>
        /// 指定されたプレイヤーのキャラクター名を取得します。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のプレイヤーIDです。
        /// </param>
        /// <returns>
        /// プレイヤーが使用するキャラクター名です。
        /// </returns>
        public string GetCharacterName(
            int playerId)
        {
            return GetRequiredPlayer(
                playerId).CharacterName;
        }

        /// <summary>
        /// 指定されたプレイヤーが使用するキャラクターを変更します。
        /// </summary>
        /// <param name="playerId">
        /// 変更対象のプレイヤーIDです。
        /// </param>
        /// <param name="characterName">
        /// 新しく使用するキャラクター名です。
        /// </param>
        public void ChangeCharacter(
            int playerId,
            string characterName)
        {
            PlayerInfo player =
                GetRequiredPlayer(
                    playerId);

            player.CharacterName =
                characterName ??
                string.Empty;
        }

        /// <summary>
        /// 指定されたプレイヤーIDへプレイヤー情報を設定します。
        /// </summary>
        /// <param name="playerId">
        /// 登録先のプレイヤーIDです。
        /// </param>
        /// <param name="player">
        /// 登録するプレイヤー情報です。
        /// </param>
        public void SetCharacterInfo(
            int playerId,
            PlayerInfo player)
        {
            if (player == null)
            {
                Debug.LogError(
                    $"{nameof(LocalPlayerService)}: " +
                    "登録するPlayerInfoがnullです。");

                return;
            }

            player.PlayerId =
                playerId;

            player.CharacterName ??=
                string.Empty;

            _players[playerId] =
                player;
        }

        /// <summary>
        /// 指定されたプレイヤー情報を取得します。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 登録されているプレイヤー情報です。
        /// </returns>
        public PlayerInfo GetPlayerInfo(
            int playerId)
        {
            return GetRequiredPlayer(
                playerId);
        }

        /// <summary>
        /// 指定されたプレイヤー情報の取得を試みます。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のプレイヤーIDです。
        /// </param>
        /// <param name="player">
        /// 取得されたプレイヤー情報です。
        /// </param>
        /// <returns>
        /// プレイヤー情報を取得できた場合は<c>true</c>です。
        /// </returns>
        public bool TryGetPlayerInfo(
            int playerId,
            out PlayerInfo player)
        {
            return _players.TryGetValue(
                playerId,
                out player);
        }

        /// <summary>
        /// 指定されたプレイヤーが登録されているか確認します。
        /// </summary>
        /// <param name="playerId">
        /// 確認するプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 登録されている場合は<c>true</c>です。
        /// </returns>
        public bool ContainsPlayer(
            int playerId)
        {
            return _players.ContainsKey(
                playerId);
        }

        /// <summary>
        /// プレイヤー一覧をDictionaryへ登録します。
        /// </summary>
        /// <param name="players">
        /// 登録するプレイヤー一覧です。
        /// </param>
        private void RegisterPlayers(
            IEnumerable<PlayerInfo> players)
        {
            if (players == null)
            {
                Debug.LogError(
                    $"{nameof(LocalPlayerService)}: " +
                    "プレイヤー一覧がnullです。");

                return;
            }

            foreach (PlayerInfo player in
                    players)
            {
                if (player == null)
                {
                    Debug.LogWarning(
                        $"{nameof(LocalPlayerService)}: " +
                        "nullのPlayerInfoを無視しました。");

                    continue;
                }

                player.CharacterName ??=
                    string.Empty;

                if (_players.ContainsKey(
                        player.PlayerId))
                {
                    Debug.LogWarning(
                        $"{nameof(LocalPlayerService)}: " +
                        $"PlayerId={player.PlayerId}が重複しています。" +
                        "後から登録された情報で上書きします。");
                }

                _players[player.PlayerId] =
                    player;
            }
        }

        /// <summary>
        /// ローカルプレイヤー以外を
        /// ローカルプレイヤーの方向へ向けます。
        /// </summary>
        private void SetLookAtRotations()
        {
            foreach (int playerId in
                    _players.Keys)
            {
                CalculateLookAtRotation(
                    playerId);
            }
        }

        /// <summary>
        /// 全プレイヤーへランダムなY回転を設定します。
        /// </summary>
        private void SetRandomRotations()
        {
            foreach (PlayerInfo player in
                    _players.Values)
            {
                float randomYaw =
                    Random.Range(
                        0.0f,
                        360.0f);

                player.Rotation =
                    new Vector3(
                        0.0f,
                        randomYaw,
                        0.0f);
            }
        }

        /// <summary>
        /// 指定されたプレイヤー情報を取得します。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 登録されているプレイヤー情報です。
        /// </returns>
        /// <exception cref="KeyNotFoundException">
        /// 指定されたプレイヤーが登録されていない場合に発生します。
        /// </exception>
        private PlayerInfo GetRequiredPlayer(
            int playerId)
        {
            if (_players.TryGetValue(
                    playerId,
                    out PlayerInfo player))
            {
                return player;
            }

            throw new KeyNotFoundException(
                $"{nameof(LocalPlayerService)}に" +
                $"PlayerId={playerId}は登録されていません。");
        }
    }
}
