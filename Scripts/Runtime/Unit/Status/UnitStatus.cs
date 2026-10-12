using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.Unit
{
    /// <summary>
    /// ユニットの識別情報、能力値、移動状態、
    /// 移動経路および視認情報を保持します。
    /// </summary>
    public sealed class UnitStatus
    {
        /// <summary>
        /// ユニットが所属するチームIDです。
        /// </summary>
        private readonly int _teamId;

        /// <summary>
        /// ユニットを所有するプレイヤーIDです。
        /// </summary>
        private readonly int _playerId;

        /// <summary>
        /// ユニットの内部名です。
        /// </summary>
        private readonly string _unitName;

        /// <summary>
        /// キャラクター固有の設定データです。
        /// </summary>
        private readonly UnitParams _characterData;

        /// <summary>
        /// ユニットが撃破されているかどうかです。
        /// </summary>
        private bool _isDead;

        /// <summary>
        /// ユニットが現在視認されているかどうかです。
        /// </summary>
        private bool _isVisible;

        /// <summary>
        /// フェイズ開始時に設定する行動ポイントです。
        /// </summary>
        private int _actionPoint;

        /// <summary>
        /// ユニットの現在HPです。
        /// </summary>
        private float _currentHealth;

        /// <summary>
        /// ユニットの最大HPです。
        /// </summary>
        private float _maxHealth;

        /// <summary>
        /// ユニットの現在の積極性です。
        /// </summary>
        private int _proactiveness;

        /// <summary>
        /// 1マス移動するときに消費する積極性です。
        /// </summary>
        private int _moveProactivenessCost;

        /// <summary>
        /// Macro Phaseで設定された移動経路です。
        /// </summary>
        private readonly List<Vector2Int> _migrationPath = new();

        /// <summary>
        /// Micro Phase開始時に計画されていた移動ステップ数です。
        /// </summary>
        private int _plannedMigrationStepCount;

        /// <summary>
        /// 現在のMicro Phaseで完了した移動ステップ数です。
        /// </summary>
        private int _completedMigrationStepCount;

        /// <summary>
        /// Macroで確定した経路をMicroで実行中かどうかです。
        /// </summary>
        private bool _isMigrationExecutionActive;

        /// <summary>
        /// 最後に正常到達した移動経路上の座標です。
        /// </summary>
        private Vector2Int _lastReachedMigrationCoordinate;

        /// <summary>
        /// 正常到達した座標を1つ以上保持しているかどうかです。
        /// </summary>
        private bool _hasLastReachedMigrationCoordinate;

        /// <summary>
        /// ユニットが現在移動中かどうかです。
        /// </summary>
        private bool _isMovable;

        /// <summary>
        /// ユニットの基本移動速度です。
        /// </summary>
        private float _moveSpeed;

        /// <summary>
        /// 現在適用されている移動速度です。
        /// </summary>
        private float _currentMoveSpeed;

        /// <summary>
        /// ダッシュ中の移動速度です。
        /// </summary>
        private float _dashMoveSpeed;

        /// <summary>
        /// 歩行中の移動速度です。
        /// </summary>
        private float _walkMoveSpeed;

        /// <summary>
        /// 現在このユニットから視認しているプレイヤーIDです。
        /// </summary>
        private readonly HashSet<int> _visiblePlayers = new();

        public int TeamId => _teamId;

        public int PlayerId => _playerId;

        public bool IsDead => _isDead;

        public bool IsAlive => !_isDead && _currentHealth > 0.0f;

        public bool IsVisible => _isVisible;

        public string UnitName => _unitName;

        public int ActionPoint => _actionPoint;

        public float CurrentHealth => _currentHealth;

        public float MaxHealth => _maxHealth;

        public float HealthRate
        {
            get
            {
                if (_maxHealth <= 0.0f)
                {
                    return 0.0f;
                }

                return Mathf.Clamp01(
                    _currentHealth / _maxHealth);
            }
        }

        public int Proactiveness => _proactiveness;

        public int MoveProactivenessCost => _moveProactivenessCost;

        public IReadOnlyList<Vector2Int> MigrationPath => _migrationPath;

        public bool HasMigrationPath => _migrationPath.Count > 0;

        public int PlannedMigrationStepCount => _plannedMigrationStepCount;

        public int CompletedMigrationStepCount => _completedMigrationStepCount;

        public bool IsMigrationExecutionActive => _isMigrationExecutionActive;

        public bool HasLastReachedMigrationCoordinate =>
    _hasLastReachedMigrationCoordinate;

        public Vector2Int LastReachedMigrationCoordinate =>
    _lastReachedMigrationCoordinate;

        public float MigrationProgress
        {
            get
            {
                if (_plannedMigrationStepCount <= 0)
                {
                    return 0.0f;
                }

                return Mathf.Clamp01(
                    (float)_completedMigrationStepCount /
                    _plannedMigrationStepCount);
            }
        }

        public bool IsMovable => _isMovable;

        public float MoveSpeed => _moveSpeed;

        public float CurrentMoveSpeed => _currentMoveSpeed;

        public float DashMoveSpeed => _dashMoveSpeed;

        public float WalkMoveSpeed => _walkMoveSpeed;

        public IReadOnlyCollection<int> VisiblePlayers => _visiblePlayers;

        public UnitParams CharacterData => _characterData;

        public UnitStatus(
    int playerId,
    int teamId,
    string unitName,
    int actionPoint,
    float maxHealth,
    float moveSpeed,
    float dashMoveSpeed,
    float walkMoveSpeed,
    int moveProactivenessCost,
    UnitParams characterData)
        {
            if (characterData == null)
            {
                throw new ArgumentNullException(
                    nameof(characterData));
            }

            _playerId = playerId;
            _teamId = teamId;
            _unitName = unitName ?? string.Empty;
            _characterData = characterData;

            _actionPoint = Mathf.Max(
                0,
                actionPoint);

            _maxHealth = Mathf.Max(
                0.0f,
                maxHealth);

            _currentHealth = _maxHealth;

            _proactiveness = Mathf.Max(
                0,
                characterData.InitialProactiveness);

            _moveProactivenessCost = Mathf.Max(
                0,
                moveProactivenessCost);

            _moveSpeed = Mathf.Max(
                0.0f,
                moveSpeed);

            _currentMoveSpeed = _moveSpeed;

            _dashMoveSpeed = Mathf.Max(
                0.0f,
                dashMoveSpeed);

            _walkMoveSpeed = Mathf.Max(
                0.0f,
                walkMoveSpeed);
        }

        /// <summary>
        /// ユニットの撃破状態を設定します。
        /// </summary>
        public void SetDead(
            bool isDead)
        {
            _isDead = isDead;
        }

        /// <summary>
        /// ユニットの視認状態を設定します。
        /// </summary>
        public void SetVisible(
            bool isVisible)
        {
            _isVisible = isVisible;
        }

        /// <summary>
        /// フェイズ開始時の行動ポイントを設定します。
        /// </summary>
        public void SetActionPoint(
            int actionPoint)
        {
            _actionPoint = Mathf.Max(
                0,
                actionPoint);
        }

        /// <summary>
        /// ユニットの現在HPを設定します。
        /// </summary>
        /// <param name="health">
        /// 設定するHPです。0から最大HPの範囲へ補正されます。
        /// </param>
        public void SetHealth(
            float health)
        {
            _currentHealth = Mathf.Clamp(
                health,
                0.0f,
                _maxHealth);
        }

        /// <summary>
        /// ユニットの最大HPを設定します。
        /// </summary>
        /// <param name="maxHealth">
        /// 設定する最大HPです。
        /// </param>
        /// <param name="restoreHealth">
        /// 現在HPも最大値まで回復する場合は<c>true</c>です。
        /// </param>
        public void SetMaxHealth(
            float maxHealth,
            bool restoreHealth = false)
        {
            _maxHealth = Mathf.Max(
                0.0f,
                maxHealth);

            if (restoreHealth)
            {
                _currentHealth = _maxHealth;

                return;
            }

            _currentHealth = Mathf.Min(
                _currentHealth,
                _maxHealth);
        }

        /// <summary>
        /// ユニットの現在HPを指定量だけ減少させます。
        /// </summary>
        /// <param name="damage">
        /// 適用するダメージ量です。
        /// </param>
        /// <returns>
        /// ダメージ適用後の現在HPです。
        /// </returns>
        public float ApplyDamage(
            float damage)
        {
            if (damage <= 0.0f)
            {
                return _currentHealth;
            }

            SetHealth(
                _currentHealth - damage);

            return _currentHealth;
        }

        /// <summary>
        /// ユニットの現在HPを指定量だけ回復します。
        /// </summary>
        /// <param name="amount">
        /// 回復するHP量です。
        /// </param>
        /// <returns>
        /// 回復後の現在HPです。
        /// </returns>
        public float RestoreHealth(
            float amount)
        {
            if (amount <= 0.0f)
            {
                return _currentHealth;
            }

            SetHealth(
                _currentHealth + amount);

            return _currentHealth;
        }

        /// <summary>
        /// 現在の積極性を設定します。
        /// </summary>
        public void SetProactiveness(
            int proactiveness)
        {
            _proactiveness = Mathf.Max(
                0,
                proactiveness);
        }

        /// <summary>
        /// 現在の積極性を指定量だけ消費します。
        /// </summary>
        /// <param name="cost">
        /// 消費する積極性です。
        /// </param>
        /// <returns>
        /// 必要な積極性を消費できた場合は<c>true</c>です。
        /// </returns>
        public bool TryConsumeProactiveness(
            int cost)
        {
            int validCost = Mathf.Max(
                0,
                cost);

            if (!CanAffordProactiveness(validCost))
            {
                return false;
            }

            _proactiveness -= validCost;

            return true;
        }

        /// <summary>
        /// 1マス移動するときに消費する積極性を設定します。
        /// </summary>
        public void SetMoveProactivenessCost(
            int cost)
        {
            _moveProactivenessCost = Mathf.Max(
                0,
                cost);
        }

        /// <summary>
        /// 移動経路を指定された座標一覧へ置き換えます。
        /// </summary>
        /// <param name="coordinates">
        /// 新しく設定する移動経路です。
        /// <c>null</c>の場合は経路を空にします。
        /// </param>
        public void SetMigrationPath(
            IEnumerable<Vector2Int> coordinates)
        {
            if (coordinates == null)
            {
                _migrationPath.Clear();
                ResetMigrationExecutionState();

                return;
            }

            List<Vector2Int> copiedCoordinates = new(
                coordinates);

            _migrationPath.Clear();
            _migrationPath.AddRange(
                copiedCoordinates);

            ResetMigrationExecutionState();

            _plannedMigrationStepCount =
                _migrationPath.Count;
        }

        /// <summary>
        /// 移動経路の末尾へ座標を追加します。
        /// </summary>
        public void AddMigrationCoordinate(
            Vector2Int coordinate)
        {
            _migrationPath.Add(
                coordinate);

            if (!_isMigrationExecutionActive)
            {
                if (_migrationPath.Count == 1)
                {
                    _completedMigrationStepCount = 0;
                    _hasLastReachedMigrationCoordinate = false;
                    _lastReachedMigrationCoordinate = default;
                }

                _plannedMigrationStepCount =
                    _migrationPath.Count;
            }
        }

        /// <summary>
        /// 移動経路の先頭座標を取得します。
        /// </summary>
        /// <param name="coordinate">
        /// 経路の先頭座標です。
        /// </param>
        /// <returns>
        /// 移動経路が存在する場合は<c>true</c>です。
        /// </returns>
        public bool TryGetNextMigrationCoordinate(
            out Vector2Int coordinate)
        {
            if (_migrationPath.Count == 0)
            {
                coordinate = default;

                return false;
            }

            coordinate = _migrationPath[0];

            return true;
        }

        /// <summary>
        /// 指定された位置の移動経路を削除します。
        /// </summary>
        /// <param name="index">
        /// 削除する要素番号です。
        /// </param>
        /// <returns>
        /// 要素を削除できた場合は<c>true</c>です。
        /// </returns>
        public bool RemoveMigrationCoordinateAt(
            int index)
        {
            if (index < 0 ||
                index >= _migrationPath.Count)
            {
                return false;
            }

            _migrationPath.RemoveAt(
                index);

            return true;
        }

        /// <summary>
        /// 移動経路の先頭座標を削除します。
        /// </summary>
        /// <returns>
        /// 座標を削除できた場合は<c>true</c>です。
        /// </returns>
        public bool RemoveNextMigrationCoordinate()
        {
            if (_migrationPath.Count == 0)
            {
                return false;
            }

            Vector2Int reachedCoordinate =
                _migrationPath[0];

            _migrationPath.RemoveAt(0);

            if (_isMigrationExecutionActive)
            {
                _completedMigrationStepCount++;
                _lastReachedMigrationCoordinate =
                    reachedCoordinate;
                _hasLastReachedMigrationCoordinate = true;
            }

            return true;
        }

        ///
        /// <summary>フェーズ開始時の上限ではなく、現在残っている行動力で判定します。
        /// </summary>
        public bool CanAffordProactiveness(int cost)
        {
            return _proactiveness >= Mathf.Max(0, cost);
        }

        /// <summary>
        /// 設定されている移動経路をすべて削除します。
        /// </summary>
        public void ClearMigrationPath()
        {
            _migrationPath.Clear();
            ResetMigrationExecutionState();
        }

        /// <summary>
        /// Macroで確定した経路のMicro実行を開始します。
        /// 移動開始時の行動力消費はMicroUnitBehaviorが行います。
        /// </summary>
        public void BeginMigrationExecution()
        {
            _plannedMigrationStepCount =
                _migrationPath.Count;

            _completedMigrationStepCount = 0;
            _hasLastReachedMigrationCoordinate = false;
            _lastReachedMigrationCoordinate = default;
            _isMigrationExecutionActive =
                _migrationPath.Count > 0;
        }

        /// <summary>
        /// Microでの経路実行を終了します。
        /// 未到達経路は次のMacroで再計画するため，必要に応じて破棄します。
        /// </summary>
        public void EndMigrationExecution(
            bool discardRemainingPath)
        {
            _isMigrationExecutionActive = false;

            if (discardRemainingPath)
            {
                _migrationPath.Clear();
            }
        }

        /// <summary>
        /// ユニットの移動状態を設定します。
        /// </summary>
        public void SetMovable(
            bool isMovable)
        {
            _isMovable = isMovable;
        }

        /// <summary>
        /// ユニットの基本移動速度を設定します。
        /// </summary>
        public void SetMoveSpeed(
            float moveSpeed)
        {
            _moveSpeed = Mathf.Max(
                0.0f,
                moveSpeed);
        }

        /// <summary>
        /// 現在適用されている移動速度を設定します。
        /// </summary>
        public void SetCurrentMoveSpeed(
            float moveSpeed)
        {
            _currentMoveSpeed = Mathf.Max(
                0.0f,
                moveSpeed);
        }

        /// <summary>
        /// ダッシュ中の移動速度を設定します。
        /// </summary>
        public void SetDashMoveSpeed(
            float moveSpeed)
        {
            _dashMoveSpeed = Mathf.Max(
                0.0f,
                moveSpeed);
        }

        /// <summary>
        /// 歩行中の移動速度を設定します。
        /// </summary>
        public void SetWalkMoveSpeed(
            float moveSpeed)
        {
            _walkMoveSpeed = Mathf.Max(
                0.0f,
                moveSpeed);
        }

        /// <summary>
        /// 視認中プレイヤー一覧へプレイヤーIDを追加します。
        /// </summary>
        /// <returns>
        /// 新たに追加された場合は<c>true</c>です。
        /// </returns>
        public bool AddVisiblePlayer(
            int playerId)
        {
            if (playerId < 0)
            {
                return false;
            }

            return _visiblePlayers.Add(
                playerId);
        }

        /// <summary>
        /// 視認中プレイヤー一覧からプレイヤーIDを削除します。
        /// </summary>
        /// <returns>
        /// 対象が存在し、削除された場合は<c>true</c>です。
        /// </returns>
        public bool RemoveVisiblePlayer(
            int playerId)
        {
            return _visiblePlayers.Remove(
                playerId);
        }

        /// <summary>
        /// 視認中プレイヤー一覧をすべて削除します。
        /// </summary>
        public void ClearVisiblePlayers()
        {
            _visiblePlayers.Clear();
        }

        /// <summary>
        /// 指定されたプレイヤーを現在視認しているか確認します。
        /// </summary>
        public bool ContainsVisiblePlayer(
            int playerId)
        {
            return _visiblePlayers.Contains(
                playerId);
        }

        /// <summary>
        /// ユニット状態を確認しやすい文字列へ変換します。
        /// </summary>
        public override string ToString()
        {
            return
                $"{nameof(UnitStatus)}(" +
                $"{nameof(PlayerId)}={PlayerId}, " +
                $"{nameof(TeamId)}={TeamId}, " +
                $"{nameof(UnitName)}={UnitName}, " +
                $"{nameof(CurrentHealth)}={CurrentHealth}/{MaxHealth}, " +
                $"{nameof(Proactiveness)}={Proactiveness}, " +
                $"{nameof(IsDead)}={IsDead}, " +
                $"{nameof(IsVisible)}={IsVisible}, " +
                $"{nameof(IsMovable)}={IsMovable}, " +
                $"Migration={CompletedMigrationStepCount}/" +
                $"{PlannedMigrationStepCount})";
        }

        /// <summary>
        /// 計画経路を置き換える際に実行状態を初期化します。
        /// </summary>
        private void ResetMigrationExecutionState()
        {
            _plannedMigrationStepCount = 0;
            _completedMigrationStepCount = 0;
            _isMigrationExecutionActive = false;
            _lastReachedMigrationCoordinate = default;
            _hasLastReachedMigrationCoordinate = false;
        }
    }
}
