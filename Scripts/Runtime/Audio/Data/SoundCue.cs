using System;
using System.Collections.Generic;
using UnityEngine;

using Random = UnityEngine.Random;

namespace PranaTactics.Audio
{
    /// <summary>
    /// BGMまたは効果音として使用するAudioClip群と、
    /// 音量、ピッチおよび3Dサウンド設定を保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "SoundCue",
    menuName = "Prana Tactics/Audio/Sound Cue",
    order = 0)]
    public sealed class SoundCue : ScriptableObject
    {
        /// <summary>
        /// 再生候補となるAudioClipの一覧です。
        /// </summary>
        [Header("Clips")]
        [SerializeField]
        private AudioClip[] _clips;

        /// <summary>
        /// 音源を再生するときの基本音量です。
        /// </summary>
        [Header("Volume")]
        [Range(0.0f, 2.0f)]
        [SerializeField]
        private float _volume = 1.0f;

        /// <summary>
        /// ランダムピッチとして使用する最小値です。
        /// </summary>
        [Header("Pitch")]
        [SerializeField]
        private float _minimumPitch = 0.95f;

        /// <summary>
        /// ランダムピッチとして使用する最大値です。
        /// </summary>
        [SerializeField]
        private float _maximumPitch = 1.05f;

        /// <summary>
        /// 2Dサウンドと3Dサウンドの混合率です。
        /// </summary>
        [Header("3D Sound")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _spatialBlend = 1.0f;

        /// <summary>
        /// 音量減衰が開始される最小距離です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _minimumDistance = 2.0f;

        /// <summary>
        /// 音が聞こえる最大距離です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _maximumDistance = 60.0f;

        /// <summary>
        /// 3Dサウンドの距離減衰方式です。
        /// </summary>
        [SerializeField]
        private AudioRolloffMode _rolloffMode =
            AudioRolloffMode.Logarithmic;

        public IReadOnlyList<AudioClip> Clips
        {
            get
            {
                return _clips;
            }
        }

        public float Volume
        {
            get
            {
                return _volume;
            }
        }

        public float MinimumPitch
        {
            get
            {
                return _minimumPitch;
            }
        }

        public float MaximumPitch
        {
            get
            {
                return _maximumPitch;
            }
        }

        public float SpatialBlend
        {
            get
            {
                return _spatialBlend;
            }
        }

        public float MinimumDistance
        {
            get
            {
                return _minimumDistance;
            }
        }

        public float MaximumDistance
        {
            get
            {
                return _maximumDistance;
            }
        }

        public AudioRolloffMode RolloffMode
        {
            get
            {
                return _rolloffMode;
            }
        }

        /// <summary>
        /// 登録されているAudioClipから、
        /// 有効なクリップをランダムに1つ取得します。
        /// </summary>
        /// <returns>
        /// 選択されたAudioClipです。
        /// 有効なクリップが存在しない場合は<c>null</c>です。
        /// </returns>
        public AudioClip GetRandomClip()
        {
            if (_clips == null ||
                _clips.Length == 0)
            {
                return null;
            }

            int startIndex =
                Random.Range(
                    0,
                    _clips.Length);

            for (int offset = 0;
                offset < _clips.Length;
                offset++)
            {
                int clipIndex =
                    (startIndex + offset) %
                    _clips.Length;

                AudioClip clip =
                    _clips[clipIndex];

                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        /// <summary>
        /// 設定された範囲からランダムなピッチを取得します。
        /// </summary>
        /// <returns>
        /// 再生時に使用するピッチです。
        /// </returns>
        public float GetRandomPitch()
        {
            return Random.Range(
                _minimumPitch,
                _maximumPitch);
        }

        /// <summary>
        /// 登録されている全てのAudioClipの
        /// オーディオデータを事前に読み込みます。
        /// </summary>
        public void Preload()
        {
            if (_clips == null)
            {
                return;
            }

            foreach (AudioClip clip in _clips)
            {
                if (clip == null ||
                    clip.loadState ==
                    AudioDataLoadState.Loaded)
                {
                    continue;
                }

                bool hasStartedLoading =
                    clip.LoadAudioData();

                if (!hasStartedLoading)
                {
                    Debug.LogWarning(
                        $"{nameof(SoundCue)}: " +
                        "AudioClipの事前読み込みを開始できませんでした。" +
                        $"Clip={clip.name}",
                        this);
                }
            }
        }

        /// <summary>
        /// Inspectorで設定が変更されたときに、
        /// ピッチと距離の大小関係を補正します。
        /// </summary>
        private void OnValidate()
        {
            if (_minimumPitch >
                _maximumPitch)
            {
                (_minimumPitch, _maximumPitch) =
                    (_maximumPitch, _minimumPitch);
            }

            _minimumDistance =
                Mathf.Max(
                    0.0f,
                    _minimumDistance);

            _maximumDistance =
                Mathf.Max(
                    _minimumDistance,
                    _maximumDistance);
        }
    }
}
