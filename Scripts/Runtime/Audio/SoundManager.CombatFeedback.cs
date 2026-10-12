using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

using PranaTactics.Combat;
using PranaTactics.Match.Phases;
using PranaTactics.Unit;

namespace PranaTactics.Audio
{
    /// <summary>
    /// ローカル操作対象の確認音と、競合音の減衰を管理します。
    /// </summary>
    public sealed partial class SoundManager
    {
        /// <summary>
        /// 命中と撃破の音源、再生間隔、減衰量の設定です。
        /// </summary>
        [SerializeField]
        private CombatFeedbackProfile _combatFeedbackProfile;

        /// <summary>
        /// 同時命中と連続命中の再生制御です。
        /// </summary>
        private readonly CombatFeedbackAudioGate _feedbackGate = new();

        /// <summary>
        /// 自分の装飾音の減衰曲線です。
        /// </summary>
        private readonly CombatFeedbackDuckEnvelope _worldDuck = new();

        /// <summary>
        /// BGMの減衰曲線です。
        /// </summary>
        private readonly CombatFeedbackDuckEnvelope _bgmDuck = new();

        /// <summary>
        /// 減衰対象の音源と、設定音量を掛ける前の基準音量です。
        /// </summary>
        private readonly Dictionary<AudioSource, float> _feedbackDuckSources = new();

        /// <summary>
        /// プールの消費数に影響されない確認音の専用音源です。
        /// </summary>
        private AudioSource _combatFeedbackSource;

        /// <summary>
        /// 置き換える確認音を短くフェードする専用音源です。
        /// </summary>
        private AudioSource _feedbackFadeSource;

        /// <summary>
        /// 被弾を撃破音と独立して伝える専用音源です。
        /// </summary>
        private AudioSource _protectedDamageSource;

        /// <summary>
        /// 確認音の基準音量です。
        /// </summary>
        private float _feedbackBaseVolume;

        /// <summary>
        /// 被弾音の基準音量です。
        /// </summary>
        private float _damageBaseVolume;

        /// <summary>
        /// フェード開始時の音量です。
        /// </summary>
        private float _feedbackFadeVolume;

        /// <summary>
        /// 置き換える確認音のフェード開始時刻です。
        /// </summary>
        private double _feedbackFadeStarted;

        /// <summary>
        /// 次の被弾音を開始できる時刻です。
        /// </summary>
        private double _nextDamageTime;

        /// <summary>
        /// 現在の自分の装飾音の音量比率です。
        /// </summary>
        private float _feedbackWorldGain = 1.0f;

        /// <summary>
        /// 現在のBGMの音量比率です。
        /// </summary>
        private float _feedbackBgmGain = 1.0f;

        /// <summary>
        /// 一時状態を所有する操作対象IDです。
        /// </summary>
        private int _feedbackControlledId = -1;

        /// <summary>
        /// 専用音源を作成し、確定結果とシーン切り替えを購読します。
        /// </summary>
        private void InitializeCombatFeedback()
        {
            _combatFeedbackSource = CreateFeedbackSource("CombatFeedback");
            _feedbackFadeSource = CreateFeedbackSource("CombatFeedbackFade");
            _protectedDamageSource = CreateFeedbackSource("DamageFeedback");
            CombatFeedbackEvents.ResultConfirmed += OnCombatResultConfirmed;
            SceneManager.activeSceneChanged += OnFeedbackSceneChanged;
        }

        /// <summary>
        /// 低い優先度番号を持つ、距離減衰のない専用音源を生成します。
        /// </summary>
        private AudioSource CreateFeedbackSource(string sourceName)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.0f;
            source.priority = 0;
            source.bypassReverbZones = true;
            return source;
        }

        /// <summary>
        /// 購読を解除し、減衰中の音量を復元します。
        /// </summary>
        private void ShutdownCombatFeedback()
        {
            CombatFeedbackEvents.ResultConfirmed -= OnCombatResultConfirmed;
            SceneManager.activeSceneChanged -= OnFeedbackSceneChanged;
            ResetCombatFeedback();
        }

