using UnityEngine;

public class GameManager : MonoBehaviour
{
    // 【魔法の1行】どこからでも「GameManager.Instance」でアクセスできるようになる（シングルトン）
    public static GameManager Instance;

    [Header("プレイヤーの色設定")]
    public Material[] playerMaterials; // 4人分の色を配列（リスト）で登録する枠
    
    [Header("現在の状態")]
    public int currentPlayerIndex = 0; // 0=赤, 1=青, 2=緑, 3=黄

    void Awake()
    {
        // ゲーム開始時に自分自身をInstanceに登録する
        if (Instance == null)
        {
            Instance = this;
        }
    }

    // --- 他のスクリプトから「今のプレイヤーの色」を聞かれたら返すメソッド ---
    public Material GetCurrentPlayerMaterial()
    {
        return playerMaterials[currentPlayerIndex];
    }

    // --- ターン終了処理 ---
    public void EndTurn()
    {
        currentPlayerIndex++;

        // もし4人目（インデックス3）の次に行ったら、1人目（インデックス0）に戻る
        if (currentPlayerIndex >= playerMaterials.Length)
        {
            currentPlayerIndex = 0;
        }

        Debug.Log("ターン終了！次は プレイヤー " + (currentPlayerIndex + 1) + " の番です。");
    }

    public void RollDice()
    {
        // 1〜6のサイコロを2つ振る
        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        int total = dice1 + dice2;

        Debug.Log("=========================");
        Debug.Log($"サイコロを振った！出目は 【 {total} 】 ({dice1} + {dice2})");

        if (total == 7)
        {
            Debug.Log("盗賊が出た！誰も資源を獲得できません。");
            return;
        }

        // ボード上のすべてのHexTileを探す
        HexTile[] allHexes = FindObjectsOfType<HexTile>();
        
        foreach (HexTile hex in allHexes)
        {
            // 出目が一致していて、砂漠ではない場合
            if (hex.number == total && hex.terrainType != TerrainType.Desert)
            {
                // そのHexに資源を配らせる
                hex.DistributeResources();
            }
        }
    }
}