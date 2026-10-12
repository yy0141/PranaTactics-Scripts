using System;
using System.Collections.Generic;

namespace PranaTactics.Unit
{
    /// <summary>
    /// チームのスコア、所属ユニットおよび
    /// チーム内で共有する視認情報を保持します。
    /// </summary>
    public sealed class TeamStatus
    {
        /// <summary>
        /// チームが防衛側かどうかです。
        /// </summary>
        private bool _isDefender;

        /// <summary>
        /// チームが獲得しているスコアです。
        /// </summary>
        private int _score;

        /// <summary>
        /// プレイヤーIDをキーとした所属ユニットの状態です。
        /// </summary>
        private readonly Dictionary<int, UnitStatus> _unitStatuses = new();

        /// <summary>
        /// チーム内で共有している視認対象のプレイヤーIDです。
        /// </summary>
        private readonly HashSet<int> _visiblePlayerIds = new();

        public bool IsDefender
        {
            get
            {
                return _isDefender;
            }
        }

        public int Score
        {
            get
            {
                return _score;
            }
        }

        public IReadOnlyDictionary<int, UnitStatus> UnitStatuses
        {
            get
            {
                return _unitStatuses;
            }
        }

        public IReadOnlyCollection<int> VisiblePlayerIds
        {
            get
            {
                return _visiblePlayerIds;
            }
        }

        public int UnitCount
        {
            get
            {
                return _unitStatuses.Count;
            }
        }

        public int VisiblePlayerCount
        {
            get
            {
                return _visiblePlayerIds.Count;
            }
        }

        public TeamStatus()
        {
        }

        public TeamStatus(
    bool isDefender)
        {
            _isDefender = isDefender;
        }

        /// <summary>
        /// チームが防衛側かどうかを設定します。
        /// </summary>
        /// <param name="isDefender">
        /// 防衛側に設定する場合は<c>true</c>です。
        /// </param>
        public void SetDefender(
            bool isDefender)
        {
            _isDefender = isDefender;
        }

        /// <summary>
        /// チームスコアへ指定された値を加算します。
        /// </summary>
        /// <param name="value">
        /// 加算する値です。負数を指定するとスコアが減少します。
        /// </param>
        /// <returns>
        /// 加算後のチームスコアです。
        /// </returns>
        public int AddScore(
            int value)
        {
            long calculatedScore =
                (long)_score +
                value;

            _score = (int)Math.Clamp(
                calculatedScore,
                0L,
                int.MaxValue);

            return _score;
        }

        /// <summary>
        /// チームスコアを指定された値へ設定します。
        /// </summary>
        /// <param name="score">
        /// 設定するスコアです。0以上へ補正されます。
        /// </param>
        public void SetScore(
            int score)
        {
            _score = Math.Max(
                0,
                score);
        }

        /// <summary>
        /// チームスコアを0へ戻します。
        /// </summary>
        public void ResetScore()
        {
            _score = 0;
        }

        /// <summary>
        /// チームへユニット状態を登録します。
        /// </summary>
        /// <param name="unitStatus">
        /// 登録するユニット状態です。
        /// </param>
        /// <returns>
        /// 新しく登録できた場合は<c>true</c>です。
        /// 同じプレイヤーIDが登録済みの場合は<c>false</c>です。
        /// </returns>
        public bool AddUnitStatus(
            UnitStatus unitStatus)
        {
            if (unitStatus == null)
            {
                return false;
            }

            return _unitStatuses.TryAdd(
                unitStatus.PlayerId,
                unitStatus);
        }

        /// <summary>
        /// 指定されたプレイヤーのユニット状態を更新します。
        /// 登録されていない場合は新しく追加します。
        /// </summary>
        /// <param name="unitStatus">
        /// 登録または更新するユニット状態です。
        /// </param>
        /// <returns>
        /// 設定できた場合は<c>true</c>です。
        /// </returns>
        public bool SetUnitStatus(
            UnitStatus unitStatus)
        {
            if (unitStatus == null)
            {
                return false;
            }

            _unitStatuses[unitStatus.PlayerId] =
                unitStatus;

            return true;
        }

        /// <summary>
        /// 指定されたプレイヤーのユニット状態を取得します。
        /// </summary>
        /// <param name="playerId">
        /// 取得するプレイヤーIDです。
        /// </param>
        /// <param name="unitStatus">
        /// 取得されたユニット状態です。
        /// </param>
        /// <returns>
        /// ユニット状態を取得できた場合は<c>true</c>です。
        /// </returns>
        public bool TryGetUnitStatus(
            int playerId,
            out UnitStatus unitStatus)
        {
            return _unitStatuses.TryGetValue(
                playerId,
                out unitStatus);
        }

        /// <summary>
        /// 指定されたプレイヤーのユニット状態を削除します。
        /// </summary>
        /// <param name="playerId">
        /// 削除するプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 登録されていたユニットを削除できた場合は<c>true</c>です。
        /// </returns>
        public bool RemoveUnitStatus(
            int playerId)
        {
            _visiblePlayerIds.Remove(
                playerId);

            return _unitStatuses.Remove(
                playerId);
        }

        /// <summary>
        /// 所属ユニットと視認情報をすべて削除します。
        /// </summary>
        public void ClearUnits()
        {
            _unitStatuses.Clear();
            _visiblePlayerIds.Clear();
        }

        /// <summary>
        /// 指定されたプレイヤーがこのチームに
        /// 所属しているかどうかを確認します。
        /// </summary>
        /// <param name="playerId">
        /// 確認するプレイヤーIDです。
        /// </param>
        /// <returns>
        /// チームに所属している場合は<c>true</c>です。
        /// </returns>
        public bool ContainsPlayer(
            int playerId)
        {
            return _unitStatuses.ContainsKey(
                playerId);
        }

        /// <summary>
        /// 視認対象のプレイヤーIDを追加します。
        /// </summary>
        /// <param name="playerId">
        /// 追加するプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 新しく追加された場合は<c>true</c>です。
        /// </returns>
        public bool AddVisiblePlayer(
            int playerId)
        {
            if (playerId < 0)
            {
                return false;
            }

            return _visiblePlayerIds.Add(
                playerId);
        }

        /// <summary>
        /// 視認対象から指定されたプレイヤーIDを削除します。
        /// </summary>
        /// <param name="playerId">
        /// 削除するプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 対象が存在し、削除された場合は<c>true</c>です。
        /// </returns>
        public bool RemoveVisiblePlayer(
            int playerId)
        {
            return _visiblePlayerIds.Remove(
                playerId);
        }

        /// <summary>
        /// 視認対象をすべて削除します。
        /// </summary>
        public void ClearVisiblePlayers()
        {
            _visiblePlayerIds.Clear();
        }

        /// <summary>
        /// 指定されたプレイヤーをチームが
        /// 現在視認しているかどうかを確認します。
        /// </summary>
        /// <param name="playerId">
        /// 確認するプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 視認している場合は<c>true</c>です。
        /// </returns>
        public bool ContainsVisiblePlayer(
            int playerId)
        {
            return _visiblePlayerIds.Contains(
                playerId);
        }

        /// <summary>
        /// チーム状態を確認しやすい文字列へ変換します。
        /// </summary>
        public override string ToString()
        {
            return
                $"{nameof(TeamStatus)}(" +
                $"{nameof(IsDefender)}={IsDefender}, " +
                $"{nameof(Score)}={Score}, " +
                $"{nameof(UnitCount)}={UnitCount}, " +
                $"{nameof(VisiblePlayerCount)}={VisiblePlayerCount})";
        }
    }
}
