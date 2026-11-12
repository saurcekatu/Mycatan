using System.Collections.Generic;
using System.IO;
using UnityEngine;

// JSONシリアライズ用のデータ構造
[System.Serializable]
public class CatanNodeData
{
    public int id;
    public float expected_yield;
    public float x;
    public float y;
    public bool is_harbor;       // 追加: 港があるかどうか
    public string port_type;
}

[System.Serializable]
public class CatanEdgeData
{
    public int node_a;
    public int node_b;
}

[System.Serializable]
public class CatanHexData
{
    public int number; 
    public float x;    
    public float y;    
    public string resource_type; // 【修正】ヘックス側に資源タイプを追加
}

[System.Serializable]
public class CatanBoardData
{
    public List<CatanNodeData> nodes = new List<CatanNodeData>();
    public List<CatanEdgeData> edges = new List<CatanEdgeData>();
    public List<CatanHexData> hexes = new List<CatanHexData>();
}

public class BoardExporter : MonoBehaviour
{
    // 開発用：キーボードの「F12」を押すとエクスポートを実行する
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            ExportBoardDataToJson();
        }
    }

    public void ExportBoardDataToJson()
    {
        CatanBoardData boardData = new CatanBoardData();
        
        // 1. 盤面にある全ての「交差点」と「地形タイル」を取得
        Intersection[] allIntersections = FindObjectsByType<Intersection>(FindObjectsSortMode.None);
        HexTile[] allHexes = FindObjectsByType<HexTile>(FindObjectsSortMode.None);

        // 2. 交差点（ノード）のデータ作成
        Dictionary<Intersection, int> nodeToId = new Dictionary<Intersection, int>();
        Dictionary<Intersection, CatanNodeData> nodeDataDict = new Dictionary<Intersection, CatanNodeData>();

        int currentId = 0;
        foreach (Intersection node in allIntersections)
        {
            nodeToId[node] = currentId;
            
            CatanNodeData data = new CatanNodeData();
            data.id = currentId;
            data.expected_yield = 0f; 
            data.x = node.transform.position.x;
            data.y = node.transform.position.z; 
            
            // ↓↓ 追加: 港情報の取得 ↓↓
            if (node.portType != ResourceType.None)
            {
                data.is_harbor = true;
                data.port_type = node.portType.ToString();
            }
            else
            {
                data.is_harbor = false;
                data.port_type = "None";
            }
            
            nodeDataDict[node] = data;
            boardData.nodes.Add(data);
            
            currentId++;
        }

        // 3. 地形タイル（ヘックス）データのエクスポート ＆ 期待値の計算
        foreach (HexTile hex in allHexes)
        {
            // 【修正】Pythonで描画するためのヘックス情報を構築し、リストに追加
            CatanHexData hexData = new CatanHexData();
            hexData.number = hex.number;
            hexData.x = hex.transform.position.x;
            hexData.y = hex.transform.position.z;
            hexData.resource_type = hex.terrainType.ToString();
            
            boardData.hexes.Add(hexData);

            // 期待値（資源の出やすさ）を隣接ノードに配る
            if (hex.terrainType != TerrainType.Desert)
            {
                int dots = GetProbabilityDots(hex.number);
                foreach (Intersection node in hex.adjacentNodes)
                {
                    if (nodeDataDict.ContainsKey(node))
                    {
                        nodeDataDict[node].expected_yield += dots;
                    }
                }
            }
        }

        // 4. 辺（隣接グラフ）データのエクスポート
        HashSet<string> addedEdges = new HashSet<string>();
        foreach (Intersection node in allIntersections)
        {
            int idA = nodeToId[node];

            foreach (Intersection neighbor in node.adjacentNodes)
            {
                if (!nodeToId.ContainsKey(neighbor)) continue;
                
                int idB = nodeToId[neighbor];

                int minId = Mathf.Min(idA, idB);
                int maxId = Mathf.Max(idA, idB);
                string edgeKey = $"{minId}-{maxId}";

                if (!addedEdges.Contains(edgeKey))
                {
                    addedEdges.Add(edgeKey);
                    CatanEdgeData edge = new CatanEdgeData();
                    edge.node_a = minId;
                    edge.node_b = maxId;
                    boardData.edges.Add(edge);
                }
            }
        }

        // 5. JSON文字列化とファイル保存
        string json = JsonUtility.ToJson(boardData, true);
        string filePath = Path.Combine(Application.dataPath, "../CatanBoardData.json");
        File.WriteAllText(filePath, json);

        Debug.Log($"【データ抽出完了】出力先: {filePath}\nノード数: {boardData.nodes.Count}, エッジ数: {boardData.edges.Count}, ヘックス数: {boardData.hexes.Count}");
    }

    private int GetProbabilityDots(int number)
    {
        switch (number)
        {
            case 2: case 12: return 1;
            case 3: case 11: return 2;
            case 4: case 10: return 3;
            case 5: case 9:  return 4;
            case 6: case 8:  return 5;
            default: return 0; // 砂漠(7)
        }
    }
}