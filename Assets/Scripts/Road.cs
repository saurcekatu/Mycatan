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

        // ▼▼▼ 初期配置（Setup）フェーズの処理 ▼▼▼
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

            // 【カタンの重要ルール】今置いたばかりの村に接続しているかチェック！
            if (!connectedNodes.Contains(GameManager.Instance.lastPlacedSetupNode))
            {
                GameManager.Instance.ShowPopup("今配置したばかりの村に隣接する道を選んでください！");
                GameManager.Instance.PlaySound(GameManager.Instance.errorSound);
                return;
            }

            // コストを支払わず強制的に建築！
            ownerID = currentPlayer;
            myRenderer.material = GameManager.Instance.GetCurrentPlayerMaterial();
            GameManager.Instance.PlaySound(GameManager.Instance.buildRoadSound);
            GameManager.Instance.ShowPopup("初期配置の道を引きました！");
            
            // 最長交易路のチェック（念のため）
            GameManager.Instance.CheckLongestRoad(currentPlayer);

            // 道を引いたら次の人に手番を回す
            GameManager.Instance.AdvanceSetupTurn();
            return; 
        }
        else if (GameManager.Instance.currentState != GameState.MainAction)
        {
            GameManager.Instance.ShowPopup("今は建設できません！（先にサイコロを振ってください）");
            GameManager.Instance.PlaySound(GameManager.Instance.errorSound);
            return;
        }

        // --- ここから下は通常のゲーム中（MainAction）の処理（既存のまま） ---
        if (ownerID != -1) 
        { 
            GameManager.Instance.ShowPopup("すでに道があります！"); 
            Debug.Log("すでに道があります！"); 
            GameManager.Instance.PlaySound(GameManager.Instance.errorSound);
            return; 
        }

        bool isConnected = false;
        foreach (Intersection node in connectedNodes) { if (node.ownerID == currentPlayer) isConnected = true; }
        foreach (Road road in connectedRoads) { if (road.ownerID == currentPlayer) isConnected = true; }
        
        if (!isConnected) 
        { 
            GameManager.Instance.ShowPopup("自分の村や道に繋がっていません！"); 
            Debug.Log("自分の村や道に繋がっていません！"); 
            GameManager.Instance.PlaySound(GameManager.Instance.errorSound);
            return; 
        }

        if (!GameManager.Instance.TryPayRoadCost())
        {
            return; 
        }

        ownerID = currentPlayer;
        myRenderer.material = GameManager.Instance.GetCurrentPlayerMaterial();
        GameManager.Instance.ShowPopup("道を引きました！");
        Debug.Log(gameObject.name + " に道を引きました！");
        
        GameManager.Instance.CheckLongestRoad(currentPlayer);
    }
}