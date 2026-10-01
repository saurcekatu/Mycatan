using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum TerrainType { Desert, Forest, Hill, Pasture, Field, Mountain }

public class BoardGenerator : MonoBehaviour
{
    [Header("プレハブ設定")]
    public GameObject hexPrefab;
    public GameObject numberTokenPrefab;
    public GameObject intersectionNodePrefab;
    // 【追加】道（辺）のプレハブ
    public GameObject edgeNodePrefab;

    [Header("ボード設定")]
    public float hexRadius = 0.52f;
    public Transform nodesContainer;
    // 【追加】生成したEdgeをまとめる親オブジェクト
    public Transform edgesContainer;

    private HashSet<Vector3Int> createdVertexIds = new HashSet<Vector3Int>();
    // 【追加】生成済みのEdge（辺）のIDを記録するHashSet
    private HashSet<Vector3Int> createdEdgeIds = new HashSet<Vector3Int>();

    void Start()
    {
        GenerateBoard();
    }

    void GenerateBoard()
    {
        List<TerrainType> terrainDeck = GetStandardTerrainDeck();
        ShuffleDeck(terrainDeck);
        List<int> numberDeck = GetStandardNumberTokens();
        ShuffleDeck(numberDeck);

        int tileIndex = 0;
        int numberIndex = 0;
        int boardRadius = 2;

        for (int q = -boardRadius; q <= boardRadius; q++)
        {
            for (int r = -boardRadius; r <= boardRadius; r++)
            {
                int s = -q - r;

                if (Mathf.Abs(q) <= boardRadius && Mathf.Abs(r) <= boardRadius && Mathf.Abs(s) <= boardRadius)
                {
                    float xPos = hexRadius * Mathf.Sqrt(3) * (q + r / 2f);
                    float zPos = hexRadius * (3f / 2f) * r;
                    Vector3 hexPos = new Vector3(xPos, 0, zPos);

                    GameObject hex = Instantiate(hexPrefab, hexPos, Quaternion.identity);
                    hex.name = $"Hex_{q}_{r}_{s}";
                    hex.transform.SetParent(this.transform);

                    HexTile hexLogic = hex.AddComponent<HexTile>();

                    TerrainType type = terrainDeck[tileIndex];
                    SetHexColor(hex, type);
                    hexLogic.terrainType = type;
                    tileIndex++;

                    if (type != TerrainType.Desert)
                    {
                        int num = numberDeck[numberIndex];
                        SpawnNumberToken(hex, num);
                        hexLogic.number = num; // 数字データを渡す
                        numberIndex++;
                    }
                    else
                    {
                        hexLogic.number = 7;// 砂漠は便宜上7にしておく
                    }

                    // 頂点の生成
                    GenerateMathematicalNodes(q, r, s, hexPos);
                    
                    // 【追加】辺（道）の生成
                    GenerateMathematicalEdges(q, r, s, hexPos);
                }
            }
        }
    }

    // --- 新規追加：辺（道）の生成 ---
    void GenerateMathematicalEdges(int q, int r, int s, Vector3 hexCenter)
    {
        // 辺の中心は「隣接する2つのヘックスの座標を足したもの」として一意のIDを作ります
        Vector3Int[] edgeIDs = new Vector3Int[]
        {
            new Vector3Int(2*q + 1, 2*r, 2*s - 1),     // 0: 右 (0度方向)
            new Vector3Int(2*q, 2*r + 1, 2*s - 1),     // 1: 右上 (60度方向)
            new Vector3Int(2*q - 1, 2*r + 1, 2*s),     // 2: 左上 (120度方向)
            new Vector3Int(2*q - 1, 2*r, 2*s + 1),     // 3: 左 (180度方向)
            new Vector3Int(2*q, 2*r - 1, 2*s + 1),     // 4: 左下 (240度方向)
            new Vector3Int(2*q + 1, 2*r - 1, 2*s)      // 5: 右下 (300度方向)
        };

        for (int i = 0; i < 6; i++)
        {
            Vector3Int eID = edgeIDs[i];

            // すでに隣のヘックスから同じ辺が作られていないかチェック
            if (!createdEdgeIds.Contains(eID))
            {
                float angle_deg = i * 60; 
                float angle_rad = angle_deg * Mathf.Deg2Rad;

                // 辺の中心座標（アポテム：中心から辺までの距離）
                float apothem = hexRadius * Mathf.Sqrt(3) / 2f;
                float x = hexCenter.x + apothem * Mathf.Cos(angle_rad);
                float z = hexCenter.z + apothem * Mathf.Sin(angle_rad);
                Vector3 edgePos = new Vector3(x, 0.1f, z);

                // 辺の向きを計算（中心からの角度に対して垂直になるように90度足す）
                Quaternion rotation = Quaternion.Euler(0, -angle_deg + 90, 0);

                GameObject edge = Instantiate(edgeNodePrefab, edgePos, rotation);
                edge.name = $"Edge_{eID.x}_{eID.y}_{eID.z}";
                edge.transform.SetParent(edgesContainer);

                createdEdgeIds.Add(eID); // 記録
            }
        }
    }

