using System.Collections.Generic;
using UnityEngine;

public class Intersection : MonoBehaviour
{
    // 誰の建物か（-1は空き地、0〜3は各プレイヤー）
    public int ownerID = -1; 
    private Renderer myRenderer;

    // --- グラフ（隣接リスト） ---
    public List<Road> connectedRoads = new List<Road>();
    public List<Intersection> adjacentNodes = new List<Intersection>();

    void Start()
    {
        myRenderer = GetComponent<Renderer>();
        BuildNetwork();
    }

    // 周囲のオブジェクトを自動検知してネットワークを構築する
    void BuildNetwork()
    {
        // 自分から半径0.35の範囲を検索し、繋がっている「道」をリストに登録
        Collider[] colliders = Physics.OverlapSphere(transform.position, 0.35f);
        foreach (Collider col in colliders)
        {
            Road road = col.GetComponent<Road>();
            if (road != null) connectedRoads.Add(road);
        }

        // 自分から半径0.6の範囲を検索し、隣の「交差点」をリストに登録
        Collider[] extendedCols = Physics.OverlapSphere(transform.position, 0.6f);
        foreach (Collider col in extendedCols)
        {
            Intersection node = col.GetComponent<Intersection>();
            if (node != null && node != this) adjacentNodes.Add(node);
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform == this.transform) TryBuildSettlement();
            }
        }
    }

    void TryBuildSettlement()
    {
        int currentPlayer = GameManager.Instance.currentPlayerIndex;

        // ルール1：すでに建物がある場所には建てられない
        if (ownerID != -1)
        {
            Debug.Log("すでに建物があります！");
            return;
        }

        // ルール2：距離ルール（隣の交差点に、誰かの村があったらNG）
        foreach (Intersection node in adjacentNodes)
        {
            if (node.ownerID != -1)
            {
                Debug.Log("近隣の村と近すぎます！（距離ルール違反）");
                return;
            }
        }

        /* 
         * ※本来はここに「自分の道が繋がっているか」のチェックも入りますが、
         * カタンの「ゲーム開始時の最初の2つの村」は道がなくても建てられるため、
         * テストプレイが詰まらないように今回は道チェックを省略しています。
         */

        // 全ルールをクリアしたら建設！
        ownerID = currentPlayer;
        myRenderer.material = GameManager.Instance.GetCurrentPlayerMaterial();
        transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        Debug.Log(gameObject.name + " に建設しました！");
    }
}