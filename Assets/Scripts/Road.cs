using System.Collections.Generic;
using UnityEngine;

public class Road : MonoBehaviour
{
    public int ownerID = -1;
    private Renderer myRenderer;

    // --- グラフ（隣接リスト） ---
    public List<Road> connectedRoads = new List<Road>();
    public List<Intersection> connectedNodes = new List<Intersection>();

    void Start()
    {
        myRenderer = GetComponent<Renderer>();
        BuildNetwork();
    }

    void BuildNetwork()
    {
        // 道の両端にある「交差点」と、繋がっている「他の道」を登録
        Collider[] colliders = Physics.OverlapSphere(transform.position, 0.35f);
        foreach (Collider col in colliders)
        {
            Intersection node = col.GetComponent<Intersection>();
            if (node != null) connectedNodes.Add(node);

            Road road = col.GetComponent<Road>();
            if (road != null && road != this) connectedRoads.Add(road);
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform == this.transform) TryBuildRoad();
            }
        }
    }

    void TryBuildRoad()
    {
        int currentPlayer = GameManager.Instance.currentPlayerIndex;

        if (ownerID != -1) { Debug.Log("すでに道があります！"); return; }

        bool isConnected = false;
        foreach (Intersection node in connectedNodes) { if (node.ownerID == currentPlayer) isConnected = true; }
        foreach (Road road in connectedRoads) { if (road.ownerID == currentPlayer) isConnected = true; }
        
        if (!isConnected) { Debug.Log("自分の村や道に繋がっていません！"); return; }

        // ▼▼▼ ここから追加・変更 ▼▼▼
        // ルール3：コストの支払い（GameManagerに決済を依頼する）
        if (!GameManager.Instance.TryPayRoadCost())
        {
            return; // 支払い失敗で中断
        }

        // 決済に成功したら建設！
        ownerID = currentPlayer;
        myRenderer.material = GameManager.Instance.GetCurrentPlayerMaterial();
        Debug.Log(gameObject.name + " に道を引きました！");
        // ▲▲▲ ここまで ▲▲▲
    }
}