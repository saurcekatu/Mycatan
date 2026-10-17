using UnityEngine;
using UnityEngine.SceneManagement; // 【重要】シーン遷移を行うための魔法の呪文

public class TitleManager : MonoBehaviour
{
    // 「ゲームスタート」ボタンを押した時に呼ばれるメソッド
    public void StartGame()
    {
        // "MainScene" の部分には、Step 1で確認したゲーム本編のシーン名を入れてください
        SceneManager.LoadScene("MainScene"); 
    }

    // おまけ：「ゲーム終了」ボタン用（エディタ上では動きませんが、ビルド後に機能します）
    public void QuitGame()
    {
        Debug.Log("ゲームを終了します");
        Application.Quit();
    }
}