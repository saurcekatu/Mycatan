using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine;

public class HexTile : MonoBehaviour
{
    public TerrainType terrainType;
    public int number;
    
    // 【追加】盗賊がいるかどうかのフラグ
    public bool hasRobber = false;

    public List<Intersection> adjacentNodes = new List<Intersection>();

    void Start()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 0.6f);
        foreach (Collider col in colliders)
        {
            Intersection node = col.GetComponent<Intersection>();
            if (node != null) adjacentNodes.Add(node);
        }
    }

    public void DistributeResources()
    {
        // 盗賊がいる場合の処理（そのまま）
        if (hasRobber)
        {
            GameManager.Instance.ShowPopup($"【盗賊】 出目は{number}ですが、{terrainType}の資源はブロックされました！");
            Debug.Log($"【盗賊】 出目は{number}ですが、{terrainType}の資源はブロックされました！");
            return;
        }

        // 【追加】「誰かに資源を配ったか？」を記録するフラグ
        bool resourceGiven = false;

       foreach (Intersection node in adjacentNodes)
        {
            if (node.ownerID != -1) 
            {
                // 【変更】都市(isCity)なら2個、村なら1個配るようにする
                int amount = node.isCity ? 2 : 1;
                
                GameManager.Instance.AddResource(node.ownerID, terrainType, amount);
                resourceGiven = true; 
            }
        }

        // 【追加】もし誰の村もなく、資源が空振りした場合はログを出す
        if (!resourceGiven)
        {
            GameManager.Instance.ShowPopup($"出目【{number}】: 『{terrainType}』から資源が出ましたが、周りに誰の村もありませんでした。");
            Debug.Log($"出目【{number}】: 『{terrainType}』から資源が出ましたが、周りに誰の村もありませんでした。");
        }
    }
}