        /// <summary>
        /// 無効化中の確認音と減衰を停止します。
        /// </summary>
        private void OnDisable()
        {
            ResetCombatFeedback();
            _protectedDamageSource?.Stop();
        }

        /// <summary>
        /// シーン間で確認音や再生制限を持ち越さないようにします。
        /// </summary>
        private void OnFeedbackSceneChanged(Scene previous, Scene current)
        {
            ResetCombatFeedback();
            _protectedDamageSource?.Stop();
            _feedbackControlledId = -1;
        }

        /// <summary>
        /// 公開可能な操作結果を、同一フレームの再生候補へ追加します。
        /// </summary>
        private void OnCombatResultConfirmed(CombatFeedbackResult result)
        {
            RefreshFeedbackContext();
            if (isActiveAndEnabled && CombatFeedbackVisibility.CanHear(result))
            {
                _feedbackGate.Submit(result.Kind);
            }
        }

        /// <summary>
        /// フレーム内の候補から確認音を選択し、実行中の音量を更新します。
        /// </summary>
        private void LateUpdate()
        {
            RefreshFeedbackContext();
            double now = Time.unscaledTimeAsDouble;
            if (_feedbackGate.TryTake(now, out CombatFeedbackKind kind) &&
                TryPlayCombatFeedback(kind, now))
            {
                _feedbackGate.Commit(
                    now, kind, _combatFeedbackProfile.HitInterval, _combatFeedbackProfile.EliminationInterval);
            }

            if (_masterVolume <= 0.0f || _seMasterVolume <= 0.0f ||
                _combatFeedbackProfile == null || !_combatFeedbackProfile.EnableDucking)
            {
                _worldDuck.Reset();
                _bgmDuck.Reset();
            }

            _feedbackWorldGain = (float)_worldDuck.GetGain(now);
            _feedbackBgmGain = (float)_bgmDuck.GetGain(now);
            ApplyFeedbackVolumes(now);
        }

        /// <summary>
        /// 操作対象、死亡、フェイズの変更に伴う一時状態を解除します。
        /// </summary>
        private void RefreshFeedbackContext()
        {
            UnitManager manager = UnitManager.Instance;
            int controlledId = manager != null ? manager.ControlledUnitId : -1;
            bool canContinue = manager != null &&
                MatchPhaseManager.Instance != null &&
                MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Micro &&
                manager.TryGetUnit(controlledId, out UnitBehavior unit) &&
                unit.GetUnitStatus() != null && unit.GetUnitStatus().CurrentHealth > 0.0f;

            if (_feedbackControlledId != controlledId || !canContinue)
            {
                ResetCombatFeedback();
                _feedbackControlledId = controlledId;
            }
        }

        /// <summary>
        /// 有効な音源と設定音量がある場合だけ確認音を開始します。
        /// </summary>
        private bool TryPlayCombatFeedback(CombatFeedbackKind kind, double now)
        {
            SoundCue cue = _combatFeedbackProfile != null ? _combatFeedbackProfile.GetCue(kind) : null;
            AudioClip clip = cue != null ? cue.GetRandomClip() : null;
            if (clip == null || cue.Volume <= 0.0f || _masterVolume <= 0.0f || _seMasterVolume <= 0.0f)
            {
                return false;
            }

            _feedbackFadeSource.Stop();
            if (_combatFeedbackSource.isPlaying)
            {
                AudioSource previous = _combatFeedbackSource;
                _combatFeedbackSource = _feedbackFadeSource;
                _feedbackFadeSource = previous;
                _feedbackFadeVolume = previous.volume;
                _feedbackFadeStarted = now;
            }

            _feedbackBaseVolume = cue.Volume;
            _combatFeedbackSource.clip = clip;
            _combatFeedbackSource.pitch = cue.GetRandomPitch();
            _combatFeedbackSource.volume = cue.Volume * _seMasterVolume * _masterVolume;
            _combatFeedbackSource.Play();

            if (_combatFeedbackProfile.EnableDucking)
            {
                bool isElimination = kind == CombatFeedbackKind.Elimination;
                double hold = isElimination ? 0.12 : 0.045;
                double release = isElimination ? 0.18 : 0.09;
                _worldDuck.Request(now, _combatFeedbackProfile.GetDuckGain(kind), hold, release);
                _bgmDuck.Request(now, Mathf.Pow(10.0f, (isElimination ? -4.0f : -2.0f) / 20.0f), hold, release);
            }

            return true;
        }

