using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Board.Networking;
using PranaTactics.Match.Phases;
using PranaTactics.Networking;
using PranaTactics.Prana;
using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.Board
{
    /// <summary>
    /// ボードに属する罠の配置、敵進入の裁定、発動遅延と味方向け表示を管理します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoardManager))]
    public sealed class BoardTrapManager : MonoBehaviour
    {
        /// <summary>
        /// 効果の種類を知らずに配置と検知を管理するレジストリです。
        /// </summary>
        private readonly BoardTrapRegistry _registry = new();

        /// <summary>
        /// ボードが所有する効果オブジェクトです。
        /// </summary>
        private readonly Dictionary<string, TrapperPranaBase> _pranas = new();

        /// <summary>
        /// 検知した敵と発動するゲーム時刻です。
        /// </summary>
        private readonly BoardTrapActivationQueue _pending = new();

        /// <summary>
        /// 受理済み要求および発動済みIDの再適用を防ぎます。
        /// </summary>
        private readonly HashSet<string> _processed = new();

        /// <summary>
        /// 現在表示している設置マスと起動範囲です。
        /// </summary>
        private readonly HashSet<Vector2Int> _highlightedCells = new();

        /// <summary>
        /// 重なる範囲を設置マス優先で合成する作業領域です。
        /// </summary>
        private readonly Dictionary<Vector2Int, (bool IsPlacement, Color Color)> _highlights = new();

        /// <summary>
        /// 所属するボードです。
        /// </summary>
        private BoardManager _board;

        /// <summary>
        /// 罠の通信アダプターです。
        /// </summary>
        private BoardTrapNetwork _network;

        /// <summary>
        /// ユニットを配置しないローカル確認シーンの観測チームです。
        /// </summary>
        private int? _previewObserverTeam;

        /// <summary>
        /// ローカル確認シーンで、設置者を生成せずに兵装データから罠を設置します。
        /// </summary>
        /// <remarks>
        /// 通常の兵装使用経路とは独立した確認用APIです。対戦中には使用できません。
        /// </remarks>
        internal bool TryPlacePreviewTrap(TrapperPranamData data, Vector2Int coordinate, int ownerTeamId)
        {
            if (data == null || ownerTeamId < 0 || !CanPlaceAt(coordinate) ||
                NetworkServiceLocator.Online?.IsInRoom == true || data.PranamPrefab == null ||
                string.IsNullOrWhiteSpace(data.PrefabResourcePath) ||
                data.PranamPrefab.GetComponent<TrapperPranaBase>() == null ||
                Resources.Load<GameObject>(data.PrefabResourcePath) != data.PranamPrefab)
            {
                return false;
            }

            _previewObserverTeam = ownerTeamId;
            var placement = new BoardTrapMessage
            {
                Command = BoardTrapCommand.Placed,
                TrapId = Guid.NewGuid().ToString("N"),
                OwnerId = 0,
                OwnerTeamId = ownerTeamId,
                X = coordinate.x,
                Y = coordinate.y,
                Floor = _board.GetMicroPhaseHexCell(coordinate.x, coordinate.y).GetFloor(),
                Range = data.TriggerRange,
                ActivationDelay = data.ActivationDelay,
                PrefabPath = data.PrefabResourcePath,
                SoundProfileId = data.SoundProfile != null ? data.SoundProfile.SoundId : string.Empty
            };
            ApplyPlacement(placement, data);
            return _registry.Contains(coordinate);
        }

        /// <summary>
        /// ボードと罠専用の通信アダプターを取得します。
        /// </summary>
        private void Awake()
        {
            _board = GetComponent<BoardManager>();
            _network = GetComponent<BoardTrapNetwork>();
            if (_network == null)
            {
                _network = gameObject.AddComponent<BoardTrapNetwork>();
            }
        }

        /// <summary>
        /// 有効なマスで、同じマスに別の罠がない場合に設置を許可します。
        /// </summary>
        public bool CanPlaceAt(Vector2Int coordinate)
        {
            return _board.GetMicroPhaseHexCell(coordinate.x, coordinate.y) != null && !_registry.Contains(coordinate);
        }

        /// <summary>
        /// 所有者からホストへ設置を要求します。配置とコストは受理後に適用します。
        /// </summary>
        public bool RequestPlacement(TrapperBase weapon, int ownerId, Vector2Int coordinate)
        {
            return _network.RequestPlacement(new BoardTrapMessage
            {
                Command = BoardTrapCommand.RequestPlacement,
                TrapId = Guid.NewGuid().ToString("N"),
                OwnerId = ownerId,
                IsMain = weapon.IsMain,
                Slot = weapon.Number,
                X = coordinate.x,
                Y = coordinate.y
            });
        }

        /// <summary>
        /// ホスト上の兵装データ、現在地とコストを用いて設置を一度だけ裁定します。
        /// </summary>
        internal void HandlePlacementRequest(BoardTrapMessage request, int senderId)
        {
            if (!_network.HasAuthority || request == null || string.IsNullOrWhiteSpace(request.TrapId) ||
                _processed.Contains(request.TrapId))
            {
                return;
            }

            TrapperBase weapon = FindWeapon(request);
            UnitManager units = UnitManager.Instance;
            UnitBehavior unit = units != null && units.TryGetUnit(request.OwnerId, out UnitBehavior found) ? found : null;
            if (unit == null || !_network.CanControlUnit(unit, senderId))
            {
                return;
            }

            if (weapon == null || !weapon.CanPlaceTrap() ||
                unit.GetMicroUnitBehavior().GetCurrentCoordinate() != new Vector2Int(request.X, request.Y))
            {
                RejectPlacement(request);
                return;
            }

            TrapperPranamData data = weapon.TrapData;
            GameObject prefab = data.PranamPrefab;
            if (prefab == null || string.IsNullOrWhiteSpace(data.PrefabResourcePath) ||
                prefab.GetComponent<TrapperPranaBase>() == null || unit.GetTeamId() < 0 ||
                Resources.Load<GameObject>(data.PrefabResourcePath) != prefab ||
                float.IsNaN(data.ActivationDelay) || float.IsInfinity(data.ActivationDelay) ||
                float.IsNaN(data.RecoveryTime) || float.IsInfinity(data.RecoveryTime))
            {
                Debug.LogError("TrapperのPrana PrefabにはTrapperPranaBase派生コンポーネントとResourcesパスが必要です。", data);
                RejectPlacement(request);
                return;
            }

            request.Command = BoardTrapCommand.Placed;
            request.OwnerTeamId = unit.GetTeamId();
            request.Range = data.TriggerRange;
            request.Floor = _board.GetMicroPhaseHexCell(request.X, request.Y).GetFloor();
            request.ActivationDelay = data.ActivationDelay;
            request.PrefabPath = data.PrefabResourcePath;
            request.SoundProfileId = data.SoundProfile != null ? data.SoundProfile.SoundId : string.Empty;
            _network.Broadcast(request);
        }

        /// <summary>
        /// 設置失敗を確定し、待機状態だけを解除します。
        /// </summary>
        private void RejectPlacement(BoardTrapMessage request)
        {
            request.Command = BoardTrapCommand.Rejected;
            _network.Broadcast(request);
        }

        /// <summary>
        /// ホストが確定した結果を適用します。受信処理から再送信は行いません。
        /// </summary>
        internal void ApplyMessage(BoardTrapMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.TrapId))
            {
                return;
            }

            switch (message.Command)
            {
                case BoardTrapCommand.Placed:
                    ApplyPlacement(message);
                    break;
                case BoardTrapCommand.Rejected:
                    if (_processed.Add(message.TrapId))
                    {
                        FindWeapon(message)?.ApplyPlacementResult(false);
                    }
                    break;
                case BoardTrapCommand.Triggered:
                    if (_pranas.ContainsKey(message.TrapId))
                    {
                        _registry.MarkTriggered(message.TrapId);
                        _pending.TrySchedule(message);
                    }
                    break;
                case BoardTrapCommand.Activated:
                    ApplyActivation(message);
                    break;
            }
        }

        /// <summary>
        /// 設置セルの床面を全種類の罠の基準位置とし、設置成功時に硬直とコストを適用します。
        /// </summary>
        private void ApplyPlacement(BoardTrapMessage message, TrapperPranamData previewData = null)
        {
            if (_processed.Contains(message.TrapId))
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(message.PrefabPath);
            HexCell cell = _board.GetMicroPhaseHexCell(message.X, message.Y);
            if (prefab == null || prefab.GetComponent<TrapperPranaBase>() == null || cell == null ||
                !cell.TryGetSurfacePosition(out Vector3 surfacePosition) || !_registry.TryPlace(message))
            {
                Debug.LogError($"罠の配置情報を適用できません。Trap={message.TrapId}", this);
                return;
            }

            _processed.Add(message.TrapId);
            GameObject instance = Instantiate(prefab, surfacePosition, Quaternion.identity, transform);
            TrapperPranaBase trap = instance.GetComponent<TrapperPranaBase>();
            _pranas.Add(message.TrapId, trap);
            TrapperBase weapon = FindWeapon(message);
            trap.InitializePlacement(message, previewData != null ? previewData : weapon != null ? weapon.TrapData : null);
            weapon?.ApplyPlacementResult(true);
            PlayTrapSound(trap.TrapData, PranamSoundEvent.TrapPlace, instance.transform.position);
            RefreshHighlights();
        }

        /// <summary>
        /// 確定したマス到着で敵だけを検知します。範囲内に立ったままでは繰り返し検知しません。
        /// </summary>
        public void NotifyUnitEnteredCell(UnitBehavior unit, Vector2Int coordinate)
        {
            if (!_network.HasAuthority || unit == null || unit.GetUnitStatus() == null ||
                !unit.GetUnitStatus().IsAlive || MatchPhaseManager.Instance == null ||
                MatchPhaseManager.Instance.CurrentPhase != MatchPhase.Micro)
            {
                return;
            }

            HexCell cell = _board.GetMicroPhaseHexCell(coordinate.x, coordinate.y);
            if (cell == null)
            {
                return;
            }

            foreach (BoardTrapMessage trap in _registry.CaptureEnteringEnemy(unit.PlayerId, unit.GetTeamId(), coordinate, cell.GetFloor()))
            {
                var message = new BoardTrapMessage
                {
                    Command = BoardTrapCommand.Triggered,
                    TrapId = trap.TrapId,
                    TargetId = unit.PlayerId,
                    ActivationDelay = trap.ActivationDelay
                };
                _network.Broadcast(message);
                if (trap.ActivationDelay <= 0.0f)
                {
                    message.Command = BoardTrapCommand.Activated;
                    _network.Broadcast(message);
                }
            }
        }

        /// <summary>
        /// ミクロ中だけ発動遅延を進め、ホストが発動を一度確定します。
        /// </summary>
        private void Update()
        {
            RefreshHighlights();
            if (MatchPhaseManager.Instance == null ||
                MatchPhaseManager.Instance.CurrentPhase != MatchPhase.Micro)
            {
                return;
            }

            foreach (BoardTrapMessage trap in _pending.Advance(Time.deltaTime, _network.HasAuthority))
            {
                trap.Command = BoardTrapCommand.Activated;
                _network.Broadcast(trap);
            }
        }

        /// <summary>
        /// 発動音と効果を一度だけ適用し、罠と赤色表示を取り除きます。
        /// </summary>
        private void ApplyActivation(BoardTrapMessage message)
        {
            if (!_pranas.Remove(message.TrapId, out TrapperPranaBase trap))
            {
                return;
            }

            BoardTrapMessage placement = null;
            foreach (BoardTrapMessage registered in _registry.Traps)
            {
                if (registered.TrapId == message.TrapId)
                {
                    placement = registered;
                    break;
                }
            }

            _pending.Remove(message.TrapId);
            if (placement != null)
            {
                _registry.Remove(placement);
            }

            try
            {
                UnitManager units = UnitManager.Instance;
                UnitBehavior target = units != null && units.TryGetUnit(message.TargetId, out UnitBehavior found) ? found : null;
                if (trap != null)
                {
                    PlayTrapSound(trap.TrapData, PranamSoundEvent.TrapActivate, trap.transform.position);
                    trap.Activate(target);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                if (trap != null)
                {
                    Destroy(trap.gameObject);
                }
                RefreshHighlights();
            }
        }

        /// <summary>
        /// 指定された所有者とスロットに対応する罠兵装を取得します。
        /// </summary>
        private static TrapperBase FindWeapon(BoardTrapMessage message)
        {
            UnitManager units = UnitManager.Instance;
            if (units == null || !units.TryGetUnit(message.OwnerId, out UnitBehavior unit))
            {
                return null;
            }

            foreach (TrapperBase weapon in unit.GetComponentsInChildren<TrapperBase>(true))
            {
                if (weapon.IsMain == message.IsMain && weapon.Number == message.Slot)
                {
                    return weapon;
                }
            }
            return null;
        }

        /// <summary>
        /// 全クライアントで確定イベントに対応する兵装音を一度再生します。
        /// </summary>
        private static void PlayTrapSound(TrapperPranamData data, PranamSoundEvent soundEvent, Vector3 position)
        {
            if (data != null && data.SoundProfile != null &&
                data.SoundProfile.TryGetCue(soundEvent, out SoundCue cue))
            {
                SoundManager.Instance?.PlayCueAtPosition(cue, position);
            }
        }

        /// <summary>
        /// 味方の罠の設置マスと六角形の起動範囲を、マクロとマップの双方へ反映します。
        /// </summary>
        private void RefreshHighlights()
        {
            UnitBehavior observer = UnitManager.Instance != null ? UnitManager.Instance.LocalUnit : null;
            int teamId = _previewObserverTeam ?? (observer != null ? observer.GetTeamId() : -1);
            _highlights.Clear();
            foreach (BoardTrapMessage trap in _registry.Traps)
            {
                if (teamId < 0 || trap.OwnerTeamId != teamId)
                {
                    continue;
                }

                TrapperPranamData data = _pranas.TryGetValue(trap.TrapId, out TrapperPranaBase prana) && prana != null
                    ? prana.TrapData : null;
                Color placementColor = data != null ? data.PlacementHighlightColor : Color.red;
                Color rangeColor = data != null ? data.RangeHighlightColor : new Color(1.0f, 0.55f, 0.55f, 1.0f);
                Vector2Int center = new Vector2Int(trap.X, trap.Y);
                for (int deltaX = -trap.Range; deltaX <= trap.Range; deltaX++)
                {
                    for (int deltaY = -trap.Range * 2; deltaY <= trap.Range * 2; deltaY++)
                    {
                        Vector2Int coordinate = center + new Vector2Int(deltaX, deltaY);
                        HexCell cell = _board.GetMicroPhaseHexCell(coordinate.x, coordinate.y);
                        if (cell == null || !BoardTrapRegistry.IsInTriggerRange(trap, coordinate, cell.GetFloor()))
                        {
                            continue;
                        }

                        bool isPlacement = coordinate == center;
                        if (!_highlights.TryGetValue(coordinate, out var existing) ||
                            isPlacement && !existing.IsPlacement)
                        {
                            _highlights[coordinate] = (isPlacement, isPlacement ? placementColor : rangeColor);
                        }
                    }
                }
            }

            foreach (Vector2Int coordinate in _highlightedCells)
            {
                if (!_highlights.ContainsKey(coordinate))
                {
                    SetCellHighlight(coordinate, false, false, Color.clear);
                }
            }

            _highlightedCells.Clear();
            foreach (var entry in _highlights)
            {
                SetCellHighlight(entry.Key, entry.Value.IsPlacement, true, entry.Value.Color);
                _highlightedCells.Add(entry.Key);
            }
        }

        /// <summary>
        /// 同じ座標にある二つの表示用フィールドへ強調を適用します。
        /// </summary>
        private void SetCellHighlight(Vector2Int coordinate, bool isPlacement, bool visible, Color color)
        {
            _board.GetMapHexCell(coordinate.x, coordinate.y)?.SetTrapHighlight(isPlacement, visible, color);
            _board.GetMacroPhaseHexCell(coordinate.x, coordinate.y)?.SetTrapHighlight(isPlacement, visible, color);
        }

        /// <summary>
        /// 盤面再生成または終了時に罠と表示を全て破棄します。
        /// </summary>
        public void ClearTraps()
        {
            foreach (Vector2Int coordinate in _highlightedCells)
            {
                SetCellHighlight(coordinate, false, false, Color.clear);
            }
            _highlightedCells.Clear();
            _highlights.Clear();
            foreach (TrapperPranaBase trap in _pranas.Values)
            {
                if (trap != null)
                {
                    Destroy(trap.gameObject);
                }
            }
            _registry.Clear();
            _pranas.Clear();
            _pending.Clear();
            _processed.Clear();
            _previewObserverTeam = null;
        }
    }
}
