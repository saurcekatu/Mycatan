using System.Collections.Generic;
using UnityEngine;

public class HexTile : MonoBehaviour
{
    public TerrainType terrainType;
    public int number;

    private List<Intersection> adjacentNodes = new List<Intersection>();

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
        foreach (Intersection node in adjacentNodes)
        {
            if (node.ownerID != -1) 
            {
                // GameManagerを呼び出し、村の持ち主に資源を「1つ」与える
                GameManager.Instance.AddResource(node.ownerID, terrainType, 1);
            }
        }
    }
}