    // --- 既存のメソッド ---
    void GenerateMathematicalNodes(int q, int r, int s, Vector3 hexCenter)
    {
        Vector3Int[] vertexIDs = new Vector3Int[]
        {
            new Vector3Int(3*q + 1, 3*r + 1, 3*s - 2), 
            new Vector3Int(3*q - 1, 3*r + 2, 3*s - 1), 
            new Vector3Int(3*q - 2, 3*r + 1, 3*s + 1), 
            new Vector3Int(3*q - 1, 3*r - 1, 3*s + 2), 
            new Vector3Int(3*q + 1, 3*r - 2, 3*s + 1), 
            new Vector3Int(3*q + 2, 3*r - 1, 3*s - 1)  
        };

        for (int i = 0; i < 6; i++)
        {
            Vector3Int vID = vertexIDs[i];
            if (!createdVertexIds.Contains(vID))
            {
                float angle_deg = 30 + (60 * i);
                float angle_rad = angle_deg * Mathf.Deg2Rad;
                float x = hexCenter.x + hexRadius * Mathf.Cos(angle_rad);
                float z = hexCenter.z + hexRadius * Mathf.Sin(angle_rad);
                Vector3 nodePos = new Vector3(x, 0.1f, z); 

                GameObject node = Instantiate(intersectionNodePrefab, nodePos, Quaternion.identity);
                node.name = $"Node_{vID.x}_{vID.y}_{vID.z}"; 
                node.transform.SetParent(nodesContainer);
                createdVertexIds.Add(vID);
            }
        }
    }

    List<TerrainType> GetStandardTerrainDeck() { return new List<TerrainType> { TerrainType.Desert, TerrainType.Forest, TerrainType.Forest, TerrainType.Forest, TerrainType.Forest, TerrainType.Hill, TerrainType.Hill, TerrainType.Hill, TerrainType.Pasture, TerrainType.Pasture, TerrainType.Pasture, TerrainType.Pasture, TerrainType.Field, TerrainType.Field, TerrainType.Field, TerrainType.Field, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain }; }
    List<int> GetStandardNumberTokens() { return new List<int> { 2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12 }; }
    void ShuffleDeck<T>(List<T> deck) { for (int i = 0; i < deck.Count; i++) { T temp = deck[i]; int randomIndex = Random.Range(i, deck.Count); deck[i] = deck[randomIndex]; deck[randomIndex] = temp; } }
    void SetHexColor(GameObject hex, TerrainType type) { Renderer renderer = hex.GetComponent<Renderer>(); if (renderer == null) return; switch (type) { case TerrainType.Desert: renderer.material.color = new Color(0.9f, 0.8f, 0.5f); break; case TerrainType.Forest: renderer.material.color = new Color(0.1f, 0.5f, 0.2f); break; case TerrainType.Hill: renderer.material.color = new Color(0.8f, 0.4f, 0.1f); break; case TerrainType.Pasture: renderer.material.color = new Color(0.5f, 0.8f, 0.2f); break; case TerrainType.Field: renderer.material.color = new Color(0.9f, 0.8f, 0.2f); break; case TerrainType.Mountain: renderer.material.color = new Color(0.5f, 0.5f, 0.5f); break; } }
    void SpawnNumberToken(GameObject hex, int number) { Vector3 tokenPos = hex.transform.position + new Vector3(0, 0.05f, 0); GameObject token = Instantiate(numberTokenPrefab, tokenPos, Quaternion.identity); token.transform.SetParent(hex.transform); TextMeshPro textMeshPro = token.GetComponentInChildren<TextMeshPro>(); if (textMeshPro != null) { textMeshPro.text = number.ToString(); if (number == 6 || number == 8) { textMeshPro.color = Color.red; } else { textMeshPro.color = Color.black; } } }
}