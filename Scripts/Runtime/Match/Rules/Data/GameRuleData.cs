using UnityEngine;

namespace PranaTactics.Match
{
    /// <summary>
    /// ゲームで使用するフェイズ、移動、戦闘、ネットワークおよび
    /// アクションポイントに関するルールを保持します。
    /// </summary>

    [CreateAssetMenu(
    fileName = "GameRuleData",
    menuName = "Prana Tactics/Match/Game Rule Data",
    order = 0)]

    public class GameRuleData : ScriptableObject
    {
        /// <summary>
        /// Macro Phaseをゲーム進行に含めるかどうかを指定します。
        /// </summary>
        [Header("Phase")]
        public bool UseMacroPhase;

        /// <summary>
        /// Micro Phaseをゲーム進行に含めるかどうかを指定します。
        /// </summary>
        public bool UseMicroPhase;

        /// <summary>
        /// Macro Phase中の移動操作を許可するかどうかを指定します。
        /// </summary>
        [Header("Movement")]
        public bool AllowMacroPhase;

        /// <summary>
        /// Micro Phase中の戦闘操作を許可するかどうかを指定します。
        /// </summary>
        [Header("Combat")]
        public bool AllowMicroPhase;

        /// <summary>
        /// 兵装の照準方向を回転できるかどうかを指定します。
        /// </summary>
        public bool AllowPranamRotate;

        /// <summary>
        /// Photon PUNを使用したネットワーク処理を有効にするかどうかを指定します。
        /// </summary>
        [Header("Network")]
        public bool UsePhoton;

        /// <summary>
        /// Epic Online Servicesを使用したネットワーク処理を
        /// 有効にするかどうかを指定します。
        /// </summary>
        public bool UseEos;

        /// <summary>
        /// 行動時にアクションポイントを消費するかどうかを指定します。
        /// </summary>
        [Header("Action Point")]
        public bool UseActionPoint;
    }
}
