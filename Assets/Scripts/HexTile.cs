using System.Collections.Generic;
using UnityEngine;

public class HexTile : MonoBehaviour
{
    public TerrainType terrainType; // 地形
    public int number;              // 数字トークン

    // 隣接する6つの交差点を記録するリスト
    private List<Intersection> adjacentNodes = new List<Intersection>();

    void Start()
    {
        // 自分の中心から半径0.6の範囲（頂点がスッポリ入る広さ）を検索
        Collider[] colliders = Physics.OverlapSphere(transform.position, 0.6f);
        foreach (Collider col in colliders)
        {
            Intersection node = col.GetComponent<Intersection>();
            if (node != null)
            {
                adjacentNodes.Add(node);
            }
        }
    }

    // サイコロの目と一致した時に呼ばれるメソッド
    public void DistributeResources()
    {
        foreach (Intersection node in adjacentNodes)
        {
            // この交差点に誰かの村が建っていたら
            if (node.ownerID != -1) 
            {
                // コンソールに獲得メッセージを出す
                Debug.Log($"出目【{number}】: プレイヤー {node.ownerID + 1} が 『{terrainType}』 を獲得しました！");
            }
        }
    }
}