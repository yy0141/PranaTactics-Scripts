#if UNITY_EDITOR || PV_CAPTURE
using System;
using System.IO;

using PranaTactics.Audio;
using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.Match;
using PranaTactics.UI;
using PranaTactics.Unit;
using PranaTactics.UI.Gameplay;
using UnityEngine;

namespace PranaTactics.Promo
{
    /// <summary>
    /// PVシーンを通常のソロ対戦として起動し、撮影用のUI切り替えだけを追加します。
    /// </summary>
    public sealed class PvGameplayCapture : MonoBehaviour
    {
        private readonly PvGameplayUiVisibility _ui = new PvGameplayUiVisibility();

        private MatchManager _match;

        private string _character;

        private TextAsset _scenarioAsset;

        [SerializeField]
        private bool _isMacroAssistEnabled;

        public void Initialize(string character, int playersPerTeam, TextAsset scenarioAsset)
        {
            _character = character;
            _scenarioAsset = scenarioAsset;

            foreach (MatchManager match in FindObjectsByType<MatchManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (match.gameObject.scene == gameObject.scene)
                {
                    _match = match;
                    _match.enabled = false;
                }
            }

            if (UIManager.Instance == null)
            {
                CreateService<UIManager>();
            }

            GameFlowManager flow = GameFlowManager.Instance;

            if (flow == null)
            {
                flow = CreateService<GameFlowManager>();
                flow.StartInCaptureScene();
            }

            flow.SetSoloPlayState(true);
            flow.SetCaptureTeamSize(Mathf.Max(1, playersPerTeam));

            if (GameDataRepository.Instance == null)
            {
                CreateService<GameDataRepository>();
            }

            if (SoundManager.Instance == null)
            {
                CreateService<SoundManager>();
            }

            Time.timeScale = 1;
            AudioListener.pause = false;
        }

        private static T CreateService<T>() where T : Component
        {
            return new GameObject("PV Gameplay " + typeof(T).Name).AddComponent<T>();
        }

        private async void Start()
        {
            try
            {
                float deadline = Time.realtimeSinceStartup + 60;

                while (!GameDataRepository.Instance.IsLoaded)
                {
                    if (GameDataRepository.Instance.InitializationError != null)
                    {
                        throw GameDataRepository.Instance.InitializationError;
                    }

                    if (Time.realtimeSinceStartup > deadline)
                    {
                        throw new TimeoutException("PV通常試合のデータ読み込みがタイムアウトしました。");
                    }

                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }

                if (_match == null)
                {
                    throw new InvalidOperationException("PVSceneのMatchManagerとGameplay Characterを確認してください。");
                }

                string json = LoadGameplayJson();

                if (json != null)
                {
                    _match.CaptureScenario = PvGameplayScenario.Parse(json);
                    _character = _match.CaptureScenario.Actors[0].Character;
                    _isMacroAssistEnabled = _match.CaptureScenario.MacroAssistEnabled;
                }

                if (GameDataRepository.Instance.GetCharacterData(_character) == null)
                {
                    throw new ArgumentException($"キャラクター '{_character}' が存在しません。");
                }

                SoloCharacterSelectionContext.SetCharacter(_character);
                await GameplayUiSceneLoader.EnsureLoadedAsync(destroyCancellationToken);
                _match.enabled = true;
                Debug.Log("PV通常試合: InGameと同じ操作・試合進行です。F6でUI表示、F5で操作キャラのマクロ経路補助を切り替えます。", this);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        /// <summary>
        /// 引数指定、実行ファイル隣のJSON、シーン内設定の優先順で撮影設定を読み込みます。
        /// </summary>
        private string LoadGameplayJson()
        {
            string path = null;
            string[] arguments = Environment.GetCommandLineArgs();

            for (int index = 0; index < arguments.Length; index++)
            {
                if (arguments[index] != "-pvGameplay")
                {
                    continue;
                }

                if (++index >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index]))
                {
                    throw new ArgumentException("-pvGameplay の後にJSONのパスが必要です。");
                }

                path = arguments[index];
            }

            if (path == null && !Application.isEditor)
            {
                string defaultPath = Path.GetFullPath(
                    Path.Combine(
                    Application.dataPath,
                    "..",
                    "Gameplay.json"));

                if (File.Exists(defaultPath))
                {
                    path = defaultPath;
                }
            }

            if (path != null)
            {
                string json = File.ReadAllText(path);

                Debug.Log($"PV撮影設定を読み込みました: {Path.GetFullPath(path)}", this);

                return json;
            }

            return _scenarioAsset != null ? _scenarioAsset.text : null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                _isMacroAssistEnabled = !_isMacroAssistEnabled;
                Debug.Log($"PVマクロ経路補助: {(_isMacroAssistEnabled ? "ON" : "OFF（隣接マスを手動選択）")}", this);
            }

            UnitManager.Instance?.LocalUnit?.GetMacroUnitBehavior()?.SetCaptureMacroAssistEnabled(
                _isMacroAssistEnabled);

            if (Input.GetKeyDown(KeyCode.F6))
            {
                _ui.SetVisible(!_ui.IsVisible);
            }
        }

        private void LateUpdate()
        {
            _ui.Refresh();
        }

        private void OnDestroy()
        {
            _ui.Dispose();
        }
    }
}
#endif
