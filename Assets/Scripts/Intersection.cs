using System.Collections.Generic;
using UnityEngine;

public class Intersection : MonoBehaviour
{
    // 誰の建物か（-1は空き地、0〜3は各プレイヤー）
    public int ownerID = -1; 
    public bool isCity = false; // ← 【追加】都市化されているかどうかのフラグ
    
    [Header("港の設定")]
    public ResourceType portType = ResourceType.None; // Noneなら港なし
    
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

        // 【追加】ルールA：すでに自分の村なら、都市へのアップグレードを試みる
        if (ownerID == currentPlayer && !isCity)
        {
            if (GameManager.Instance.TryPayCityCost())
            {
                isCity = true; // 都市フラグをON
                
                // 見た目を大きくする（本来はモデルを差し替えますが、今回はサイズで表現）
                transform.localScale = new Vector3(0.5f, 0.6f, 0.5f); 
                
                // すでに村で1点もらっているので、「+1点」して合計2点にする
                GameManager.Instance.AddVictoryPoint(currentPlayer); 
                
                Debug.Log(gameObject.name + " を『都市』にアップグレードしました！");
            }
            return;
        }

        // ここから下は既存の「開拓地（村）を建てる」処理
        if (ownerID != -1) { Debug.Log("すでに建物があります！"); return; }
        
        foreach (Intersection node in adjacentNodes)
        {
            if (node.ownerID != -1) { Debug.Log("近隣の村と近すぎます！（距離ルール違反）"); return; }
        }

        if (!GameManager.Instance.TryPaySettlementCost()) return;

        ownerID = currentPlayer;
        myRenderer.material = GameManager.Instance.GetCurrentPlayerMaterial();
        transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        Debug.Log(gameObject.name + " に建設しました！");
        GameManager.Instance.AddVictoryPoint(currentPlayer);

        // もしこの交差点が「港」だったら、プレイヤーの港フラグをONにする
        if (portType != ResourceType.None)
        {
            GameManager.Instance.players[currentPlayer].hasPort[portType] = true;
            Debug.Log($" プレイヤー {currentPlayer + 1} が『{portType}』の 2:1 港を獲得しました！");
        }

    }
}