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
}