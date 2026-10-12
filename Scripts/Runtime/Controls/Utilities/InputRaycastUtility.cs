using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PranaTactics.Controls
{
    /// <summary>
    /// マウス位置を使用した3Dオブジェクトおよび
    /// UIオブジェクトへのレイキャスト処理を提供します。
    /// </summary>
    public static class InputRaycastUtility
    {
        /// <summary>
        /// マウス位置からワールド空間へレイを飛ばし、
        /// 指定されたレイヤー上のオブジェクトを取得します。
        /// </summary>
        /// <param name="camera">
        /// マウス位置からレイを生成するカメラです。
        /// </param>
        /// <param name="layerMask">
        /// レイキャストの対象とするレイヤーです。
        /// </param>
        /// <param name="hit">
        /// オブジェクトに衝突した場合のレイキャスト結果です。
        /// </param>
        /// <returns>
        /// レイが対象オブジェクトに衝突した場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        public static bool TryGetWorldObject(
        Camera camera,
        LayerMask layerMask,
        out RaycastHit hit)
        {
            hit = default;

            if (camera == null)
            {
                Debug.LogWarning(
                    $"{nameof(InputRaycastUtility)}: " +
                    "レイキャストに使用するCameraが指定されていません。");

                return false;
            }

            Ray ray =
                camera.ScreenPointToRay(Input.mousePosition);

            return Physics.Raycast(
                ray,
                out hit,
                Mathf.Infinity,
                layerMask);
        }

        /// <summary>
        /// 現在のマウス位置に重なっている
        /// すべてのUIオブジェクトを取得します。
        /// </summary>
        /// <returns>
        /// マウス位置に重なっているUIオブジェクトの一覧です。
        /// EventSystemが存在しない場合は空の一覧を返します。
        /// </returns>
        public static List<GameObject> GetUIRaycastResults()
        {
            List<GameObject> hitObjects = new();

            if (EventSystem.current == null)
            {
                Debug.LogWarning(
                    $"{nameof(InputRaycastUtility)}: " +
                    "現在のシーンにEventSystemが存在しません。");

                return hitObjects;
            }

            PointerEventData pointerEventData =
                new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };

            List<RaycastResult> raycastResults = new();

            EventSystem.current.RaycastAll(
                pointerEventData,
                raycastResults);

            foreach (RaycastResult raycastResult in raycastResults)
            {
                if (raycastResult.gameObject == null)
                {
                    continue;
                }

                hitObjects.Add(raycastResult.gameObject);
            }

            return hitObjects;
        }
    }
}
