using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Serialization;

using PranaTactics.Pranam;

namespace PranaTactics.Audio
{
    /// <summary>
    /// BGM、共通効果音、兵装効果音の読み込みと再生を管理します。
    /// </summary>
    public sealed partial class SoundManager : MonoBehaviour
    {
        /// <summary>
        /// PranamSoundProfileを読み込む既定のResourcesパスです。
        /// </summary>
        private const string k_PranamSoundAddressableLabel =
            "PranamSound";

        /// <summary>
        /// 効果音用AudioSourceプールの既定数です。
        /// </summary>
        private const int k_DefaultSESourcePoolSize = 40;

        /// <summary>
        /// AudioSourceの既定音量です。
        /// </summary>
        private const float k_DefaultVolume = 1.0f;

        /// <summary>
        /// AudioSourceの既定ピッチです。
        /// </summary>
        private const float k_DefaultPitch = 1.0f;

        /// <summary>
        /// 2D音声として再生するときの空間ブレンド値です。
        /// </summary>
        private const float k_TwoDimensionalSpatialBlend = 0.0f;

        /// <summary>
        /// 3D音声として再生するときの空間ブレンド値です。
        /// </summary>
        private const float k_ThreeDimensionalSpatialBlend = 1.0f;

        /// <summary>
        /// 3D音声の既定の最小距離です。
        /// </summary>
        private const float k_DefaultMinimumDistance = 1.0f;

        /// <summary>
        /// 3D音声の既定の最大距離です。
        /// </summary>
        private const float k_DefaultMaximumDistance = 50.0f;

        /// <summary>
        /// 再生時間計算で使用するピッチ絶対値の最小値です。
        /// </summary>
        private const float k_MinimumPitchMagnitude = 0.01f;

        /// <summary>
        /// 音源の正面方向を更新するために必要な方向ベクトルの最小二乗長です。
        /// </summary>
        private const float k_ForwardMagnitudeThreshold = 0.0001f;

        /// <summary>
        /// BGMの再生に使用するAudioSourceです。
        /// </summary>
        [Header("Audio Sources")]
        [SerializeField]
        private AudioSource _bgmAudioSource;

        /// <summary>
        /// 共通効果音の再生に使用するAudioSourceです。
        /// </summary>
        [SerializeField]
        private AudioSource _seAudioSource;

        /// <summary>
        /// ローカルプレイヤーの被弾確認音です。
        /// </summary>
        [Header("Local Player Feedback")]
        [SerializeField]
        private SoundCue _damageReceivedCue;

        /// <summary>
        /// 被弾したローカルプレイヤー向けの通知音を再生します。
        /// </summary>
        public void PlayDamageReceivedSE()
        {
            if (_damageReceivedCue != null)
            {
                PlayProtectedDamageCue(_damageReceivedCue);
            }
        }

        /// <summary>
        /// ゲーム全体のBGMキュー割り当てです。
        /// </summary>
        [Header("Sound Data")]
        [SerializeField]
        private BgmSoundProfile _bgmProfile;

        /// <summary>
        /// 共通効果音として使用するサウンドデータの一覧です。
        /// </summary>
        [SerializeField]
        private List<SESoundData> _seSoundDataList = new();

        /// <summary>
        /// PranamSoundProfileを読み込むAddressableパスです。
        /// </summary>
        [Header("Pranam Sound Profiles")]
        [SerializeField]
        private string _pranamSoundProfileAddressableLabel =
            k_PranamSoundAddressableLabel;

        /// <summary>
        /// PranamSoundProfileを読み込むAddressableのハンドルです。
        /// </summary>
        private AsyncOperationHandle<IList<PranamSoundProfile>>
            _pranamSoundProfilesHandle;