        /// <summary>
        /// 大量の着弾音で押し出されない専用枠から被弾音を再生します。
        /// </summary>
        private void PlayProtectedDamageCue(SoundCue cue)
        {
            double now = Time.unscaledTimeAsDouble;
            AudioClip clip = cue.GetRandomClip();
            if (clip == null || _protectedDamageSource == null || now < _nextDamageTime ||
                _masterVolume <= 0.0f || _seMasterVolume <= 0.0f)
            {
                return;
            }

            _nextDamageTime = now + 0.08;
            _damageBaseVolume = cue.Volume;
            _protectedDamageSource.clip = clip;
            _protectedDamageSource.pitch = cue.GetRandomPitch();
            _protectedDamageSource.volume = cue.Volume * _seMasterVolume * _masterVolume;
            _protectedDamageSource.Play();
        }

        /// <summary>
        /// 現在の設定音量と減衰曲線を実際の出力へ適用します。
        /// </summary>
        private void ApplyFeedbackVolumes(double now)
        {
            foreach (KeyValuePair<AudioSource, float> entry in _feedbackDuckSources)
            {
                if (entry.Key != null)
                {
                    entry.Key.volume = entry.Value * _masterVolume * _seMasterVolume * _feedbackWorldGain;
                }
            }

            if (_combatFeedbackSource != null)
            {
                _combatFeedbackSource.volume = _feedbackBaseVolume * _masterVolume * _seMasterVolume;
            }

            if (_protectedDamageSource != null)
            {
                _protectedDamageSource.volume = _damageBaseVolume * _masterVolume * _seMasterVolume;
            }

            if (_feedbackFadeSource != null && _feedbackFadeSource.isPlaying)
            {
                float remaining = Mathf.Clamp01(1.0f - (float)((now - _feedbackFadeStarted) / 0.008));
                _feedbackFadeSource.volume = _masterVolume > 0.0f && _seMasterVolume > 0.0f
                    ? _feedbackFadeVolume * remaining
                    : 0.0f;
                if (remaining <= 0.0f)
                {
                    _feedbackFadeSource.Stop();
                }
            }

            SetBgmVolume();
        }

        /// <summary>
        /// 通知、再生間隔、減衰を解除して元の設定音量へ戻します。
        /// </summary>
        private void ResetCombatFeedback()
        {
            _feedbackGate.Reset();
            _worldDuck.Reset();
            _bgmDuck.Reset();
            _feedbackWorldGain = 1.0f;
            _feedbackBgmGain = 1.0f;
            _combatFeedbackSource?.Stop();
            _feedbackFadeSource?.Stop();
            _nextDamageTime = 0.0;
            ApplyFeedbackVolumes(Time.unscaledTimeAsDouble);
        }

        /// <summary>
        /// 警告を保護し、自分の発射・飛翔・装飾的な接触音だけを減衰対象にします。
        /// </summary>
        private static bool IsFeedbackDuckableEvent(PranamSoundEvent soundEvent)
        {
            return soundEvent == PranamSoundEvent.Generate || soundEvent == PranamSoundEvent.Fire ||
                soundEvent == PranamSoundEvent.Rapid_Fire || soundEvent == PranamSoundEvent.Scatter ||
                soundEvent == PranamSoundEvent.Hit || soundEvent == PranamSoundEvent.Destroy;
        }
    }
}
