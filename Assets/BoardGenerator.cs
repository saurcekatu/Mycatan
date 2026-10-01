using System.Collections.Generic;
using UnityEngine;
using TMPro;

// 1. 地形（資源）の種類を定義
public enum TerrainType
{
    Desert,   // 砂漠（資源なし）
    Forest,   // 森（木）
    Hill,     // 丘（レンガ）
    Pasture,  // 牧草地（羊）
    Field,    // 畑（麦）
    Mountain  // 山（鉄）
}

public class BoardGenerator : MonoBehaviour
{
    [Header("六角形のプレハブ")]
    public GameObject hexPrefab;

    [Header("数字トークンのプレハブ")]
    public GameObject numberTokenPrefab;

    [Header("六角形のサイズ（中心から頂点までの距離）")]
    public float hexRadius = 0.52f;

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

        // 2. 地形のリスト（山札）を作成してシャッフルする
        List<TerrainType> terrainDeck = GetStandardTerrainDeck();
        ShuffleDeck(terrainDeck);

        //数字トークンの山札
        List<int> numberDeck = GetStandardNumberTokens();
        ShuffleDeck(numberDeck);

        int tileIndex = 0; // 山札から引くためのインデックス
        int numberIndex = 0;

        for (int row = 0; row < rowLengths.Length; row++)
        {
            int numHexes = rowLengths[row];
            float xOffset = -(numHexes - 1) * hexWidth / 2.0f;

            for (int col = 0; col < numHexes; col++)
            {
                float xPos = xOffset + col * hexWidth;
                float zPos = -row * verticalSpacing;

                Vector3 pos = new Vector3(xPos, 0, zPos);
                GameObject hex = Instantiate(hexPrefab, pos, Quaternion.identity);
                hex.name = $"Hex_{row}_{col}";
                hex.transform.SetParent(this.transform);

                // 3. タイルに地形データを割り当てて、色を変える
                TerrainType type = terrainDeck[tileIndex];
                SetHexColor(hex, type);

                //砂漠以外ならnumトークン配置
                if (type !=TerrainType.Desert)
                {
                    int num = numberDeck[numberIndex];
                    SpawnNumberToken(hex, num);
                    numberIndex++;
                }

                tileIndex++;
            }
        }
    }

    // --- カタン標準の19枚の地形リストを作成するメソッド ---
    List<TerrainType> GetStandardTerrainDeck()
    {
        List<TerrainType> deck = new List<TerrainType>();
        deck.Add(TerrainType.Desert);
        for (int i = 0; i < 4; i++) deck.Add(TerrainType.Forest);
        for (int i = 0; i < 3; i++) deck.Add(TerrainType.Hill);
        for (int i = 0; i < 4; i++) deck.Add(TerrainType.Pasture);
        for (int i = 0; i < 4; i++) deck.Add(TerrainType.Field);
        for (int i = 0; i < 3; i++) deck.Add(TerrainType.Mountain);
        return deck;
    }

    List<int> GetStandardNumberTokens()
    {
        return new List<int> {2,3,3,4,4,5,5,6,6,8,8,9,9,10,10,11,11,12};
    }

    // --- リストをランダムにシャッフルするメソッド ---
    void ShuffleDeck<T>(List<T> deck)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            T temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
    }

    // --- 地形に合わせてタイルの色を変更するメソッド ---
    void SetHexColor(GameObject hex, TerrainType type)
    {
        // タイルの見た目（Renderer）を取得
        Renderer renderer = hex.GetComponent<Renderer>();
        if (renderer == null) return;

        // 地形に合わせて色（RGB値）を変える
        switch (type)
        {
            case TerrainType.Desert:   renderer.material.color = new Color(0.9f, 0.8f, 0.5f); break; // 砂色
            case TerrainType.Forest:   renderer.material.color = new Color(0.1f, 0.5f, 0.2f); break; // 濃い緑
            case TerrainType.Hill:     renderer.material.color = new Color(0.8f, 0.4f, 0.1f); break; // 赤茶色
            case TerrainType.Pasture:  renderer.material.color = new Color(0.5f, 0.8f, 0.2f); break; // 黄緑
            case TerrainType.Field:    renderer.material.color = new Color(0.9f, 0.8f, 0.2f); break; // 黄金色
            case TerrainType.Mountain: renderer.material.color = new Color(0.5f, 0.5f, 0.5f); break; // 灰色
        }
    }

    void SpawnNumberToken(GameObject hex, int number)
    {
        Vector3 tokenPos = hex.transform.position + new Vector3(0, 0.05f, 0);
        GameObject token = Instantiate(numberTokenPrefab, tokenPos, Quaternion.identity);
        token.transform.SetParent(hex.transform);

        TextMeshPro textMeshPro = token.GetComponentInChildren<TextMeshPro>();
        if (textMeshPro != null)
        {
            textMeshPro.text = number.ToString();

            if (number == 6 || number == 8)
            {
                textMeshPro.color = Color.red;
            }
            else
            {
                textMeshPro.color = Color.black;
            }
        }
    }
}