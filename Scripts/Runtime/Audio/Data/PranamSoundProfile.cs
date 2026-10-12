using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PranaTactics.Audio
{
    /// <summary>
    /// プラーナムで発生するサウンドイベントと
    /// <see cref="SoundCue"/>の対応関係を保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "PranamSoundProfile",
    menuName = "Prana Tactics/Audio/Trigger Sound Profile",
    order = 1)]
    public sealed class PranamSoundProfile : ScriptableObject
    {
        /// <summary>
        /// ネットワーク上でこのサウンドプロファイルを
        /// 識別するためのIDです。
        /// </summary>
        /// <remarks>
        /// UnityアセットのGUIDから自動的に生成されます。
        /// </remarks>
        [Header("Network")]
        [SerializeField]
        private string _soundId = string.Empty;

        /// <summary>
        /// サウンドイベントとSoundCueの対応一覧です。
        /// </summary>
        [Header("Sound Entries")]
        [SerializeField]
        private List<PranamSoundEntry> _entries = new();

        public string SoundId
        {
            get
            {
                return _soundId;
            }
        }

        public IReadOnlyList<PranamSoundEntry> Entries
        {
            get
            {
                return _entries;
            }
        }

        /// <summary>
        /// 指定されたサウンドイベントに対応する
        /// SoundCueを取得します。
        /// </summary>
        /// <param name="soundEvent">
        /// 取得するサウンドイベントです。
        /// </param>
        /// <returns>
        /// イベントに対応するSoundCueです。
        /// 登録されていない場合は<c>null</c>です。
        /// </returns>
        public SoundCue GetCue(
            PranamSoundEvent soundEvent)
        {
            if (_entries == null)
            {
                return null;
            }

            foreach (PranamSoundEntry entry in _entries)
            {
                if (entry == null ||
                    entry.Event != soundEvent)
                {
                    continue;
                }

                return entry.Cue;
            }

            return null;
        }

        /// <summary>
        /// 指定されたサウンドイベントに対応する
        /// SoundCueの取得を試みます。
        /// </summary>
        /// <param name="soundEvent">
        /// 取得するサウンドイベントです。
        /// </param>
        /// <param name="soundCue">
        /// 取得されたSoundCueです。
        /// </param>
        /// <returns>
        /// 有効なSoundCueを取得できた場合は<c>true</c>です。
        /// </returns>
        public bool TryGetCue(
            PranamSoundEvent soundEvent,
            out SoundCue soundCue)
        {
            soundCue = GetCue(soundEvent);

            return soundCue != null;
        }

        /// <summary>
        /// 登録されている全てのSoundCueについて、
        /// AudioClipの事前読み込みを開始します。
        /// </summary>
        public void Preload()
        {
            if (_entries == null)
            {
                return;
            }

            HashSet<SoundCue> preloadedCues = new();

            foreach (PranamSoundEntry entry in _entries)
            {
                SoundCue soundCue =
                    entry?.Cue;

                if (soundCue == null ||
                    !preloadedCues.Add(soundCue))
                {
                    continue;
                }

                soundCue.Preload();
            }
        }

#if UNITY_EDITOR

        /// <summary>
        /// アセットが編集されたときに、
        /// ネットワーク識別子と登録内容を検証します。
        /// </summary>
        private void OnValidate()
        {
            UpdateSoundId();
            ValidateEntries();
        }

        /// <summary>
        /// このアセットのGUIDをネットワーク識別子として設定します。
        /// </summary>
        private void UpdateSoundId()
        {
            string assetPath =
                AssetDatabase.GetAssetPath(this);

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            string assetGuid =
                AssetDatabase.AssetPathToGUID(assetPath);

            if (string.IsNullOrWhiteSpace(assetGuid) ||
                string.Equals(
                    _soundId,
                    assetGuid,
                    StringComparison.Ordinal))
            {
                return;
            }

            _soundId = assetGuid;

            EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// サウンドエントリーの重複や未設定を確認します。
        /// </summary>
        private void ValidateEntries()
        {
            if (_entries == null)
            {
                _entries = new List<PranamSoundEntry>();

                EditorUtility.SetDirty(this);

                return;
            }

            HashSet<PranamSoundEvent> registeredEvents = new();

            foreach (PranamSoundEntry entry in _entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (!registeredEvents.Add(entry.Event))
                {
                    Debug.LogWarning(
                        $"{nameof(PranamSoundProfile)}: " +
                        "同じサウンドイベントが複数登録されています。" +
                        $"Event={entry.Event}, Profile={name}",
                        this);
                }

                if (entry.Cue == null)
                {
                    Debug.LogWarning(
                        $"{nameof(PranamSoundProfile)}: " +
                        "SoundCueが設定されていないエントリーがあります。" +
                        $"Event={entry.Event}, Profile={name}",
                        this);
                }
            }
        }

#endif
    }

    /// <summary>
    /// プラーナムのサウンドイベントと
    /// 再生するSoundCueの対応を保持します。
    /// </summary>
    [Serializable]
    public sealed class PranamSoundEntry
    {
        /// <summary>
        /// SoundCueを再生するサウンドイベントです。
        /// </summary>
        [SerializeField]
        private PranamSoundEvent _event;

        /// <summary>
        /// イベント発生時に再生するSoundCueです。
        /// </summary>
        [SerializeField]
        private SoundCue _cue;

        public PranamSoundEvent Event
        {
            get
            {
                return _event;
            }
        }

        public SoundCue Cue
        {
            get
            {
                return _cue;
            }
        }
    }
}