        /// <summary>
        /// ゲーム全体に適用するマスター音量です。
        /// </summary>
        [Header("Volume")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _masterVolume = k_DefaultVolume;

        /// <summary>
        /// BGMに適用するマスター音量です。
        /// </summary>
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _bgmMasterVolume = k_DefaultVolume;

        /// <summary>
        /// 効果音に適用するマスター音量です。
        /// </summary>
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _seMasterVolume = k_DefaultVolume;

        /// <summary>
        /// 起動時に生成する効果音用AudioSourceの数です。
        /// </summary>
        [Header("SE Pool")]
        [Min(0)]
        [SerializeField]
        private int _seSourcePoolSize = k_DefaultSESourcePoolSize;

        /// <summary>
        /// Resourcesから読み込んだPranamSoundProfileの一覧です。
        /// </summary>
        private readonly List<PranamSoundProfile> _pranamSoundProfiles = new();

        /// <summary>
        /// 使用可能な効果音用AudioSourceを保持するプールです。
        /// </summary>
        private readonly Queue<AudioSource> _seSourcePool = new();

        /// <summary>
        /// 効果音の種類から対応するサウンドデータを取得する辞書です。
        /// </summary>
        private readonly Dictionary<SESoundData.SE, SESoundData>
            _seDictionary = new();

        /// <summary>
        /// サウンドプロファイルIDから対応するプロファイルを取得する辞書です。
        /// </summary>
        private readonly Dictionary<string, PranamSoundProfile>
            _pranamSoundDictionary = new();

        /// <summary>
        /// 現在再生中のBGMデータに設定されている基準音量です。
        /// </summary>
        private float _currentBGMBaseVolume = k_DefaultVolume;

        /// <summary>
        /// 動的に追加する効果音用AudioSourceの次のインデックスです。
        /// </summary>
        private int _nextPooledSESourceIndex;

        public static SoundManager Instance
        {
            get; private set;
        }

        public float MasterVolume
        {
            get
            {
                return _masterVolume;
            }

            set
            {
                _masterVolume = Mathf.Clamp01(value);
                SetBgmVolume();
            }
        }

        public float BGMMasterVolume
        {
            get
            {
                return _bgmMasterVolume;
            }

            set
            {
                _bgmMasterVolume = Mathf.Clamp01(value);
                SetBgmVolume();
            }
        }

        public float SEMasterVolume
        {
            get
            {
                return _seMasterVolume;
            }

            set
            {
                _seMasterVolume = Mathf.Clamp01(value);
            }
        }

        /// <summary>
        /// ユーザー設定の音量を現在のサウンド出力へ適用します。
        /// </summary>
        private void ApplyUserSettings()
        {
            PranaTactics.Settings.UserSettings settings = PranaTactics.Settings.SettingsManager.Current;

            BGMMasterVolume = settings.BgmVolume;
            SEMasterVolume = settings.SeVolume;
        }

        /// <summary>
        /// SoundManagerを初期化し、サウンドデータとAudioSourceプールを準備します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeCombatFeedback();

            ApplyUserSettings();
            PranaTactics.Settings.SettingsManager.SettingsChanged += ApplyUserSettings;
            InitializeSceneBgm();
            StartCoroutine(Initialize());
        }

        /// <summary>
        /// SoundManagerの非同期初期化を行います。
        /// </summary>
        private IEnumerator Initialize()
        {
            yield return LoadPranamSoundProfilesFromAddressables();

            BuildDictionaries();
            CreateSESourcePool();
            PreloadAllSoundClips();
        }

