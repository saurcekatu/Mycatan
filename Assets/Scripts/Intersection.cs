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

        if (ownerID != -1) { Debug.Log("すでに建物があります！"); return; }
        
        foreach (Intersection node in adjacentNodes)
        {
            if (node.ownerID != -1) { Debug.Log("近隣の村と近すぎます！（距離ルール違反）"); return; }
        }

        // ▼▼▼ ここから追加・変更 ▼▼▼
        // ルール3：コストの支払い（GameManagerに決済を依頼する）
        if (!GameManager.Instance.TryPaySettlementCost())
        {
            // 支払いに失敗した場合は、ここで処理を中断する（建てられない）
            return;
        }

        // 決済に成功したら建設！
        ownerID = currentPlayer;
        myRenderer.material = GameManager.Instance.GetCurrentPlayerMaterial();
        transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        Debug.Log(gameObject.name + " に建設しました！");
        // ▲▲▲ ここまで ▲▲▲
    }
}