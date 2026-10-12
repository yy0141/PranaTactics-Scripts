using UnityEngine.SceneManagement;

using PranaTactics.Unit;

namespace PranaTactics.GameFlow.States
{
    /// <summary>
    /// 設定用のローカルシーンを開始します。
    /// </summary>
    public sealed class SettingsState : LocalSimulationState
    {
        protected override string SceneName => "SettingScene";

        /// <summary>
        /// 射撃訓練用ボットへ撃破後の自動再スポーンを設定します。
        /// </summary>
        protected override void OnSimulationReady(UnitBehavior player, UnitBehavior target)
        {
            if (target == null)
            {
                return;
            }

            RespawnBot respawn = target.GetComponent<RespawnBot>();
            if (respawn == null)
            {
                respawn = target.gameObject.AddComponent<RespawnBot>();
            }

            respawn.SetRespawnEnabled(true);
            respawn.enabled = true;
            SceneManager.LoadSceneAsync("TrainingCharacterSelectUI", LoadSceneMode.Additive);
        }
    }
}
