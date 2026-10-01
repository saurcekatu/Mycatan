using UnityEngine;

public class Intersection : MonoBehaviour
{
    [Header("建設後のマテリアル（色）")]
    public Material playerMaterial; // 誰が建てたか（とりあえず今回はPlayer1の赤をセット）

    private Renderer myRenderer;
    private bool isBuilt = false;   // すでに建物が置かれているかどうかの判定

    void Start()
    {
        // 自分の見た目（Renderer）を取得しておく
        myRenderer = GetComponent<Renderer>();
    }

    // マウスでクリック（iPadでタップ）された時に自動で呼ばれるメソッド
    void OnMouseDown()
    {
        // まだ誰も建物を建てていなければ
        if (!isBuilt)
        {
            BuildSettlement();
        }
        else
        {
            Debug.Log("ここはすでに誰かの建物があります！");
        }
    }

    void BuildSettlement()
    {
        isBuilt = true;

        // 色（マテリアル）をプレイヤーのものに変更する
        myRenderer.material = playerMaterial;

        // 【おまけ演出】開拓地っぽく見せるために、少しだけサイズを大きくする
        transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        // コンソールにログを出す
        Debug.Log(gameObject.name + " に開拓地を建設しました！");
    }
}