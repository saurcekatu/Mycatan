using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Intersection : MonoBehaviour
{
    // 誰の建物か（-1は空き地、0〜3は各プレイヤー）
    public int ownerID = -1; 
    public bool isCity = false; // ← 【追加】都市化されているかどうかのフラグ
    
    [Header("港の設定")]
    public ResourceType portType = ResourceType.None; // Noneなら港なし
    
    // 【追加】マップ上に表示する3Dテキスト
    public TextMeshPro portText;
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


    // 【新規追加】GameManagerから港を割り当てられた時の処理
    public void SetPort(ResourceType type)
    {
        portType = type;

        if (portText != null)
        {
            if (type == ResourceType.Any) portText.text = "3:1\n港";
            else portText.text = $"{type}\n2:1";
            
            // 見やすくするために文字を少し上に浮かせる
            portText.transform.localPosition = new Vector3(0, 0.5f, 0);
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
                
                GameManager.Instance.ShowPopup(gameObject.name + " を『都市』にアップグレードしました！");
                Debug.Log(gameObject.name + " を『都市』にアップグレードしました！");
            }
            return;
        }

        // ここから下は既存の「開拓地（村）を建てる」処理
        if (ownerID != -1) 
        { 
            GameManager.Instance.ShowPopup("すでに建物があります！"); 
            Debug.Log("すでに建物があります！"); 
            GameManager.Instance.PlaySound(GameManager.Instance.errorSound);
            return; 
        }
        
        foreach (Intersection node in adjacentNodes)
        {
            if (node.ownerID != -1) 
            { 
                GameManager.Instance.ShowPopup("近隣の村と近すぎます！（距離ルール違反）");
                Debug.Log("近隣の村と近すぎます！（距離ルール違反）");
                GameManager.Instance.PlaySound(GameManager.Instance.errorSound); 
                return; 
            }
        }

        if (!GameManager.Instance.TryPaySettlementCost()) return;

        ownerID = currentPlayer;
        myRenderer.material = GameManager.Instance.GetCurrentPlayerMaterial();
        transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        GameManager.Instance.ShowPopup(gameObject.name + " に建設しました！");
        Debug.Log(gameObject.name + " に建設しました！");
        GameManager.Instance.AddVictoryPoint(currentPlayer);

        // もしこの交差点が「港」だったら、プレイヤーの港フラグをONにする
        if (portType == ResourceType.Any)
        {
            GameManager.Instance.players[currentPlayer].has3to1Port = true;
            GameManager.Instance.ShowPopup($" プレイヤー {currentPlayer + 1} が『3:1 汎用港』を獲得しました！");
            Debug.Log($" プレイヤー {currentPlayer + 1} が『3:1 汎用港』を獲得しました！");
        }
        else if (portType != ResourceType.None)
        {
            GameManager.Instance.players[currentPlayer].hasPort[portType] = true;
            GameManager.Instance.ShowPopup($" プレイヤー {currentPlayer + 1} が『{portType}』の 2:1 港を獲得しました！");
            Debug.Log($" プレイヤー {currentPlayer + 1} が『{portType}』の 2:1 港を獲得しました！");
        }

    }
}