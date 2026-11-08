using UnityEngine.EventSystems;
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
        if (EventSystem.current.IsPointerOverGameObject())
        {
            return; 
        }
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

        // ========================================================
        // ▼▼▼ 1. 初期配置（Setup）フェーズの処理 ▼▼▼
        // ========================================================
        if (GameManager.Instance.currentState == GameState.Setup)
        {
            if (!GameManager.Instance.isPlacingSetupRoad)
            {
                GameManager.Instance.ShowPopup("まずは村を配置してください！");
                GameManager.Instance.PlaySound(GameManager.Instance.errorSound);
                return;
            }
            if (ownerID != -1) 
            { 
                GameManager.Instance.ShowPopup("すでに道があります！"); 
                GameManager.Instance.PlaySound(GameManager.Instance.errorSound);
                return; 
            }
            if (!connectedNodes.Contains(GameManager.Instance.lastPlacedSetupNode))
            {
                GameManager.Instance.ShowPopup("今配置したばかりの村に隣接する道を選んでください！");
                GameManager.Instance.PlaySound(GameManager.Instance.errorSound);
                return;
            }

            // 初期配置なので【無条件で（カードも資源も使わず）】建築！
            ownerID = currentPlayer;
            myRenderer.material = GameManager.Instance.GetCurrentPlayerMaterial();
            GameManager.Instance.PlaySound(GameManager.Instance.buildRoadSound);
            GameManager.Instance.ShowPopup("初期配置の道を引きました！");
            
            GameManager.Instance.players[currentPlayer].remainingRoads--; // コマを1つ減らす
            GameManager.Instance.CheckLongestRoad(currentPlayer);
            GameManager.Instance.AdvanceSetupTurn();
            return; 
        }
        else if (GameManager.Instance.currentState != GameState.MainAction)
        {
            GameManager.Instance.ShowPopup("今は建設できません！（先にサイコロを振ってください）");
            GameManager.Instance.PlaySound(GameManager.Instance.errorSound);
            return;
        }

        // ========================================================
        // ▼▼▼ 2. 通常のゲーム中（MainAction）の処理 ▼▼▼
        // ========================================================
        if (ownerID != -1) 
        { 
            GameManager.Instance.ShowPopup("すでに道があります！"); 
            GameManager.Instance.PlaySound(GameManager.Instance.errorSound);
            return; 
        }

        bool isConnected = false;
        foreach (Intersection node in connectedNodes) { if (node.ownerID == currentPlayer) isConnected = true; }
        foreach (Road road in connectedRoads) { if (road.ownerID == currentPlayer) isConnected = true; }
        
        if (!isConnected) 
        { 
            GameManager.Instance.ShowPopup("自分の村や道に繋がっていません！"); 
            GameManager.Instance.PlaySound(GameManager.Instance.errorSound);
            return; 
        }

        // --------------------------------------------------------
        // 💡 質問いただいたのはここです！ここで「カード」か「資源」か判定します
        // --------------------------------------------------------
        if (GameManager.Instance.freeRoadsToBuild > 0)
        {
            // パターンA：街道建設カードの効果中なら、無料で建設！
            GameManager.Instance.freeRoadsToBuild--;
            GameManager.Instance.players[currentPlayer].remainingRoads--; // コマを減らす
            GameManager.Instance.PlaySound(GameManager.Instance.buildRoadSound);
            GameManager.Instance.UpdateUI();
        }
        else
        {
            // パターンB：カード効果がないなら、通常通り資源を支払う
            // （※ TryPayRoadCost() の中でコマを減らす処理は実行されます）
            if (!GameManager.Instance.TryPayRoadCost())
            {
                return; // 支払い失敗で中断
            }
        }
        // --------------------------------------------------------

        // 決済（カード消費 or 資源支払い）に成功したので、道を自分の色にする！
        ownerID = currentPlayer;
        myRenderer.material = GameManager.Instance.GetCurrentPlayerMaterial();
        GameManager.Instance.ShowPopup("道を引きました！");
        
        GameManager.Instance.CheckLongestRoad(currentPlayer);
    }
}