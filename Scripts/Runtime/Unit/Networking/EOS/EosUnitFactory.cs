using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Core;
using PranaTactics.Unit;
using PranaTactics.Unit.Networking;

namespace PranaTactics.Unit.Networking.EOS
{
    /// <summary>
    /// EOSで同期された初期化情報を使用して、
    /// ユニットを各クライアント上へローカル生成します。
    /// </summary>
    /// <remarks>
    /// EOSではPhotonのようなネットワークPrefab生成を使用せず、
    /// Hostから共有された<see cref="UnitInitializeData"/>を基に
    /// 各クライアントが同じユニットPrefabを生成します。
    /// </remarks>
    public sealed class EosUnitFactory : IUnitFactory
    {
        /// <summary>
        /// 指定された初期化情報からEOS用ユニットを生成します。
        /// </summary>
        /// <param name="data">
        /// 所有者、チーム、キャラクター、初期座標および
        /// 回転を含む初期化情報です。
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
        /// データリポジトリ、Prefabまたは必要なコンポーネントが
        /// 設定されていない場合に発生します。
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        /// キャラクターデータが登録されていない場合に発生します。
        /// </exception>
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
                ValidateEosNetworkComponent(
                    unitObject,
                    data.CharacterName);

                UnitBehavior unitBehavior =
                    GetUnitBehavior(
                        unitObject,
                        data.CharacterName);

                unitBehavior.ApplyInstantiate(
                    data);

                LogCreatedUnit(
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
        /// キャラクターデータに設定されたPrefabを
        /// ローカルシーンへ生成します。
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
                    $"{nameof(UnitParams.UnitPrefab)}が設定されていません。");
            }

            GameObject unitObject =
                UnityEngine.Object.Instantiate(
                    unitPrefab,
                    Vector3.zero,
                    Quaternion.identity);

            if (unitObject == null)
            {
                throw new InvalidOperationException(
                    $"CharacterName={characterName}の" +
                    "ユニットPrefabを生成できませんでした。");
            }

            return unitObject;
        }

        /// <summary>
        /// 生成されたユニットにEOS用ネットワークコンポーネントが
        /// 設定されているか確認します。
        /// </summary>
        private static void ValidateEosNetworkComponent(
            GameObject unitObject,
            string characterName)
        {
            if (unitObject.TryGetComponent(
                    out EosUnitNetwork _))
            {
                return;
            }

            throw new InvalidOperationException(
                $"CharacterName={characterName}のPrefabに" +
                $"{nameof(EosUnitNetwork)}が設定されていません。");
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
        /// 生成されたEOSユニットの情報をConsoleへ出力します。
        /// </summary>
        private static void LogCreatedUnit(
            UnitInitializeData data)
        {
            Debug.Log(
                $"{nameof(EosUnitFactory)}: " +
                "ユニットをローカル生成しました。" +
                $" PlayerId={data.OwnerId}," +
                $" TeamId={data.TeamId}," +
                $" Character={data.CharacterName}," +
                $" StartCoordinate=({data.StartX}, {data.StartY})");
        }
    }
}
