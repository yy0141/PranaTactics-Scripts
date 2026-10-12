using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Core;

namespace PranaTactics.Unit.Networking.Local
{
    /// <summary>
    /// ローカルゲーム用のユニットを生成します。
    /// </summary>
    /// <remarks>
    /// <see cref="GameDataRepository"/>からキャラクターデータを取得し、
    /// 対応するPrefabを生成して<see cref="UnitBehavior"/>を初期化します。
    /// </remarks>
    public sealed class LocalUnitFactory : IUnitFactory
    {
        /// <summary>
        /// 指定された初期化情報からユニットを生成します。
        /// </summary>
        /// <param name="data">
        /// 所有プレイヤー、チーム、キャラクター、
        /// 初期座標および回転を含む初期化情報です。
        /// </param>
        /// <returns>
        /// 初期化が完了したユニットです。
        /// </returns>
        public UnitBehavior Create(
            UnitInitializeData data)
        {
            ValidateInitializeData(
                data);

            GameDataRepository repository =
                GetRepository();

            UnitParams unitParams =
                GetUnitParams(
                    repository,
                    data.CharacterName);

            GameObject unitObject =
                CreateUnitObject(
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
                UnityEngine.Object.Destroy(
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

            if (data.OwnerId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(data),
                    data.OwnerId,
                    "OwnerIdが無効です。");
            }

            if (data.TeamId < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(data),
                    data.TeamId,
                    "TeamIdが無効です。");
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
        /// ゲームデータリポジトリを取得します。
        /// </summary>
        private static GameDataRepository GetRepository()
        {
            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameDataRepository)}が" +
                    "初期化されていません。");
            }

            if (!repository.IsLoaded)
            {
                throw new InvalidOperationException(
                    $"{nameof(GameDataRepository)}の" +
                    "データ読み込みが完了していません。");
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
        /// キャラクターデータに設定された
        /// ユニットPrefabを生成します。
        /// </summary>
        private static GameObject CreateUnitObject(
            UnitParams unitParams,
            string characterName)
        {
            GameObject unitPrefab =
                unitParams.UnitPrefab;

            if (unitPrefab == null)
            {
                throw new InvalidOperationException(
                    $"CharacterName={characterName}の" +
                    $"{nameof(UnitParams.UnitPrefab)}が" +
                    "設定されていません。");
            }

            GameObject unitObject =
                UnityEngine.Object.Instantiate(
                    unitPrefab);

            if (unitObject == null)
            {
                throw new InvalidOperationException(
                    $"CharacterName={characterName}の" +
                    "ユニットPrefabを生成できませんでした。");
            }

            return unitObject;
        }

        /// <summary>
        /// 生成されたGameObjectから
        /// ユニット動作を取得します。
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
    }
}
