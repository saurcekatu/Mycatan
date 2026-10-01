using UnityEngine;

public class Intersection : MonoBehaviour
{
    [Header("建設後のマテリアル（色）")]
    

    private Renderer myRenderer;
    private bool isBuilt = false;

    void Start()
    {
        myRenderer = GetComponent<Renderer>();
    }

    // 毎フレーム、クリックされていないか監視する
    void Update()
    {
        // マウスの左クリック（またはスマホのタップ）が押された瞬間
        if (Input.GetMouseButtonDown(0))
        {
            // カメラから、クリックした画面の位置に向かって「見えない光線（Ray）」を飛ばす
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // 光線が何かにぶつかったら
            if (Physics.Raycast(ray, out hit))
            {
                // ぶつかった相手が「自分自身（この交差点）」だった場合のみ処理する
                if (hit.transform == this.transform)
                {
                    if (!isBuilt)
                    {
                        BuildSettlement();
                    }
                    else
                    {
                        Debug.Log("ここはすでに誰かの建物があります！");
                    }
                }
            }
        }
    }

    void BuildSettlement()
    {
        isBuilt = true;
        myRenderer.material = GameManager.Instance.GetCurrentPlayerMaterial();
        transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        Debug.Log(gameObject.name + " に開拓地を建設しました！");
    }
}