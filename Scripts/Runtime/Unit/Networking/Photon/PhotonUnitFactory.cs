using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

using PranaTactics.Core;
using PranaTactics.Unit;
using PranaTactics.Unit.Networking;

namespace PranaTactics.Unit.Networking.Photon
{
    /// <summary>
    /// Photonを使用してネットワーク同期されるユニットを生成します。
    /// </summary>
    /// <remarks>
    /// <see cref="GameDataRepository"/>からキャラクターデータを取得し、
    /// <see cref="PhotonNetwork.Instantiate(string, Vector3, Quaternion, byte, object[])"/>
    /// を使用して全クライアントへ同期されるユニットを生成します。
    /// </remarks>
    public sealed class PhotonUnitFactory : IUnitFactory
    {
        /// <summary>
        /// 指定された初期化情報からPhoton同期ユニットを生成します。
        /// </summary>
        /// <param name="data">
        /// 所有プレイヤー、チーム、キャラクター、
        /// 初期座標および回転を含む初期化情報です。
        /// </param>
        /// <returns>
        /// 初期化が完了したユニットです。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="data"/>が<c>null</c>の場合に発生します。
        /// </exception>
        /// <exception cref="ArgumentException">
        /// キャラクター名が設定されていない場合に発生します。
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Photonルームへ参加していない場合、
        /// データリポジトリが初期化されていない場合、
        /// またはPrefab設定が不正な場合に発生します。
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        /// 指定されたキャラクターデータが存在しない場合に発生します。
        /// </exception>
        public UnitBehavior Create(
            UnitInitializeData data)
        {
            ValidateInitializeData(
                data);

            ValidatePhotonState();

            GameDataRepository repository =
                GetRepository();

            UnitParams unitParams =
                GetUnitParams(
                    repository,
                    data.CharacterName);

            GameObject unitObject =
                CreateNetworkUnitObject(
                    unitParams,
                    data.CharacterName);

            try
            {
                UnitBehavior unitBehavior =
                    GetUnitBehavior(
                        unitObject,
                        data.CharacterName);

                unitBehavior.Instantiate(
                    data);

                return unitBehavior;
            }
            catch
            {
                DestroyNetworkUnitObject(
                    unitObject);

                throw;
            }
        }

        /// <summary>
        /// ユニット初期化情報を検証します。
        /// </summary>
        private static void ValidateInitializeData(
            UnitInitializeData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(
                    nameof(data));
            }

            if (string.IsNullOrWhiteSpace(
                    data.CharacterName))
            {
                throw new ArgumentException(
                    "CharacterNameが設定されていません。",
                    nameof(data));
            }
        }

        /// <summary>
        /// Photonユニットを生成できる接続状態か確認します。
        /// </summary>
        private static void ValidatePhotonState()
        {
            if (!PhotonNetwork.IsConnected)
            {
                throw new InvalidOperationException(
                    "Photonへ接続されていないため、" +
                    "ユニットを生成できません。");
            }

            if (!PhotonNetwork.InRoom ||
                PhotonNetwork.CurrentRoom == null)
            {
                throw new InvalidOperationException(
                    "Photonルームへ参加していないため、" +
                    "ユニットを生成できません。");
            }

            if (PhotonNetwork.LocalPlayer == null)
            {
                throw new InvalidOperationException(
                    "Photonのローカルプレイヤー情報を取得できません。");
            }
        }

        /// <summary>
        /// ゲームデータリポジトリを取得します。
        /// </summary>
        private static GameDataRepository GetRepository()
        {
            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameDataRepository)}が初期化されていません。");
            }

            return repository;
        }

        /// <summary>
        /// 指定されたキャラクターのパラメータを取得します。
        /// </summary>
        private static UnitParams GetUnitParams(
            GameDataRepository repository,
            string characterName)
        {
            UnitParams unitParams =
                repository.GetCharacterData(
                    characterName);

            if (unitParams == null)
            {
                throw new KeyNotFoundException(
                    $"CharacterName={characterName}に対応する" +
                    $"{nameof(UnitParams)}が登録されていません。");
            }

            return unitParams;
        }

        /// <summary>
        /// Photonを使用してユニットPrefabを生成します。
        /// </summary>
        private static GameObject CreateNetworkUnitObject(
            UnitParams unitParams,
            string characterName)
        {
            GameObject unitPrefab =
                unitParams.UnitPrefab;

            if (unitPrefab == null)
            {
                throw new InvalidOperationException(
                    $"CharacterName={characterName}の" +
                    $"{nameof(UnitParams.UnitPrefab)}が設定されていません。");
            }

            string prefabName =
                unitPrefab.name;

            if (string.IsNullOrWhiteSpace(
                    prefabName))
            {
                throw new InvalidOperationException(
                    $"CharacterName={characterName}の" +
                    "Photon Prefab名が設定されていません。");
            }

            GameObject unitObject =
                PhotonNetwork.Instantiate(
                    prefabName,
                    Vector3.zero,
                    Quaternion.identity);

            if (unitObject == null)
            {
                throw new InvalidOperationException(
                    $"Photonによるユニット生成に失敗しました。" +
                    $" PrefabName={prefabName}");
            }

            return unitObject;
        }

        /// <summary>
        /// 生成されたGameObjectからユニット動作を取得します。
        /// </summary>
        private static UnitBehavior GetUnitBehavior(
            GameObject unitObject,
            string characterName)
        {
            if (unitObject.TryGetComponent(
                    out UnitBehavior unitBehavior))
            {
                return unitBehavior;
            }

            throw new InvalidOperationException(
                $"CharacterName={characterName}のPrefabに" +
                $"{nameof(UnitBehavior)}が設定されていません。");
        }

        /// <summary>
        /// 初期化に失敗したPhotonユニットを破棄します。
        /// </summary>
        private static void DestroyNetworkUnitObject(
            GameObject unitObject)
        {
            if (unitObject == null)
            {
                return;
            }

            PhotonView photonView =
                unitObject.GetComponent<PhotonView>();

            if (PhotonNetwork.InRoom &&
                photonView != null &&
                photonView.IsMine)
            {
                PhotonNetwork.Destroy(
                    unitObject);

                return;
            }

            UnityEngine.Object.Destroy(
                unitObject);
        }
    }
}
