using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum TerrainType { Desert, Forest, Hill, Pasture, Field, Mountain }

public class BoardGenerator : MonoBehaviour
{
    [Header("プレハブ設定")]
    public GameObject hexPrefab;
    public GameObject numberTokenPrefab;
    // 【追加】交差点（Node）のプレハブ
    public GameObject intersectionNodePrefab;

    [Header("ボード設定")]
    public float hexRadius = 0.52f;
    // 【追加】生成したNodeをまとめる親オブジェクト
    public Transform nodesContainer;

    // 【追加】生成済みのNodeの座標を記録するリスト（重複排除用）
    private List<Vector3> createdNodes = new List<Vector3>();

    void Start()
    {
        GenerateBoard();
    }

    void GenerateBoard()
    {
        int[] rowLengths = { 3, 4, 5, 4, 3 };
        float hexWidth = Mathf.Sqrt(3) * hexRadius;
        float hexHeight = 2.0f * hexRadius;
        float verticalSpacing = hexHeight * 0.75f;

        List<TerrainType> terrainDeck = GetStandardTerrainDeck();
        ShuffleDeck(terrainDeck);
        List<int> numberDeck = GetStandardNumberTokens();
        ShuffleDeck(numberDeck);

        int tileIndex = 0;
        int numberIndex = 0;

        for (int row = 0; row < rowLengths.Length; row++)
        {
            int numHexes = rowLengths[row];
            float xOffset = -(numHexes - 1) * hexWidth / 2.0f;

            for (int col = 0; col < numHexes; col++)
            {
                float xPos = xOffset + col * hexWidth;
                float zPos = -row * verticalSpacing;

                Vector3 hexPos = new Vector3(xPos, 0, zPos);
                GameObject hex = Instantiate(hexPrefab, hexPos, Quaternion.identity);
                hex.name = $"Hex_{row}_{col}";
                hex.transform.SetParent(this.transform);

                TerrainType type = terrainDeck[tileIndex];
                SetHexColor(hex, type);

                if (type != TerrainType.Desert)
                {
                    int num = numberDeck[numberIndex];
                    SpawnNumberToken(hex, num);
                    numberIndex++;
                }

                // 【追加】この六角形の6つの頂点にNodeを生成する
                GenerateIntersectionNodes(hexPos);

                tileIndex++;
            }
        }
    }

    // （以下の GetStandardTerrainDeck, GetStandardNumberTokens, ShuffleDeck, SetHexColor, SpawnNumberToken は前回と同じなので省略せずにそのまま残してください。文字数制限のため省略表示しています）
    List<TerrainType> GetStandardTerrainDeck() { /* 前回と同じ */ return new List<TerrainType> { TerrainType.Desert, TerrainType.Forest, TerrainType.Forest, TerrainType.Forest, TerrainType.Forest, TerrainType.Hill, TerrainType.Hill, TerrainType.Hill, TerrainType.Pasture, TerrainType.Pasture, TerrainType.Pasture, TerrainType.Pasture, TerrainType.Field, TerrainType.Field, TerrainType.Field, TerrainType.Field, TerrainType.Mountain, TerrainType.Mountain, TerrainType.Mountain }; }
    List<int> GetStandardNumberTokens() { /* 前回と同じ */ return new List<int> { 2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12 }; }
    void ShuffleDeck<T>(List<T> deck) { /* 前回と同じ */ for (int i = 0; i < deck.Count; i++) { T temp = deck[i]; int randomIndex = Random.Range(i, deck.Count); deck[i] = deck[randomIndex]; deck[randomIndex] = temp; } }
    void SetHexColor(GameObject hex, TerrainType type) { /* 前回と同じ */ Renderer renderer = hex.GetComponent<Renderer>(); if (renderer == null) return; switch (type) { case TerrainType.Desert: renderer.material.color = new Color(0.9f, 0.8f, 0.5f); break; case TerrainType.Forest: renderer.material.color = new Color(0.1f, 0.5f, 0.2f); break; case TerrainType.Hill: renderer.material.color = new Color(0.8f, 0.4f, 0.1f); break; case TerrainType.Pasture: renderer.material.color = new Color(0.5f, 0.8f, 0.2f); break; case TerrainType.Field: renderer.material.color = new Color(0.9f, 0.8f, 0.2f); break; case TerrainType.Mountain: renderer.material.color = new Color(0.5f, 0.5f, 0.5f); break; } }
    void SpawnNumberToken(GameObject hex, int number) { /* 前回と同じ */ Vector3 tokenPos = hex.transform.position + new Vector3(0, 0.05f, 0); GameObject token = Instantiate(numberTokenPrefab, tokenPos, Quaternion.identity); token.transform.SetParent(hex.transform); TextMeshPro textMeshPro = token.GetComponentInChildren<TextMeshPro>(); if (textMeshPro != null) { textMeshPro.text = number.ToString(); if (number == 6 || number == 8) { textMeshPro.color = Color.red; } else { textMeshPro.color = Color.black; } } }

    // 【新規追加】六角形の6つの頂点を計算してNodeを置くメソッド
    void GenerateIntersectionNodes(Vector3 hexCenter)
    {
        // 六角形の6つの頂点の角度（Pointy-top型は30度からスタート）
        for (int i = 0; i < 6; i++)
        {
            float angle_deg = 60 * i - 30;
            float angle_rad = Mathf.PI / 180 * angle_deg;

            // 頂点のX, Z座標を計算（少しだけ大きくしてタイルの外側に置く）
            float x = hexCenter.x + hexRadius * Mathf.Cos(angle_rad);
            float z = hexCenter.z + hexRadius * Mathf.Sin(angle_rad);
            Vector3 nodePos = new Vector3(x, 0.1f, z); // 0.1f浮かす

            // すでに同じ場所にNodeが存在するかチェックする（誤差0.1を許容）
            bool isDuplicate = false;
            foreach (Vector3 existingPos in createdNodes)
            {
                if (Vector3.Distance(existingPos, nodePos) < 0.1f)
                {
                    isDuplicate = true;
                    break;
                }
            }

            // 重複していなければNodeを生成
            if (!isDuplicate)
            {
                GameObject node = Instantiate(intersectionNodePrefab, nodePos, Quaternion.identity);
                node.transform.SetParent(nodesContainer);
                createdNodes.Add(nodePos); // 記録に追加
            }
        }
    }
}