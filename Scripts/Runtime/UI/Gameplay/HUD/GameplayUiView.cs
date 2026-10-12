using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Match.Phases;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 各ゲームシーンのInspectorで編集する共通UIの表示設定です。
    /// </summary>
    [Serializable]
    public sealed class GameplayUiVisibility
    {
        public bool Health = true;

        public bool ActionPoints = true;

        public bool Timer = true;

        public bool Turn = true;

        public bool Weapons = true;

        public bool MiniMap = true;

        public bool CharacterSelection;

        public bool BackToLobby;
    }

    /// <summary>
    /// 共通シーンのUI参照とマクロ中の表示切り替えを管理します。
    /// </summary>
    /// <remarks>
    /// ホバー透過やミニマップ更新の後で非表示を適用します。
    /// </remarks>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class GameplayUiView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _health;

        [SerializeField]
        private GameObject _actionPoints;

        [SerializeField]
        private GameObject _timer;

        [SerializeField]
        private GameObject _turn;

        [SerializeField]
        private GameObject _weapons;

        [SerializeField]
        private GameObject _miniMap;

        [SerializeField]
        private GameObject _teamRoster;

        /// <summary>
        /// 制限時間以外の共通UIの表示と入力を制御するグループです。
        /// </summary>
        private readonly List<CanvasGroup> _macroHideGroups = new();

        /// <summary>
        /// 非表示にする直前の表示・入力設定です。
        /// </summary>
        private readonly Dictionary<CanvasGroup, (float Alpha, bool Interactable, bool BlocksRaycasts)>
            _savedGroupStates = new();

        /// <summary>
        /// F3によってマクロ中の共通UIを非表示にしているかを示します。
        /// </summary>
        private bool _isMacroHudHidden;

        public bool IsMacroHudHidden => _isMacroHudHidden;
        public int MacroHudChangeCount { get; private set; }

        /// <summary>
        /// 制限時間と別シーンのマクロ専用UIを除く表示対象を取得します。
        /// </summary>
        private void Awake()
        {
            RegisterMacroHideGroup(_health);
            RegisterMacroHideGroup(_actionPoints);
            RegisterMacroHideGroup(_turn);
            RegisterMacroHideGroup(_weapons);
            RegisterMacroHideGroup(_miniMap);
            RegisterMacroHideGroup(_teamRoster);
        }

        /// <summary>
        /// 表示対象のルートへグループを追加し、既存の子要素の透過処理を維持します。
        /// </summary>
        private void RegisterMacroHideGroup(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            if (!root.TryGetComponent(out CanvasGroup group))
            {
                group = root.AddComponent<CanvasGroup>();
            }

            if (!_macroHideGroups.Contains(group))
            {
                _macroHideGroups.Add(group);
            }
        }

        /// <summary>
        /// マクロ中のF3入力で表示を切り替え、フェイズ終了時に元の設定へ戻します。
        /// </summary>
        private void Update()
        {
            MatchPhaseManager manager = MatchPhaseManager.Instance;
            bool isMacro = manager != null && manager.CurrentPhase == MatchPhase.Macro;

            if (!isMacro)
            {
                SetMacroHudHidden(false);
            }
            else if (!PranaTactics.Tutorial.TutorialSession.BlocksInput && Input.GetKeyDown(KeyCode.F3))
            {
                SetMacroHudHidden(!_isMacroHudHidden);
            }
        }

        /// <summary>
        /// 非表示中もUI更新を継続し、背後のフィールドへの入力を通します。
        /// </summary>
        private void LateUpdate()
        {
            if (!_isMacroHudHidden)
            {
                return;
            }

            foreach (CanvasGroup group in _macroHideGroups)
            {
                if (group != null)
                {
                    group.alpha = 0.0f;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                }
            }
        }

        /// <summary>
        /// 非表示前の状態を保存し、再表示時に表示・入力設定を復元します。
        /// </summary>
        private void SetMacroHudHidden(bool isHidden)
        {
            if (_isMacroHudHidden == isHidden)
            {
                return;
            }

            _isMacroHudHidden = isHidden;
            MacroHudChangeCount++;

            if (isHidden)
            {
                foreach (CanvasGroup group in _macroHideGroups)
                {
                    if (group != null)
                    {
                        _savedGroupStates[group] = (group.alpha, group.interactable, group.blocksRaycasts);
                    }
                }
            }
            else
            {
                foreach (var entry in _savedGroupStates)
                {
                    CanvasGroup group = entry.Key;

                    if (group != null)
                    {
                        group.alpha = entry.Value.Alpha;
                        group.interactable = entry.Value.Interactable;
                        group.blocksRaycasts = entry.Value.BlocksRaycasts;
                    }
                }

                _savedGroupStates.Clear();
            }
        }

        /// <summary>
        /// 無効化やシーン終了時に表示・入力設定を復元します。
        /// </summary>
        private void OnDisable()
        {
            SetMacroHudHidden(false);
        }

        /// <summary>
        /// ゲームシーンの設定に従って共通UIの表示項目を反映します。
        /// </summary>
        public void Apply(GameplayUiVisibility visibility)
        {
            _health.SetActive(visibility.Health);
            _actionPoints.SetActive(visibility.ActionPoints);
            _timer.SetActive(visibility.Timer);
            _turn.SetActive(visibility.Turn);
            _weapons.SetActive(visibility.Weapons);
            _miniMap.SetActive(visibility.MiniMap);

            if (_teamRoster != null)
            {
                _teamRoster.SetActive(visibility.Health && visibility.ActionPoints);
            }
        }
    }
}