        /// <summary>
        /// このインスタンスが破棄されたとき、シングルトン参照を解除します。
        /// </summary>
        private void OnDestroy()
        {
            ShutdownSceneBgm();
            ShutdownCombatFeedback();
            PranaTactics.Settings.SettingsManager.SettingsChanged -= ApplyUserSettings;

            if (_pranamSoundProfilesHandle.IsValid())
            {
                Addressables.Release(_pranamSoundProfilesHandle);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 登録されているすべてのAudioClipを事前に読み込みます。
        /// </summary>
        private void PreloadAllSoundClips()
        {
            foreach (PranamSoundProfile profile in _pranamSoundProfiles)
            {
                if (profile == null)
                {
                    continue;
                }

                profile.Preload();
            }

            foreach (SESoundData data in _seSoundDataList)
            {
                if (data == null || data.AudioClip == null)
                {
                    continue;
                }

                data.AudioClip.LoadAudioData();
            }
        }

        /// <summary>
        /// 指定されたAddressablesからPranamSoundProfileを読み込みます。
        /// </summary>
        private IEnumerator LoadPranamSoundProfilesFromAddressables()
        {
            _pranamSoundProfiles.Clear();

            if (string.IsNullOrEmpty(_pranamSoundProfileAddressableLabel))
            {
                Debug.LogWarning(
                    "PranamSoundProfileのAddressablesラベルが設定されていません。",
                    this);

                yield break;
            }

            _pranamSoundProfilesHandle =
                Addressables.LoadAssetsAsync<PranamSoundProfile>(
                    _pranamSoundProfileAddressableLabel,
                    null);

            yield return _pranamSoundProfilesHandle;

            if (_pranamSoundProfilesHandle.Status !=
                AsyncOperationStatus.Succeeded)
            {
                Debug.LogError(
                    $"PranamSoundProfileの読み込みに失敗しました。" +
                    $"Label={_pranamSoundProfileAddressableLabel}",
                    this);

                yield break;
            }

            foreach (PranamSoundProfile profile in
                    _pranamSoundProfilesHandle.Result)
            {
                if (profile != null)
                {
                    _pranamSoundProfiles.Add(profile);
                }
            }

            Debug.Log(
                $"PranamSoundProfile読み込み数: {_pranamSoundProfiles.Count}, " +
                $"Label={_pranamSoundProfileAddressableLabel}",
                this);
        }

        /// <summary>
        /// 登録されたサウンドデータを検索するための辞書を構築します。
        /// </summary>
        private void BuildDictionaries()
        {
            _seDictionary.Clear();
            _pranamSoundDictionary.Clear();

            BuildSEDictionary();
            BuildPranamSoundDictionary();
        }

        /// <summary>
        /// 効果音の種類からSESoundDataを取得する辞書を構築します。
        /// </summary>
        private void BuildSEDictionary()
        {
            foreach (SESoundData data in _seSoundDataList)
            {
                if (data == null)
                {
                    continue;
                }

                if (_seDictionary.ContainsKey(data.Se))
                {
                    Debug.LogWarning(
                        $"SEが重複して登録されています: {data.Se}",
                        this);

                    continue;
                }

                _seDictionary.Add(data.Se, data);
            }
        }

        /// <summary>
        /// サウンドプロファイルIDからPranamSoundProfileを取得する辞書を構築します。
        /// </summary>
        private void BuildPranamSoundDictionary()
        {
            foreach (PranamSoundProfile profile in _pranamSoundProfiles)
            {
                if (profile == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(profile.SoundId))
                {
                    Debug.LogWarning(
                        $"{profile.name}のSoundIdが設定されていません。",
                        profile);

                    continue;
                }

                if (_pranamSoundDictionary.ContainsKey(profile.SoundId))
                {
                    Debug.LogWarning(
                        $"SoundIdが重複しています: {profile.SoundId}",
                        profile);

                    continue;
                }

                _pranamSoundDictionary.Add(
                    profile.SoundId,
                    profile);

                Debug.Log(
                    $"SoundProfile登録: {profile.name}, ID={profile.SoundId}",
                    profile);
            }

            Debug.Log(
                $"登録済みSoundProfile数: {_pranamSoundDictionary.Count}",
                this);
        }

        /// <summary>
        /// 設定された数の効果音用AudioSourceを生成し、プールへ登録します。
        /// </summary>
        private void CreateSESourcePool()
        {
            for (int index = 0; index < _seSourcePoolSize; index++)
            {
                AudioSource source = CreatePooledSESource(index);

                _seSourcePool.Enqueue(source);
            }

            _nextPooledSESourceIndex = _seSourcePoolSize;
        }

        /// <summary>
        /// 効果音再生用のAudioSourceを生成します。
        /// </summary>
        /// <param name="index">
        /// 生成するAudioSourceを識別するインデックスです。
        /// </param>
        /// <returns>
        /// 生成された効果音再生用AudioSourceです。
        /// </returns>
        private AudioSource CreatePooledSESource(int index)
        {
            GameObject sourceObject =
                new GameObject($"PooledSESource_{index}");

            sourceObject.transform.SetParent(transform);
            sourceObject.SetActive(false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;

            return source;
        }

        /// <summary>
        /// プールから使用可能な効果音用AudioSourceを取得します。
        /// プールが空の場合は新しいAudioSourceを生成します。
        /// </summary>
        /// <returns>
        /// 効果音の再生に使用するAudioSourceです。
        /// </returns>
        private AudioSource GetPooledSESource()
        {
            if (_seSourcePool.Count > 0)
            {
                return _seSourcePool.Dequeue();
            }

            AudioSource source =
                CreatePooledSESource(_nextPooledSESourceIndex);

            _nextPooledSESourceIndex++;

            return source;
        }

        /// <summary>
        /// キューの音源と個別音量を使用してBGMをループ再生します。
        /// </summary>
        /// <remarks>
        /// 同じキューの再要求では再生位置を維持します。BGMは2D、等速で再生します。
        /// </remarks>
        public void PlayBGM(SoundCue cue)
        {
            if (_bgmAudioSource == null || cue == null)
            {
                Debug.LogWarning("BGM用AudioSourceまたはキューが設定されていません。", this);
                return;
            }

            if (_currentBgmCue == cue && _bgmAudioSource.isPlaying)
            {
                _currentBGMBaseVolume = cue.Volume;
                SetBgmVolume();
                return;
            }

            AudioClip clip = cue.GetRandomClip();
            if (clip == null)
            {
                Debug.LogWarning($"BGMキューにAudioClipが設定されていません: {cue.name}", cue);
                StopBGM();
                return;
            }

            _currentBgmCue = cue;
            _currentBGMBaseVolume = cue.Volume;
            _bgmAudioSource.clip = clip;
            _bgmAudioSource.loop = true;
            _bgmAudioSource.pitch = k_DefaultPitch;
            _bgmAudioSource.spatialBlend = k_TwoDimensionalSpatialBlend;
            SetBgmVolume();
            _bgmAudioSource.Play();
        }

        /// <summary>
        /// 現在再生中のBGMを停止します。
        /// </summary>
        public void StopBGM()
        {
            if (_bgmAudioSource == null)
            {
                return;
            }

            _bgmAudioSource.Stop();
            _currentBgmCue = null;
        }

        /// <summary>
        /// 現在再生中のBGMにマスター音量を適用します。
        /// </summary>
        public void SetBgmVolume()
        {
            if (_bgmAudioSource == null)
            {
                return;
            }

            _bgmAudioSource.volume =
                _currentBGMBaseVolume *
                _bgmMasterVolume *
                _masterVolume *
                _feedbackBgmGain;
        }

        /// <summary>
        /// 登録された種類の共通効果音を2D音声として再生します。
        /// </summary>
        /// <param name="se">
        /// 再生する効果音の種類です。
        /// </param>
        public void PlaySE(SESoundData.SE se)
        {
            if (_seAudioSource == null)
            {
                Debug.LogWarning(
                    "SE用AudioSourceが設定されていません。",
                    this);

                return;
            }

            if (!_seDictionary.TryGetValue(
                    se,
                    out SESoundData data))
            {
                Debug.LogWarning(
                    $"SEが登録されていません: {se}",
                    this);

                return;
            }

            if (data.AudioClip == null)
            {
                Debug.LogWarning(
                    $"SEのAudioClipが設定されていません: {se}",
                    this);

                return;
            }

            ConfigureCommonSEAudioSource();

            _seAudioSource.PlayOneShot(
                data.AudioClip,
                data.Volume);
        }

        /// <summary>
        /// 指定されたAudioClipを共通効果音として2D再生します。
        /// </summary>
        /// <param name="clip">
        /// 再生するAudioClipです。
        /// </param>
        /// <param name="Volume">
        /// AudioClipに適用する個別音量です。
        /// </param>
        public void PlaySE(
            AudioClip clip,
            float Volume = k_DefaultVolume)
        {
            if (clip == null)
            {
                return;
            }

            if (_seAudioSource == null)
            {
                Debug.LogWarning(
                    "SE用AudioSourceが設定されていません。",
                    this);

                return;
            }

            ConfigureCommonSEAudioSource();

            _seAudioSource.PlayOneShot(
                clip,
                Volume);
        }

        /// <summary>
        /// 共通効果音用AudioSourceを2D再生向けの設定へ戻します。
        /// </summary>
        private void ConfigureCommonSEAudioSource()
        {
            _seAudioSource.volume =
                _seMasterVolume *
                _masterVolume;

            _seAudioSource.pitch = k_DefaultPitch;
            _seAudioSource.spatialBlend =
                k_TwoDimensionalSpatialBlend;
        }

        /// <summary>
        /// サウンドプロファイルIDとイベントに対応する兵装効果音を
        /// 指定された位置で再生します。
        /// </summary>
        /// <param name="soundProfileId">
        /// 再生するPranamSoundProfileの識別子です。
        /// </param>
        /// <param name="soundEvent">
        /// 再生する効果音イベントです。
        /// </param>
        /// <param name="position">
        /// 音源を配置するワールド座標です。
        /// </param>
        /// <param name="forward">
        /// 音源の正面方向です。
        /// </param>
        /// <param name="isLocalOwner">
        /// ローカル操作対象の音を減衰候補として登録する場合は<c>true</c>です。
        /// </param>
        public void PlayPranamSE(
            string soundProfileId,
            PranamSoundEvent soundEvent,
            Vector3 position,
            Vector3 forward,
            bool isLocalOwner = false)
        {
            if (string.IsNullOrEmpty(soundProfileId))
            {
                Debug.LogWarning(
                    "soundProfileIdが設定されていません。",
                    this);

                return;
            }

            if (_pranamSoundDictionary.Count == 0)
            {
                Debug.LogWarning(
                    "PranamSoundProfileがまだ読み込まれていません。",
                    this);

                return;
            }

            if (!_pranamSoundDictionary.TryGetValue(
                    soundProfileId,
                    out PranamSoundProfile profile))
            {
                Debug.LogWarning(
                    $"SoundProfileが見つかりません: {soundProfileId}",
                    this);

                return;
            }

            SoundCue cue = profile.GetCue(soundEvent);

            if (cue == null)
            {
                if (soundEvent == PranamSoundEvent.UnitHit ||
                    soundEvent == PranamSoundEvent.TrapPlace || soundEvent == PranamSoundEvent.TrapActivate)
                {
                    return;
                }

                Debug.LogWarning(
                    $"SoundCueが見つかりません。Profile={profile.name}, " +
                    $"Event={soundEvent}",
                    profile);

                return;
            }

            Debug.Log(
                $"PlayPranamSE: {soundEvent}",
                profile);

            PlayCueAtPosition(
                cue,
                position,
                forward,
                isLocalOwner && IsFeedbackDuckableEvent(soundEvent));
        }

        /// <summary>
        /// PranamDataに登録された効果音を2D音声として再生します。
        /// </summary>
        /// <param name="pranamData">
        /// 使用するサウンドプロファイルを保持するPranamDataです。
        /// </param>
        /// <param name="soundEvent">
        /// 再生する効果音イベントです。
        /// </param>
        public void PlayPranamSE2D(
            PranamData pranamData,
            PranamSoundEvent soundEvent)
        {
            if (pranamData == null ||
                pranamData.SoundProfile == null)
            {
                return;
            }

            SoundCue cue =
                pranamData.SoundProfile.GetCue(soundEvent);

            PlayCue2D(cue);
        }

        /// <summary>
        /// 指定されたSoundCueを2D音声として再生します。
        /// </summary>
        /// <param name="cue">
        /// 再生するAudioClipと再生設定を保持するSoundCueです。
        /// </param>
        public void PlayCue2D(SoundCue cue)
        {
            if (cue == null)
            {
                return;
            }

            AudioClip clip = cue.GetRandomClip();

            if (clip == null)
            {
                return;
            }

            float Volume =
                cue.Volume *
                _seMasterVolume *
                _masterVolume;

            float pitch = cue.GetRandomPitch();

            PlaySEFromPool(
                clip,
                transform.position,
                Vector3.forward,
                Volume,
                pitch,
                k_TwoDimensionalSpatialBlend,
                cue.MinimumDistance,
                cue.MaximumDistance,
                cue.RolloffMode);
        }

        /// <summary>
        /// 指定されたSoundCueを指定位置で再生します。
        /// </summary>
        /// <param name="cue">
        /// 再生するAudioClipと再生設定を保持するSoundCueです。
        /// </param>
        /// <param name="position">
        /// 音源を配置するワールド座標です。
        /// </param>
        public void PlayCueAtPosition(
            SoundCue cue,
            Vector3 position)
        {
            PlayCueAtPosition(
                cue,
                position,
                Vector3.forward);
        }

        /// <summary>
        /// 指定されたSoundCueを指定位置と方向で再生します。
        /// </summary>
        /// <param name="cue">
        /// 再生するAudioClipと再生設定を保持するSoundCueです。
        /// </param>
        /// <param name="position">
        /// 音源を配置するワールド座標です。
        /// </param>
        /// <param name="forward">
        /// 音源の正面方向です。
        /// </param>
        /// <param name="duckForFeedback">
        /// 確認音の再生中に一時減衰させる音の場合は<c>true</c>です。
        /// </param>
        public void PlayCueAtPosition(
            SoundCue cue,
            Vector3 position,
            Vector3 forward,
            bool duckForFeedback = false)
        {
            if (cue == null)
            {
                Debug.LogWarning(
                    "PlayCueAtPositionにnullのSoundCueが渡されました。",
                    this);

                return;
            }

            AudioClip clip = cue.GetRandomClip();

            if (clip == null)
            {
                Debug.LogWarning(
                    $"{cue.name}にAudioClipが設定されていません。",
                    cue);

                return;
            }

            float Volume =
                cue.Volume *
                _seMasterVolume *
                _masterVolume;

            float pitch = cue.GetRandomPitch();

            PlaySEFromPool(
                clip,
                position,
                forward,
                Volume,
                pitch,
                cue.SpatialBlend,
                cue.MinimumDistance,
                cue.MaximumDistance,
                cue.RolloffMode,
                duckForFeedback);
        }

        /// <summary>
        /// 指定されたAudioClipを3D効果音として指定位置で再生します。
        /// </summary>
        /// <param name="clip">
        /// 再生するAudioClipです。
        /// </param>
        /// <param name="position">
        /// 音源を配置するワールド座標です。
        /// </param>
        /// <param name="Volume">
        /// AudioClipに適用する個別音量です。
        /// </param>
        public void PlaySEAtPosition(
            AudioClip clip,
            Vector3 position,
            float Volume = k_DefaultVolume)
        {
            if (clip == null)
            {
                return;
            }

            float finalVolume =
                Volume *
                _seMasterVolume *
                _masterVolume;

            PlaySEFromPool(
                clip,
                position,
                Vector3.forward,
                finalVolume,
                k_DefaultPitch,
                k_ThreeDimensionalSpatialBlend,
                k_DefaultMinimumDistance,
                k_DefaultMaximumDistance,
                AudioRolloffMode.Logarithmic);
        }

        /// <summary>
        /// プールから取得したAudioSourceを使用して効果音を再生します。
        /// </summary>
        /// <param name="clip">
        /// 再生するAudioClipです。
        /// </param>
        /// <param name="position">
        /// 音源を配置するワールド座標です。
        /// </param>
        /// <param name="forward">
        /// 音源の正面方向です。
        /// </param>
        /// <param name="Volume">
        /// AudioSourceに設定する音量です。
        /// </param>
        /// <param name="pitch">
        /// AudioSourceに設定するピッチです。
        /// </param>
        /// <param name="SpatialBlend">
        /// 2D音声と3D音声の混合割合です。
        /// </param>
        /// <param name="MinimumDistance">
        /// 3D音声が最大音量で聞こえる最小距離です。
        /// </param>
        /// <param name="MaximumDistance">
        /// 3D音声の減衰が終了する最大距離です。
        /// </param>
        /// <param name="RolloffMode">
        /// 距離による音量の減衰方式です。
        /// </param>
        private void PlaySEFromPool(
            AudioClip clip,
            Vector3 position,
            Vector3 forward,
            float Volume,
            float pitch,
            float SpatialBlend,
            float MinimumDistance,
            float MaximumDistance,
            AudioRolloffMode RolloffMode,
            bool duckForFeedback = false)
        {
            if (clip == null)
            {
                return;
            }

            if (duckForFeedback && _feedbackDuckSources.Count >= 16)
            {
                return;
            }

            AudioSource source = GetPooledSESource();

            source.gameObject.SetActive(true);
            source.transform.position = position;

            if (forward.sqrMagnitude >
                k_ForwardMagnitudeThreshold)
            {
                source.transform.forward = forward.normalized;
            }

            source.clip = clip;
            source.volume = Volume;
            source.pitch = pitch;
            source.spatialBlend = SpatialBlend;
            source.minDistance = MinimumDistance;
            source.maxDistance = MaximumDistance;
            source.rolloffMode = RolloffMode;
            source.loop = false;

            if (duckForFeedback)
            {
                _feedbackDuckSources[source] = Volume / Mathf.Max(0.00001f, _masterVolume * _seMasterVolume);
                source.volume = Volume * _feedbackWorldGain;
            }

            source.Play();

            float duration =
                clip.length /
                Mathf.Max(
                    Mathf.Abs(pitch),
                    k_MinimumPitchMagnitude);

            StartCoroutine(
                ReturnSESourceAfterPlay(
                    source,
                    duration));
        }

        /// <summary>
        /// 効果音の再生終了後、AudioSourceを初期状態へ戻してプールへ返却します。
        /// </summary>
        /// <param name="source">
        /// プールへ返却するAudioSourceです。
        /// </param>
        /// <param name="delay">
        /// AudioSourceを返却するまでの待機時間です。
        /// </param>
        /// <returns>
        /// AudioSourceの返却完了まで待機するコルーチンです。
        /// </returns>
        private IEnumerator ReturnSESourceAfterPlay(
            AudioSource source,
            float delay)
        {
            yield return new WaitForSeconds(delay);

            if (source == null)
            {
                yield break;
            }

            source.Stop();
            _feedbackDuckSources.Remove(source);
            source.clip = null;
            source.pitch = k_DefaultPitch;
            source.volume = k_DefaultVolume;
            source.spatialBlend = k_TwoDimensionalSpatialBlend;
            source.minDistance = k_DefaultMinimumDistance;
            source.maxDistance = k_DefaultMaximumDistance;
            source.rolloffMode = AudioRolloffMode.Logarithmic;

            source.transform.SetParent(transform);
            source.gameObject.SetActive(false);

            _seSourcePool.Enqueue(source);
        }
    }

    /// <summary>
    /// 共通効果音の種類、AudioClip、個別音量を保持します。
    /// </summary>
    [Serializable]
    public sealed class SESoundData
    {
        public enum SE
        {
            Click,

            Cancel,

            Decide
        }

        /// <summary>
        /// このデータが表す効果音の種類です。
        /// </summary>
        [SerializeField]
        private SE _se;

        /// <summary>
        /// 再生する効果音のAudioClipです。
        /// </summary>
        [SerializeField]
        private AudioClip _audioClip;

        /// <summary>
        /// この効果音に適用する個別音量です。
        /// </summary>
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float _Volume = 1.0f;

        public SE Se
        {
            get
            {
                return _se;
            }
        }

        public AudioClip AudioClip
        {
            get
            {
                return _audioClip;
            }
        }

        public float Volume
        {
            get
            {
                return _Volume;
            }
        }
    }
}
