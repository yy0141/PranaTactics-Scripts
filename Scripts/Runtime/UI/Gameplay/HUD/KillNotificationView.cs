using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Core;
using PranaTactics.Match;
using PranaTactics.Match.Results;
using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 同期された致死攻撃を、陣営色・Unit画像・兵装名と画像で表示します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KillNotificationView : MonoBehaviour
    {
        [Serializable]
        private sealed class NotificationRow
        {
            public CanvasGroup Group;
            public Image AttackerIcon;
            public Image VictimIcon;
            public Graphic AttackerFrame;
            public Graphic VictimFrame;
            public Image WeaponIcon;
            public TextMeshProUGUI WeaponName;
            public float ExpiresAt;
        }

        [SerializeField]
        private NotificationRow[] _rows = Array.Empty<NotificationRow>();

        [SerializeField]
        private UnitParams[] _characters = Array.Empty<UnitParams>();

        [SerializeField, Min(0.1f)]
        private float _displaySeconds = 6.0f;

        [SerializeField]
        private RectTransform _minimap;

        [SerializeField, Min(0.0f)]
        private float _mapGap = 18.0f;

        private readonly Vector3[] _mapCorners = new Vector3[4];

        private readonly HashSet<int> _notifiedPlayers = new();
        private readonly Dictionary<int, UnitBattleRecord> _records = new();
        private MatchManager _match;
        private float _nextRefreshAt;

        /// <summary>
        /// ミニマップの拡大設定に追従し、その左側へ通知を配置します。
        /// </summary>
        private void LateUpdate()
        {
            if (_minimap == null || transform.parent == null)
            {
                return;
            }

            _minimap.GetWorldCorners(_mapCorners);
            Vector3 topLeft = transform.parent.InverseTransformPoint(_mapCorners[1]);
            Vector3 position = transform.localPosition;
            position.x = topLeft.x - _mapGap;
            position.y = topLeft.y - 140.0f;
            transform.localPosition = position;
        }

        /// <summary>
        /// 初期状態では通知行の表示と入力を無効にします。
        /// </summary>
        private void Awake()
        {
            foreach (NotificationRow row in _rows)
            {
                row.Group.alpha = 0.0f;
                row.Group.interactable = false;
                row.Group.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 最新の累積戦績を低頻度で確認し、表示期限に応じて通知を消します。
        /// </summary>
        private void Update()
        {
            foreach (NotificationRow row in _rows)
            {
                row.Group.alpha = Mathf.Clamp01((row.ExpiresAt - Time.unscaledTime) / 0.4f);
            }

            if (Time.unscaledTime < _nextRefreshAt)
            {
                return;
            }

            _nextRefreshAt = Time.unscaledTime + 0.25f;
            MatchManager match = MatchManager.Instance;
            if (_match != match)
            {
                _match = match;
                _notifiedPlayers.Clear();
                _records.Clear();
                foreach (NotificationRow row in _rows)
                {
                    row.ExpiresAt = 0.0f;
                    row.Group.alpha = 0.0f;
                }
            }

            if (match == null)
            {
                return;
            }

            foreach (UnitBattleRecord record in match.GetBattleRecords())
            {
                _records[record.PlayerId] = record;
            }

            foreach (UnitBattleRecord victim in _records.Values)
            {
                if (!victim.IsAlive && victim.DefeatedBy >= 0 &&
                    !_notifiedPlayers.Contains(victim.PlayerId) &&
                    _records.TryGetValue(victim.DefeatedBy, out UnitBattleRecord attacker))
                {
                    _notifiedPlayers.Add(victim.PlayerId);
                    ShowNotification(attacker, victim);
                }
            }
        }

        /// <summary>
        /// 最新通知を上へ挿入し、最大行数を超えた古い通知を置き換えます。
        /// </summary>
        private void ShowNotification(UnitBattleRecord attacker, UnitBattleRecord victim)
        {
            if (_rows.Length == 0)
            {
                return;
            }

            NotificationRow row = _rows[_rows.Length - 1];
            for (int index = _rows.Length - 1; index > 0; index--)
            {
                _rows[index] = _rows[index - 1];
            }

            _rows[0] = row;
            for (int index = 0; index < _rows.Length; index++)
            {
                RectTransform rect = (RectTransform)_rows[index].Group.transform;
                Vector2 position = rect.anchoredPosition;
                position.y = -index * (rect.rect.height + 8.0f);
                rect.anchoredPosition = position;
            }

            UnitParams attackerCharacter = FindCharacter(attacker.Role);
            UnitParams victimCharacter = FindCharacter(victim.Role);
            SetIcon(row.AttackerIcon, attackerCharacter != null ? attackerCharacter.GameplaySprite : null);
            SetIcon(row.VictimIcon, victimCharacter != null ? victimCharacter.GameplaySprite : null);
            row.AttackerFrame.color = GameplayColorSettings.GetTeamColor(attacker.TeamId);
            row.VictimFrame.color = GameplayColorSettings.GetTeamColor(victim.TeamId);

            PranamData weapon = FindWeapon(attackerCharacter, victim);
            SetIcon(row.WeaponIcon, weapon != null ? weapon.Icon : null);
            row.WeaponName.text = weapon != null ? weapon.DisplayName : "撃破";
            row.ExpiresAt = Time.unscaledTime + _displaySeconds;
            row.Group.alpha = 1.0f;
        }

        /// <summary>
        /// 撃破後にモデルが破棄されても参照可能なキャラクター設定を取得します。
        /// </summary>
        private UnitParams FindCharacter(string role)
        {
            foreach (UnitParams character in _characters)
            {
                if (character != null && character.Type.ToString() == role)
                {
                    return character;
                }
            }

            return null;
        }

        /// <summary>
        /// 現在の選択兵装ではなく、致死攻撃の発射スロットから兵装を取得します。
        /// </summary>
        private static PranamData FindWeapon(UnitParams character, UnitBattleRecord victim)
        {
            GameDataRepository repository = GameDataRepository.Instance;
            if (repository == null || character == null || victim.FatalPranamSlot < 0)
            {
                return null;
            }

            List<List<PranamData>> weapons = repository.GetPranams(character.UnitName);
            int side = victim.FatalPranamIsMain ? 0 : 1;
            if (weapons == null || side >= weapons.Count || weapons[side] == null ||
                victim.FatalPranamSlot >= weapons[side].Count)
            {
                return null;
            }

            return weapons[side][victim.FatalPranamSlot];
        }

        /// <summary>
        /// 欠損画像は白い矩形を描画せず非表示にします。
        /// </summary>
        private static void SetIcon(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
        }
    }
}
