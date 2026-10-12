using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Audio;
using PranaTactics.Combat;
using PranaTactics.GameFlow;
using PranaTactics.Networking;
using PranaTactics.Prana;
using PranaTactics.Pranam.Networking;
using PranaTactics.Pranam.Networking.EOS;
using PranaTactics.Pranam.Networking.Local;
using PranaTactics.Pranam.Networking.Photon;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// ユニットが装備しているメインまたはサブのプラーナムと、
    /// プラーナムによって生成されたプラーナオブジェクトを管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PranamBehavior : MonoBehaviourPunCallbacks
    {
        /// <summary>
        /// プレイヤーIDを取得できない場合に使用する値です。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// 使用対象Pranamが見つからない場合に、
        /// 状態ログを再出力するまでの間隔です。
        /// </summary>
        private const float k_SelectionFailureLogInterval = 1.0f;

        /// <summary>
        /// 空のPranamスロットを表す名前です。
        /// </summary>
        private const string k_NonePranamName = "NONE";

        /// <summary>
        /// プラーナオブジェクトを格納する親オブジェクトです。（MicroUnitに追従）
        /// </summary>
        [SerializeField]
        private GameObject _pranaMountRootContainer;

        /// <summary>
        /// プラーナオブジェクトを格納する親オブジェクトです。（ワールド座標で独立）
        /// </summary>
        [SerializeField]
        private GameObject _pranaWorldRootContainer;

        /// <summary>
        /// このプラーナムを所有するユニットです。
        /// </summary>
        [SerializeField]
        private UnitBehavior _unitBehavior;

        /// <summary>
        /// 所有ユニットのタクティカルマップ表現です。
        /// </summary>
        [SerializeField]
        private TacticalMapUnitBehavior _mapUnitBehavior;

        /// <summary>
        /// 使用するネットワーク実装です。
        /// </summary>
        private IPranamNetwork _network;

        /// <summary>
        /// このコンポーネントがメインプラーナムを
        /// 管理しているかどうかを示します。
        /// </summary>
        private bool _isMain;

        /// <summary>
        /// 現在硬直中かどうかを示します。
        /// </summary>
        private bool _isRecovering;

        /// <summary>
        /// 残り硬直時間です。
        /// </summary>
        private float _recoveryTimer;

        /// <summary>
        /// 次に生成するプラーナへ割り当てるIDです。
        /// </summary>
        private int _nextPranaId;

        /// <summary>
        /// 次に使用対象Pranamの状態をログへ出力できる時刻です。
        /// </summary>
        private float _nextSelectionFailureLogTime;

        /// <summary>
        /// このコンポーネントの子に登録されている
        /// プラーナム一覧です。
        /// </summary>
        private readonly List<PranamBase> _pranams = new();

        /// <summary>
        /// 反対側のプラーナムが最後に対象とした
        /// ユニットID一覧です。
        /// </summary>
        private readonly HashSet<int> _targetIds = new();

        /// <summary>
        /// プラーナIDと生成済みGameObjectの対応です。
        /// </summary>
        private readonly Dictionary<int, GameObject> _pranaObjects = new();

        private readonly Dictionary<string, HashSet<PranamSoundEvent>> _soundGroups = new();

        private readonly Queue<string> _soundGroupOrder = new();

        /// <summary>
        /// 分割弾の散開・発射音を兵装使用ごとに各1回にまとめます。
        /// </summary>
        /// <remarks>
        /// シーン内で使用を繰り返しても履歴を無制限に保持しません。
        /// </remarks>
        public bool TryClaimGroupedSound(string groupId, PranamSoundEvent soundEvent)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                return true;
            }

            if (!_soundGroups.TryGetValue(groupId, out var events))
            {
                events = new HashSet<PranamSoundEvent>();
                _soundGroups.Add(groupId, events);
                _soundGroupOrder.Enqueue(groupId);

                if (_soundGroupOrder.Count > 256)
                {
                    _soundGroups.Remove(_soundGroupOrder.Dequeue());
                }
            }

            return events.Add(soundEvent);
        }

        public int UseCount
        {
            get; private set;
        }

        public bool IsUseInProgress
        {
            get
            {
                if (_isRecovering)
                {
                    return true;
                }

                foreach (var pranam in _pranams)
                {
                    if (pranam is AttackerBase attacker && attacker.IsAttackSequenceActive)
                    {
                        return true;
                    }
                }

                foreach (var prana in _pranaObjects.Values)
                {
                    if (prana != null && prana.activeInHierarchy)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int OwnerPlayerId
        {
            get
            {
                return _unitBehavior != null
                    ? _unitBehavior.PlayerId
                    : k_InvalidPlayerId;
            }
        }

        public bool IsMain
        {
            get
            {
                return _isMain;
            }
        }

        public int SelectedSlotIndex
        {
            get
            {
                Transform root = GetPranamSlotRoot();

                if (root == null)
                {
                    return -1;
                }

                for (int i = 0; i < root.childCount; i++)
                {
                    if (root.GetChild(i).gameObject.activeSelf)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        public bool IsRecovering
        {
            get
            {
                return _isRecovering;
            }
        }

        public bool CanSwitchPranam
        {
            get
            {
                return !_isRecovering;
            }
        }

        public bool CanUseHold
        {
            get
            {
                PranamBase activePranam =
                    GetActiveInputPranam();

                return activePranam != null &&
                    activePranam.CanUseHold;
            }
        }

        /// <summary>
        /// 現在設定されているメイン・サブ状態を使用して
        /// 初期化します。
        /// </summary>
        public void Initialize()
        {
            Initialize(_isMain);
        }

        /// <summary>
        /// プラーナム管理コンポーネントを初期化します。
        /// </summary>
        /// <param name="isMain">
        /// メインプラーナムを管理する場合は<c>true</c>です。
        /// </param>
        public void Initialize(bool isMain)
        {
            _isMain = isMain;

            ResolveUnitReferences();
            InitializeNetwork();
            CachePranams();

            Debug.Log(
                $"{nameof(PranamBehavior)} initialized: " +
                $"owner={OwnerPlayerId}, " +
                $"isMain={_isMain}, " +
                $"network={_network?.GetType().Name ?? "null"}",
                this);
        }

        /// <summary>
        /// 指定されたスロット番号の
        /// プラーナムへ切り替えます。
        /// </summary>
        /// <param name="slotNumber">
        /// 切り替えるスロット番号です。
        /// </param>
        /// <returns>
        /// 切り替えに成功した場合は<c>true</c>です。
        /// </returns>
        /// <remarks>
        /// NONEスロットも通常のPranamスロットと同様に
        /// 選択できます。
        ///
        /// NONEにはPranamBaseが存在しないため、
        /// スロットGameObjectを基準に切り替えます。
        /// </remarks>
        public bool TrySelectPranam(
            int slotNumber)
        {
            if (_isRecovering ||
                slotNumber < 0)
            {
                return false;
            }

            Transform slotRoot =
                GetPranamSlotRoot();

            if (slotRoot == null ||
                slotNumber >= slotRoot.childCount)
            {
                return false;
            }

            GameObject targetSlot =
                slotRoot.GetChild(
                        slotNumber)
                    .gameObject;

            if (targetSlot == null)
            {
                return false;
            }

            PranamBase targetPranam =
                targetSlot.GetComponentInChildren<PranamBase>(
                    true);

            bool isNoneSlot =
                IsNonePranamSlot(
                    targetSlot);

            if (targetPranam == null &&
                !isNoneSlot)
            {
                Debug.LogWarning(
                    $"{nameof(PranamBehavior)}: " +
                    "選択対象にPranamBaseがなく、" +
                    "NONEスロットでもありません。" +
                    $" Side={(_isMain ? "Main" : "Sub")}," +
                    $" Slot={slotNumber}," +
                    $" Object={targetSlot.name}",
                    targetSlot);

                return false;
            }

            if (targetPranam != null &&
                !targetPranam.IsRuntimeReady)
            {
                return false;
            }

            for (int index = 0;
                index < slotRoot.childCount;
                index++)
            {
                GameObject slotObject =
                    slotRoot.GetChild(
                            index)
                        .gameObject;

                bool shouldActivate =
                    index ==
                    slotNumber;

                if (slotObject.activeSelf ==
                    shouldActivate)
                {
                    continue;
                }

                slotObject.SetActive(
                    shouldActivate);
            }

            if (targetPranam != null)
            {
                targetPranam.RefreshTargetsImmediately();
            }

            UpdateSelectedPranamUI(
                slotNumber);

            return true;
        }

        /// <summary>
        /// このPranamBehaviorが管理している
        /// スロットルートを取得します。
        /// </summary>
        /// <returns>
        /// MainまたはSubのスロットルートです。
        /// </returns>
        private Transform GetPranamSlotRoot()
        {
            if (_mapUnitBehavior == null)
            {
                return null;
            }

            return _isMain
                ? _mapUnitBehavior.GetMainPranamTransform()
                : _mapUnitBehavior.GetSubPranamTransform();
        }

        /// <summary>
        /// 指定されたGameObjectが
        /// NONEスロットか判定します。
        /// </summary>
        /// <param name="slotObject">
        /// 判定するスロットです。
        /// </param>
        /// <returns>
        /// NONEの場合は<c>true</c>です。
        /// </returns>
        private static bool IsNonePranamSlot(
            GameObject slotObject)
        {
            return slotObject != null &&
                string.Equals(
                    slotObject.name,
                    k_NonePranamName,
                    System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 現在NONEスロットが選択されているか判定します。
        /// </summary>
        private bool IsNonePranamSelected()
        {
            Transform slotRoot =
                GetPranamSlotRoot();

            if (slotRoot == null)
            {
                return false;
            }

            for (int index = 0;
                index < slotRoot.childCount;
                index++)
            {
                GameObject slotObject =
                    slotRoot.GetChild(
                            index)
                        .gameObject;

                if (!slotObject.activeSelf)
                {
                    continue;
                }

                return IsNonePranamSlot(
                    slotObject);
            }

            return false;
        }

        /// <summary>
        /// 現在選択されたPranamをHUDへ反映します。
        /// </summary>
        /// <param name="slotNumber">
        /// 選択されたスロット番号です。
        /// </param>
        private void UpdateSelectedPranamUI(
            int slotNumber)
        {
            if (!IsLocalPlayerPranamOwner())
            {
                return;
            }

            PranamUIManager pranamUiManager =
                PranamUIManager.Instance;

            pranamUiManager?.SetSelectedPranam(
                _isMain,
                slotNumber);
        }

        /// <summary>
        /// 操作引き継ぎ時に選択スロットと現在の硬直時間を表示へ反映します。
        /// </summary>
        public void RefreshControlledUi()
        {
            UpdateSelectedPranamUI(SelectedSlotIndex);

            if (IsLocalPlayerPranamOwner() && _isRecovering)
            {
                PranamUIManager.Instance?.SetCooldownGauge(_isMain, SelectedSlotIndex, _recoveryTimer);
            }
        }

        /// <summary>
        /// 指定されたコストでプラーナムを使用できるか判定します。
        /// </summary>
        /// <param name="actionPointCost">
        /// 消費する行動力です。
        /// </param>
        /// <param name="healthCost">
        /// 使用時に消費するHPです。
        /// </param>
        /// <returns>
        /// 使用可能な場合は<c>true</c>です。
        /// </returns>
        public bool CanUsePranam(
            int actionPointCost,
            float healthCost = 0.0f)
        {
            if (_isRecovering ||
                _mapUnitBehavior == null ||
                _unitBehavior == null)
            {
                return false;
            }

            UnitStatus unitStatus =
                _mapUnitBehavior.GetUnitStatus();

            if (unitStatus == null)
            {
                return false;
            }

            int sanitizedActionPointCost =
                Mathf.Max(
                    0,
                    actionPointCost);

            float sanitizedHealthCost =
                Mathf.Max(
                    0.0f,
                    healthCost);

            return unitStatus.CanAffordProactiveness(sanitizedActionPointCost) &&
                unitStatus.CurrentHealth >=
                    sanitizedHealthCost;
        }

        /// <summary>
        /// 反対側のプラーナムが指定対象を含んでいるか判定します。
        /// </summary>
        /// <param name="targetIds">
        /// 判定する対象ユニットIDです。
        /// </param>
        /// <returns>
        /// いずれかの対象が一致する場合は<c>true</c>です。
        /// </returns>
        public bool CanUseOppositePranam(
            params int[] targetIds)
        {
            return CanUseOppositePranam(
                false,
                targetIds);
        }

        /// <summary>
        /// 反対側のプラーナムが指定対象を含んでいるか判定します。
        /// </summary>
        /// <param name="isExact">
        /// 対象一覧が完全一致する必要がある場合は<c>true</c>です。
        /// </param>
        /// <param name="targetIds">
        /// 判定する対象ユニットIDです。
        /// </param>
        /// <returns>
        /// 条件を満たす場合は<c>true</c>です。
        /// </returns>
        public bool CanUseOppositePranam(
            bool isExact,
            params int[] targetIds)
        {
            targetIds ??=
                System.Array.Empty<int>();

            if (isExact)
            {
                return _targetIds.SetEquals(
                    targetIds);
            }

            return _targetIds.Overlaps(
                targetIds);
        }

        /// <summary>
        /// 現在有効なプラーナムを使用します。
        /// </summary>
        public void UsePranam()
        {
            if (!CanExecuteOwnerLogic())
            {
                return;
            }

            PranamBase activePranam =
                GetActiveRuntimePranam();

            if (activePranam == null)
            {
                if (IsNonePranamSelected())
                {
                    return;
                }

                LogPranamSelectionFailure();

                return;
            }

            if (!activePranam.RequiresInput)
            {
                return;
            }

            activePranam.UsePranam();
        }

        /// <summary>
        /// 現在有効なプラーナムの入力を解放します。
        /// </summary>
        public void ReleasePranam()
        {
            if (!CanExecuteOwnerLogic())
            {
                return;
            }

            PranamBase activePranam =
                GetActiveRuntimePranam();

            if (activePranam != null &&
                activePranam.RequiresInput)
            {
                activePranam.ReleasePranam();
            }
        }

        /// <summary>
        /// プラーナム使用時のコストを所有ユニットへ適用します。
        /// </summary>
        /// <param name="playerId">
        /// コストを発生させたプレイヤーIDです。
        /// </param>
        /// <param name="actionPointCost">
        /// 消費する行動力です。
        /// </param>
        /// <param name="healthCost">
        /// 消費するHPです。
        /// </param>
        public void ConsumeCosts(
            int playerId,
            int actionPointCost,
            float healthCost = 0.0f)
        {
            if (_mapUnitBehavior == null ||
                _unitBehavior == null)
            {
                return;
            }

            Debug.LogWarning(
                $"[PranamCost] " +
                $"RequestedPlayer={playerId}, " +
                $"PranamOwner={OwnerPlayerId}, " +
                $"TargetUnit={_unitBehavior?.PlayerId ?? -1}, " +
                $"MapUnit={_mapUnitBehavior?.GetUnitStatus()?.PlayerId ?? -1}, " +
                $"HealthCost={healthCost}, " +
                $"Object={name}",
                this);

            _mapUnitBehavior.CalcProactiveness(
                Mathf.Max(
                    0,
                    actionPointCost));

            UseCount++;

            PranamBase activePranam = GetActiveRuntimePranam();
            if (activePranam is AttackerBase || activePranam is ShooterBase ||
                activePranam is GunnerBase || activePranam is SniperBase)
            {
                _unitBehavior.RecordAttack();
            }

            if (healthCost > 0.0f)
            {
                _unitBehavior.ApplyDamage(
                    playerId,
                    healthCost,
                    false);
            }
        }

        /// <summary>
        /// プラーナム使用後の硬直と移動制限を設定します。
        /// </summary>
        /// <param name="isMain">
        /// メインプラーナムの場合は<c>true</c>です。
        /// </param>
        /// <param name="number">
        /// プラーナムのスロット番号です。
        /// </param>
        /// <param name="recoveryTime">
        /// 硬直時間です。
        /// </param>
        public void ApplyRecoveryLock(
            bool isMain,
            int number,
            float recoveryTime = 0.0f)
        {
            SetRecoveryTime(
                isMain,
                number,
                recoveryTime);

            _unitBehavior?.LockMovement(
                Mathf.Max(
                    0.0f,
                    recoveryTime));
        }

        /// <summary>
        /// プラーナオブジェクトの親設定をネットワークへ要求します。
        /// </summary>
        /// <param name="objectId">
        /// 親を変更するオブジェクトの識別子です。
        /// PhotonではPhoton View ID、EOSとLocalではプラーナIDを指定します。
        /// </param>
        /// <param name="isMountRoot">
        /// <c>true</c>の場合はMicroUnitに追従するMountRoot、
        /// <c>false</c>の場合はワールド空間のWorldRootへ設定します。
        /// </param>
        public void SetPranaParent(
            int objectId,
            bool isMountRoot)
        {
            if (_network == null)
            {
                Debug.LogWarning(
                    $"{nameof(SetPranaParent)} skipped: " +
                    $"{nameof(IPranamNetwork)} is null.",
                    this);

                return;
            }

            _network.SetPranaParent(
                objectId,
                isMountRoot);
        }

        /// <summary>
        /// プラーナオブジェクトを指定された格納先の子へ設定します。
        /// </summary>
        /// <param name="objectId">
        /// 親を変更するオブジェクトの識別子です。
        /// PhotonではPhoton View ID、EOSとLocalではプラーナIDを指定します。
        /// </param>
        /// <param name="isMountRoot">
        /// <c>true</c>の場合はMountRoot、
        /// <c>false</c>の場合はWorldRootへ設定します。
        /// </param>
        public void ApplySetPranaParent(
            int objectId,
            bool isMountRoot)
        {
            if (!TryGetPranaObject(
                    objectId,
                    out GameObject pranaObject))
            {
                Debug.LogWarning(
                    $"{nameof(ApplySetPranaParent)} failed: " +
                    $"Prana object was not found. " +
                    $"ObjectId={objectId}",
                    this);

                return;
            }

            if (!TryGetPranaParentContainer(
                    isMountRoot,
                    out GameObject parent))
            {
                Debug.LogWarning(
                    $"{nameof(ApplySetPranaParent)} failed: " +
                    $"Parent container is null. " +
                    $"ObjectId={objectId}, " +
                    $"IsMountRoot={isMountRoot}",
                    this);

                return;
            }

            pranaObject.transform.SetParent(
                parent.transform,
                true);
        }

        /// <summary>
        /// プラーナオブジェクトの生成をネットワークへ要求します。
        /// </summary>
        /// <param name="prefabPath">
        /// ResourcesフォルダからのPrefab相対パスです。
        /// </param>
        /// <param name="position">
        /// 生成位置です。
        /// </param>
        /// <param name="rotation">
        /// 生成回転です。
        /// </param>
        /// <param name="context">
        /// 生成するプラーナの情報です。
        /// </param>
        /// <param name="attackDivisionCount">
        /// 固定設定の合計威力を分配する射出弾の数です。
        /// </param>
        public void CreatePranaObject(
            string prefabPath,
            Vector3 position,
            Quaternion rotation,
            PranamContext context,
            int attackDivisionCount = 1)
        {
            if (_network == null)
            {
                Debug.LogError(
                    $"{nameof(CreatePranaObject)} failed: " +
                    $"{nameof(IPranamNetwork)} is null.",
                    this);

                return;
            }

            if (context == null)
            {
                Debug.LogError(
                    $"{nameof(CreatePranaObject)} failed: " +
                    $"{nameof(PranamContext)} is null.",
                    this);

                return;
            }

            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                Debug.LogError(
                    $"{nameof(CreatePranaObject)} failed: " +
                    "Prefab path is empty.",
                    this);

                return;
            }

            if (!string.IsNullOrWhiteSpace(context.DefinitionId))
            {
                GameObject prefab = Resources.Load<GameObject>(prefabPath);
                PranaBase prana = prefab != null ? prefab.GetComponent<PranaBase>() : null;
                if (prana == null ||
                    !prana.TryCreateSpawnSnapshot(context, attackDivisionCount, out PranamContext snapshot))
                {
                    Debug.LogError($"Pranaの発射時設定を確定できません。Path={prefabPath}", this);
                    return;
                }

                context = snapshot;
            }

            context.Id =
                GeneratePranaId();

            _network.InstantiatePrana(
                prefabPath,
                position,
                rotation,
                context);
        }

        /// <summary>
        /// ネットワークから受け取った生成情報を適用します。
        /// </summary>
        /// <param name="prefabPath">
        /// ResourcesフォルダからのPrefab相対パスです。
        /// </param>
        /// <param name="position">
        /// 生成位置です。
        /// </param>
        /// <param name="rotation">
        /// 生成回転です。
        /// </param>
        /// <param name="context">
        /// 生成するプラーナの情報です。
        /// </param>
        public void ApplyInstantiate(
            string prefabPath,
            Vector3 position,
            Quaternion rotation,
            PranamContext context)
        {
            if (context == null)
            {
                Debug.LogWarning(
                    $"{nameof(ApplyInstantiate)} failed: " +
                    $"{nameof(PranamContext)} is null.",
                    this);

                return;
            }

            if (_pranaObjects.ContainsKey(
                    context.Id))
            {
                Debug.LogWarning(
                    $"{nameof(ApplyInstantiate)} skipped: " +
                    $"duplicate Prana ID. Id={context.Id}",
                    this);

                return;
            }

            GameObject prefab =
                Resources.Load<GameObject>(
                    prefabPath);

            if (prefab == null)
            {
                Debug.LogError(
                    $"{nameof(ApplyInstantiate)} failed: " +
                    $"Prefab was not found. Path={prefabPath}",
                    this);

                return;
            }

            GameObject pranaObject =
                Instantiate(
                    prefab,
                    position,
                    rotation);

            if (!TryGetPranaParentContainer(
                    context.IsMountRoot,
                    out GameObject parent))
            {
                Debug.LogError(
                    $"{nameof(ApplyInstantiate)} failed: " +
                    $"Parent container is null. " +
                    $"Id={context.Id}, " +
                    $"IsMountRoot={context.IsMountRoot}",
                    this);

                Destroy(
                    pranaObject);

                return;
            }

            pranaObject.transform.SetParent(
                parent.transform,
                true);

            PranaBase prana =
                pranaObject.GetComponent<PranaBase>();

            if (prana == null)
            {
                Debug.LogError(
                    $"{nameof(ApplyInstantiate)} failed: " +
                    $"{nameof(PranaBase)} is missing. " +
                    $"Path={prefabPath}",
                    pranaObject);

                Destroy(
                    pranaObject);

                return;
            }

            RegisterPranaObject(
                context.Id,
                pranaObject);

            prana.Initialize(
                this,
                context);
        }

        /// <summary>
        /// 指定されたプラーナムの登録位置を取得します。
        /// </summary>
        /// <param name="pranam">
        /// 検索するプラーナムです。
        /// </param>
        /// <returns>
        /// 登録位置です。存在しない場合は<c>-1</c>です。
        /// </returns>
        public int GetPranamIndex(
            PranamBase pranam)
        {
            return _pranams.IndexOf(
                pranam);
        }

        /// <summary>
        /// プラーナム使用後の硬直時間を設定します。
        /// </summary>
        /// <param name="isMain">
        /// メインプラーナムの場合は<c>true</c>です。
        /// </param>
        /// <param name="number">
        /// プラーナムのスロット番号です。
        /// </param>
        /// <param name="recoveryTime">
        /// 硬直時間です。
        /// </param>
        public void SetRecoveryTime(
            bool isMain,
            int number,
            float recoveryTime = 0.0f)
        {
            _recoveryTimer =
                Mathf.Max(
                    0.0f,
                    recoveryTime);

            _isRecovering =
                _recoveryTimer > 0.0f;

            if (!_isRecovering ||
                !IsLocalPlayerPranamOwner())
            {
                return;
            }

            PranamUIManager pranamUiManager =
                PranamUIManager.Instance;

            pranamUiManager?.SetCooldownGauge(
                isMain,
                number,
                _recoveryTimer);
        }

        /// <summary>
        /// プラーナムの攻撃状態を反映します。
        /// </summary>
        /// <param name="pranam">
        /// 攻撃を実行したプラーナムです。
        /// </param>
        /// <param name="context">
        /// 攻撃に関する情報です。
        /// </param>
        public void ApplyAttack(
            PranamBase pranam,
            PranamAttackContext context)
        {
            if (pranam == null ||
                context == null)
            {
                return;
            }

            SetRecoveryTime(
                pranam.IsMain,
                pranam.Number,
                context.Timer);

            _targetIds.Clear();

            if (context.TargetIds == null)
            {
                return;
            }

            foreach (int targetId in
                    context.TargetIds)
            {
                _targetIds.Add(
                    targetId);
            }
        }

        /// <summary>
        /// プラーナの命中と破棄をネットワークへ要求します。
        /// </summary>
        /// <param name="playerId">
        /// 攻撃を行ったプレイヤーIDです。
        /// </param>
        /// <param name="pranaId">
        /// 命中したプラーナのIDです。
        /// </param>
        /// <param name="viewId">
        /// 命中対象ユニットの識別子です。
        /// </param>
        /// <param name="damage">
        /// 対象へ与えるダメージです。
        /// </param>
        /// <param name="destroyPrana">
        /// 命中後にプラーナを破棄する場合は<c>true</c>です。
        /// </param>
        /// <param name="hitSlowdownRatio">
        /// 通常・歩行速度を減らす割合です。0から1で指定します。
        /// </param>
        /// <param name="hitSlowdownDurationSeconds">
        /// 減速が持続するゲーム内秒数です。
        /// </param>
        public void HitPrana(
            int playerId,
            int pranaId,
            int viewId = 0,
            float damage = 0.0f,
            bool destroyPrana = true,
            float hitSlowdownRatio = 0.0f,
            float hitSlowdownDurationSeconds = 0.0f)
        {
            if (_network == null)
            {
                Debug.LogWarning(
                    $"{nameof(HitPrana)} skipped: " +
                    $"{nameof(IPranamNetwork)} is null.",
                    this);

                return;
            }

            int sourcePranamSlot = -1;
            if (_pranaObjects.TryGetValue(pranaId, out GameObject pranaObject) &&
                pranaObject != null && pranaObject.TryGetComponent(out PranaBase prana))
            {
                sourcePranamSlot = prana.SourcePranamNumber;
            }

            if (_network is EosPranamNetwork eosNetwork)
            {
                if (destroyPrana)
                {
                    eosNetwork.HitAndDestroy(
                        playerId,
                        pranaId,
                        viewId,
                        damage,
                        hitSlowdownRatio,
                        hitSlowdownDurationSeconds,
                        sourcePranamSlot);
                }
                else
                {
                    _network.HitUnit(
                        playerId,
                        viewId,
                        damage,
                        hitSlowdownRatio,
                        hitSlowdownDurationSeconds,
                        sourcePranamSlot);
                }

                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null ||
                !unitManager.CanExecuteUnitLogic(
                    playerId))
            {
                return;
            }

            _network.HitUnit(
                playerId,
                viewId,
                damage,
                hitSlowdownRatio,
                hitSlowdownDurationSeconds,
                sourcePranamSlot);

            if (destroyPrana)
            {
                _network.DestroyPrana(
                    pranaId);
            }
        }

        /// <summary>
        /// ネットワークから受け取ったダメージを対象ユニットへ適用します。
        /// </summary>
        /// <param name="hitSlowdownRatio">
        /// 通常・歩行速度を減らす割合です。0から1で指定します。
        /// </param>
        /// <param name="hitSlowdownDurationSeconds">
        /// 減速が持続するゲーム内秒数です。
        /// </param>
        /// <param name="sourcePranamSlot">
        /// 発射時の兵装スロットです。不明な場合は-1です。
        /// </param>
        public void ApplyHit(
            int playerId,
            int viewId,
            float damage,
            float hitSlowdownRatio = 0.0f,
            float hitSlowdownDurationSeconds = 0.0f,
            int sourcePranamSlot = -1)
        {
            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return;
            }

            GameObject targetUnit =
                unitManager.GetUnitObject(
                    viewId);

            if (targetUnit == null)
            {
                Debug.LogWarning(
                    $"{nameof(ApplyHit)} failed: " +
                    $"Unit was not found. ViewId={viewId}",
                    this);

                return;
            }

            IDamageable damageable =
                targetUnit.GetComponent<IDamageable>() ??
                targetUnit.GetComponentInChildren<IDamageable>(
                    true);

            if (damageable == null)
            {
                Debug.LogWarning(
                    $"{nameof(ApplyHit)} failed: " +
                    $"{nameof(IDamageable)} is missing. " +
                    $"Object={targetUnit.name}",
                    targetUnit);

                return;
            }

            UnitBehavior hitUnit = targetUnit.GetComponent<UnitBehavior>();
            float healthBeforeHit = hitUnit?.GetUnitStatus()?.CurrentHealth ?? 0.0f;

            if (hitUnit != null)
            {
                hitUnit.ApplyCombatDamage(
                    playerId,
                    damage,
                    IsMain ? CombatFeedbackSide.Main : CombatFeedbackSide.Sub,
                    sourcePranamSlot);
            }
            else
            {
                damageable.ApplyDamage(playerId, damage);
            }

            if (hitUnit != null && hitUnit.GetUnitStatus() != null &&
                hitUnit.GetUnitStatus().CurrentHealth < healthBeforeHit &&
                unitManager.GetTeamId(playerId) != hitUnit.TeamId)
            {
                hitUnit.ApplyHitSlowdown(hitSlowdownRatio, hitSlowdownDurationSeconds);
            }
        }

        /// <summary>
        /// 指定したプラーナの破棄をネットワークへ要求します。
        /// </summary>
        /// <param name="pranaId">
        /// 破棄するプラーナIDです。
        /// </param>
        public void DestroyPrana(int pranaId)
        {
            _network?.DestroyPrana(
                pranaId);
        }

        /// <summary>
        /// ネットワークから受け取ったプラーナ破棄を適用します。
        /// </summary>
        /// <param name="pranaId">
        /// 破棄するプラーナIDです。
        /// </param>
        public void ApplyDestroyPrana(int pranaId)
        {
            if (!_pranaObjects.Remove(
                    pranaId,
                    out GameObject pranaObject))
            {
                return;
            }

            if (pranaObject != null)
            {
                Destroy(
                    pranaObject);
            }
        }

        /// <summary>
        /// プラーナムの効果音再生をネットワークへ要求します。
        /// </summary>
        public void RequestPranamSound(
            PranamData pranamData,
            PranamSoundEvent soundEvent,
            Vector3 position)
        {
            RequestPranamSound(
                pranamData,
                soundEvent,
                position,
                Vector3.forward);
        }

        /// <summary>
        /// プラーナムの効果音再生をネットワークへ要求します。
        /// </summary>
        public void RequestPranamSound(
            PranamData pranamData,
            PranamSoundEvent soundEvent,
            Vector3 position,
            Vector3 forward)
        {
            if (pranamData == null ||
                pranamData.SoundProfile == null)
            {
                return;
            }

            RequestPranamSoundByProfileId(
                pranamData.SoundProfile.SoundId,
                soundEvent,
                position,
                forward);
        }

        /// <summary>
        /// サウンドプロファイルIDを指定して
        /// プラーナム効果音の再生を要求します。
        /// </summary>
        public void RequestPranamSoundByProfileId(
            string soundProfileId,
            PranamSoundEvent soundEvent,
            Vector3 position,
            Vector3 forward)
        {
            if (_network == null ||
                string.IsNullOrWhiteSpace(
                    soundProfileId))
            {
                return;
            }

            _network.PlayPranamSound(
                soundProfileId,
                soundEvent,
                position,
                forward);
        }

        /// <summary>
        /// ネットワークから受け取った
        /// プラーナム効果音を再生します。
        /// </summary>
        /// <remarks>
        /// UnitHitは兵装固有の接触音として、確定したダメージ結果によるFeedbackの確認音とは別に再生します。
        /// 自分の発射音などは確認音の一時減衰対象として登録します。
        /// </remarks>
        public void ApplyPranamSound(
                    string soundProfileId,
                    PranamSoundEvent soundEvent,
                    Vector3 position,
                    Vector3 forward)
        {
            SoundManager soundManager =
                SoundManager.Instance;

            if (soundManager == null)
            {
                return;
            }

            soundManager.PlayPranamSE(
                soundProfileId,
                soundEvent,
                position,
                forward,
                IsLocalPlayerPranamOwner());
        }

        /// <summary>
        /// 硬直時間を更新します。
        /// </summary>
        private void Update()
        {
            if (!_isRecovering)
            {
                return;
            }

            _recoveryTimer -=
                Time.deltaTime;

            if (_recoveryTimer > 0.0f)
            {
                return;
            }

            _recoveryTimer = 0.0f;
            _isRecovering = false;
        }

        /// <summary>
        /// 所有ユニットへの参照を取得します。
        /// </summary>
        private void ResolveUnitReferences()
        {
            if (_unitBehavior == null)
            {
                _unitBehavior =
                    GetComponentInParent<UnitBehavior>(
                        true);
            }

            if (_mapUnitBehavior == null)
            {
                _mapUnitBehavior =
                    GetComponentInParent<TacticalMapUnitBehavior>(
                        true);
            }
        }

        /// <summary>
        /// 現在のプレイモードと通信方式に対応する
        /// ネットワーク実装を初期化します。
        /// </summary>
        private void InitializeNetwork()
        {
            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            bool isSoloPlay =
                gameFlowManager != null &&
                gameFlowManager.IsSoloPlay;

            if (isSoloPlay)
            {
                _network =
                    GetOrAddNetwork<LocalPranamNetwork>();

                DisableInactiveNetworkComponents(
                    _network);

                return;
            }

            switch (NetworkModeUtility.CurrentMode)
            {
                case NetworkMode.Photon:
                    _network =
                        GetOrAddNetwork<PhotonPranamNetwork>();

                    break;

                case NetworkMode.Eos:
                    {
                        EosPranamNetwork eosNetwork =
                            GetOrAddNetwork<EosPranamNetwork>();

                        eosNetwork.Configure(
                            this,
                            _isMain);

                        _network =
                            eosNetwork;

                        break;
                    }

                default:
                    _network =
                        GetOrAddNetwork<LocalPranamNetwork>();

                    break;
            }

            DisableInactiveNetworkComponents(
                _network);
        }

        /// <summary>
        /// 子オブジェクトに存在するプラーナムを取得します。
        /// </summary>
        private void CachePranams()
        {
            _pranams.Clear();

            PranamBase[] pranams =
                GetComponentsInChildren<PranamBase>(
                    true);

            foreach (PranamBase pranam in
                    pranams)
            {
                if (pranam != null)
                {
                    _pranams.Add(
                        pranam);
                }
            }
        }

        /// <summary>
        /// 現在選択されている、実行可能なPranamを取得します。
        /// </summary>
        /// <remarks>
        /// 親のTacticalMap表示が無効でも選択状態を保持できるよう、
        /// スロット直下の<see cref="GameObject.activeSelf"/>で判定します。
        /// PranamBaseがスロットの子階層にある構成にも対応します。
        /// </remarks>
        private PranamBase GetActiveRuntimePranam()
        {
            Transform slotRoot =
                GetPranamSlotRoot();

            if (slotRoot == null)
            {
                return null;
            }

            for (int slotIndex = 0;
                slotIndex < slotRoot.childCount;
                slotIndex++)
            {
                GameObject slotObject =
                    slotRoot.GetChild(
                            slotIndex)
                        .gameObject;

                if (!slotObject.activeSelf)
                {
                    continue;
                }

                PranamBase[] slotPranams =
                    slotObject.GetComponentsInChildren<PranamBase>(
                        true);

                foreach (PranamBase pranam in
                        slotPranams)
                {
                    if (pranam == null ||
                        !pranam.gameObject.activeSelf ||
                        !pranam.enabled ||
                        !pranam.IsRuntimeReady)
                    {
                        continue;
                    }

                    if (pranam.Number == slotIndex)
                    {
                        return pranam;
                    }
                }

                foreach (PranamBase pranam in
                        slotPranams)
                {
                    if (pranam != null &&
                        pranam.gameObject.activeSelf &&
                        pranam.enabled &&
                        pranam.IsRuntimeReady)
                    {
                        return pranam;
                    }
                }

                return null;
            }

            return null;
        }

        /// <summary>
        /// 現在選択されている入力対象Pranamを取得します。
        /// </summary>
        private PranamBase GetActiveInputPranam()
        {
            PranamBase activePranam =
                GetActiveRuntimePranam();

            return activePranam != null &&
                activePranam.RequiresInput
                    ? activePranam
                    : null;
        }

        /// <summary>
        /// 入力対象Pranamを取得できなかった場合に、
        /// 各スロットの状態をログへ出力します。
        /// </summary>
        private void LogPranamSelectionFailure()
        {
            if (Time.unscaledTime <
                _nextSelectionFailureLogTime)
            {
                return;
            }

            _nextSelectionFailureLogTime =
                Time.unscaledTime +
                k_SelectionFailureLogInterval;

            Debug.LogWarning(
                $"{nameof(UsePranam)} skipped: " +
                "有効な入力対象Pranamがありません。" +
                $" Owner={OwnerPlayerId}," +
                $" IsMain={_isMain}," +
                $" PranamCount={_pranams.Count}",
                this);

            foreach (PranamBase pranam in
                    _pranams)
            {
                if (pranam == null)
                {
                    Debug.LogWarning(
                        $"{nameof(PranamBehavior)}: " +
                        "Pranam=null",
                        this);

                    continue;
                }

                Debug.LogWarning(
                    $"{nameof(PranamBehavior)}: " +
                    "Pranam状態 " +
                    $"Name={pranam.name}," +
                    $" Number={pranam.Number}," +
                    $" ActiveSelf={pranam.gameObject.activeSelf}," +
                    $" ActiveInHierarchy={pranam.gameObject.activeInHierarchy}," +
                    $" ComponentEnabled={pranam.enabled}," +
                    $" RequiresInput={pranam.RequiresInput}," +
                    $" RuntimeInitialized={pranam.IsRuntimeInitialized}," +
                    $" RequiresArc={pranam.RequiresArcInitialization}," +
                    $" ArcInitialized={pranam.IsArcInitialized}," +
                    $" RuntimeReady={pranam.IsRuntimeReady}",
                    pranam);
            }
        }

        /// <summary>
        /// 所有ユニットの処理をこのクライアントで実行できるか判定します。
        /// </summary>
        private bool CanExecuteOwnerLogic()
        {
            if (_unitBehavior == null)
            {
                return false;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            return unitManager != null &&
                unitManager.CanExecuteUnitLogic(
                    OwnerPlayerId);
        }

        /// <summary>
        /// ローカルプレイヤーが所有するプラーナムか判定します。
        /// </summary>
        private bool IsLocalPlayerPranamOwner()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            return _unitBehavior != null &&
                unitManager != null &&
                OwnerPlayerId ==
                unitManager.ControlledUnitId;
        }

        /// <summary>
        /// 指定された追従設定に対応する親オブジェクトを取得します。
        /// </summary>
        private bool TryGetPranaParentContainer(
            bool isMountRoot,
            out GameObject parent)
        {
            parent =
                isMountRoot
                    ? _pranaMountRootContainer
                    : _pranaWorldRootContainer;

            return parent != null;
        }

        /// <summary>
        /// 通信方式に対応する識別子からプラーナオブジェクトを取得します。
        /// </summary>
        private bool TryGetPranaObject(
            int objectId,
            out GameObject pranaObject)
        {
            if (NetworkModeUtility.CurrentMode ==
                NetworkMode.Photon)
            {
                PhotonView targetView =
                    PhotonView.Find(
                        objectId);

                pranaObject =
                    targetView != null
                        ? targetView.gameObject
                        : null;

                return pranaObject != null;
            }

            return _pranaObjects.TryGetValue(
                    objectId,
                    out pranaObject) &&
                pranaObject != null;
        }

        /// <summary>
        /// 新しいプラーナIDを生成します。
        /// </summary>
        private int GeneratePranaId()
        {
            return _nextPranaId++;
        }

        /// <summary>
        /// 生成されたプラーナオブジェクトを登録します。
        /// </summary>
        private void RegisterPranaObject(
            int pranaId,
            GameObject pranaObject)
        {
            if (pranaObject == null)
            {
                return;
            }

            _pranaObjects[pranaId] =
                pranaObject;
        }

        /// <summary>
        /// 指定されたネットワークコンポーネントを取得し、
        /// 存在しない場合は追加します。
        /// </summary>
        private T GetOrAddNetwork<T>()
            where T : MonoBehaviour, IPranamNetwork
        {
            T network =
                GetComponent<T>();

            if (network == null)
            {
                network =
                    gameObject.AddComponent<T>();
            }

            network.enabled = true;

            return network;
        }

        /// <summary>
        /// 現在使用しないネットワークコンポーネントを無効化します。
        /// </summary>
        private void DisableInactiveNetworkComponents(
            IPranamNetwork activeNetwork)
        {
            MonoBehaviour[] behaviours =
                GetComponents<MonoBehaviour>();

            foreach (MonoBehaviour behaviour in
                    behaviours)
            {
                if (behaviour is not IPranamNetwork pranamNetwork)
                {
                    continue;
                }

                behaviour.enabled =
                    ReferenceEquals(
                        pranamNetwork,
                        activeNetwork);
            }
        }
    }
}
