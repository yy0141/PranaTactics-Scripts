using UnityEngine;

using PranaTactics.Unit;

namespace PranaTactics.Unit.Networking
{
    public interface IUnitNetwork
    {
        void SetMacroPath(string snapshot);

        void Instantiate(UnitInitializeData data);

        void SetDead();

        void AddTeamScore(int teamId);

        void AddMigrationCoordinate(
            int x,
            int y);

        void TeleportMove(
            int x,
            int y);

        void LerpMove(
            int playerId,
            int x,
            int y);

        void LerpMove(
            Vector3 position);

        void LockMovement(
            float duration);

        void SetMoveSpeed(
            float value);

        void ChangeLayer(
            string layerName);

        void SwitchStealth(
            int teamID,
            bool isStealth);

        void ReportStealthDetection(int targetPlayerId);
    }
}
