using UnityEngine;
using System.Collections.Generic;
using TMPro; // 【追加】UI（文字）を操作するために必要
using System.Collections;

public enum ResourceType { None, Wood, Brick, Wool, Grain, Ore, Any }

// 【追加】発展カードの種類（騎士、勝利点、収穫、独占、街道建設）
public enum DevCardType { Knight, VictoryPoint, YearOfPlenty, Monopoly, RoadBuilding }



public class PlayerData
{
    public int victoryPoints = 0;
    public int playedKnights = 0;
    
    public Dictionary<ResourceType, int> inventory = new Dictionary<ResourceType, int>()
    {
        { ResourceType.Wood, 0 },
        { ResourceType.Brick, 0 },
        { ResourceType.Wool, 0 },
        { ResourceType.Grain, 0 },
        { ResourceType.Ore, 0 }
    };
    //発展カードの所持数
    public Dictionary<DevCardType, int> devCards = new Dictionary<DevCardType, int>()
    {
        { DevCardType.Knight, 0 },
        { DevCardType.VictoryPoint, 0 },
        { DevCardType.YearOfPlenty, 0 },
        { DevCardType.Monopoly, 0 },
        { DevCardType.RoadBuilding, 0 }
    };

    // どの資源の「2:1港」を持っているかを記憶する辞書
    public Dictionary<ResourceType, bool> hasPort = new Dictionary<ResourceType, bool>()
    {
        { ResourceType.Wood, false },
        { ResourceType.Brick, false },
        { ResourceType.Wool, false },
        { ResourceType.Grain, false },
        { ResourceType.Ore, false }
    };

    public bool has3to1Port = false; // 3:1港（一般港）を持っているか
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("ゲーム状態")]
    public bool isGameOver = false; // ゲーム終了判定フラグ

    [Header("プレイヤーの色設定")]
    public Material[] playerMaterials;
    
    [Header("現在の状態")]
    public int currentPlayerIndex = 0;

    public PlayerData[] players = new PlayerData[4];

    // 【追加】画面に表示するUIテキストの枠
    [Header("UI設定")]
    public TextMeshProUGUI resourceText; 
    public TextMeshProUGUI popupText; // ポップアップ用のテキスト枠
    private Coroutine popupCoroutine; // 実行中のコルーチンを記憶する変数

    [Header("トレードUI")]
    public TMP_Dropdown payDropdown; // 払う資源を選ぶドロップダウン
    public TMP_Dropdown getDropdown; // もらう資源を選ぶドロップダウン

    [Header("盗賊システム")]
    public GameObject robberPrefab;
    private GameObject robberInstance; // 画面にいる盗賊の実体
    public HexTile currentRobberHex;   // 今盗賊がいるタイル
    public bool isWaitingForRobber = false; // 盗賊の移動先をクリック待ちか

    // 既存の変数の下あたりに追加
    [Header("発展カード")]
    public List<DevCardType> devCardDeck = new List<DevCardType>();

    [Header("特別称号")]
    public int playerWithLargestArmy = -1;  // 現在の「最大騎士力」保持者(-1は誰もいない)
    public int currentLargestArmyCount = 2;

    public int playerWithLongestRoad = -1;
    public int currentLongestRoadCount = 4; // 奪うために必要な最低本数（最初は5本から）

    [Header("港の生成設定")]
    public int maxPortsCount = 9; // カタンの基本ルールは9個
    public int portSpacing = 2;   // 1=真隣には置かない、2=間に1マス以上空ける
    
    void Awake()
    {
        if (Instance == null) Instance = this;
        for (int i = 0; i < 4; i++) players[i] = new PlayerData();
        
        InitializeDevCardDeck();
    }

    // 【新規追加】山札を構築してシャッフルするメソッド
    void InitializeDevCardDeck()
    {
        // カタンの標準的な構成（全25枚）
        for(int i=0; i<14; i++) devCardDeck.Add(DevCardType.Knight);
        for(int i=0; i<5; i++) devCardDeck.Add(DevCardType.VictoryPoint);
        for(int i=0; i<2; i++) devCardDeck.Add(DevCardType.YearOfPlenty);
        for(int i=0; i<2; i++) devCardDeck.Add(DevCardType.Monopoly);
        for(int i=0; i<2; i++) devCardDeck.Add(DevCardType.RoadBuilding);

        // データサイエンスの定番「Fisher-Yates シャッフル」でランダムに並び替える
        for (int i = 0; i < devCardDeck.Count; i++)
        {
            DevCardType temp = devCardDeck[i];
            int randomIndex = Random.Range(i, devCardDeck.Count);
            devCardDeck[i] = devCardDeck[randomIndex];
            devCardDeck[randomIndex] = temp;
        }
    }
    
    private HexTile[] allHexes;

    void Start()
    {
        UpdateUI(); 
        // ゲーム開始時に1回だけ、ボード上の全タイルを探して記憶しておく
        allHexes = FindObjectsByType<HexTile>(FindObjectsSortMode.None);

        // 【追加】ボードが生成された直後に、砂漠を探して盗賊を配置する
        Invoke("InitializeRobber", 0.1f);
        Invoke("InitializePorts", 0.1f);

    }

    // 【新規追加】港をランダムに配置する
    // 【改良版】港を間隔をあけてランダムに配置する
    void InitializePorts()
    {
        Intersection[] allNodes = FindObjectsByType<Intersection>(FindObjectsSortMode.None);
        List<Intersection> edgeNodes = new List<Intersection>();

        // 1. マップの「端（海沿い）」にある交差点を探す
        foreach (Intersection node in allNodes)
        {
            if (node.adjacentNodes.Count < 3) edgeNodes.Add(node);
        }

        // 2. カタン公式の港セットを作る
        List<ResourceType> portDeck = new List<ResourceType>()
        {
            ResourceType.Any, ResourceType.Any, ResourceType.Any, ResourceType.Any, 
            ResourceType.Wood, ResourceType.Brick, ResourceType.Wool, ResourceType.Grain, ResourceType.Ore 
        };

        // 【追加】港の種類をシャッフルする（間隔設定によって9個全部置けない場合でも、種類が偏らないようにするため）
        for (int i = 0; i < portDeck.Count; i++)
        {
            ResourceType temp = portDeck[i];
            int r = Random.Range(i, portDeck.Count);
            portDeck[i] = portDeck[r];
            portDeck[r] = temp;
        }

        int placedCount = 0;

        // 3. ランダムな端の交差点に港を割り当てる
        foreach (ResourceType port in portDeck)
        {
            // 最大数に達したか、もう間隔をあけて置ける場所がなくなったら終了
            if (placedCount >= maxPortsCount || edgeNodes.Count == 0) break;

            // ランダムに場所を選ぶ
            int randomIndex = Random.Range(0, edgeNodes.Count);
            Intersection targetNode = edgeNodes[randomIndex];

            targetNode.SetPort(port);
            placedCount++;

            // 【追加】配置した交差点と、その「周囲の交差点」を候補から除外する
            RemoveNodeAndNeighbors(targetNode, edgeNodes, portSpacing);
        }

        
    }

    // 【新規追加】指定した交差点から、distance（距離）の範囲内にある交差点を候補リストから消す
    void RemoveNodeAndNeighbors(Intersection node, List<Intersection> availableNodes, int distance)
    {
        // リストの中にいれば削除する
        if (availableNodes.Contains(node)) 
        {
            availableNodes.Remove(node);
        }

        // まだ指定距離に達していなければ、隣の交差点も連鎖的に削除する（再帰処理）
        if (distance > 0)
        {
            foreach (Intersection adj in node.adjacentNodes)
            {
                RemoveNodeAndNeighbors(adj, availableNodes, distance - 1);
            }
        }
    }

    // 【新規メソッド追加】砂漠を探して初期配置する
    void InitializeRobber()
    {
        HexTile[] allHexes = FindObjectsOfType<HexTile>();
        foreach(HexTile hex in allHexes) 
        {
            if (hex.terrainType == TerrainType.Desert) 
            {
                currentRobberHex = hex;
                hex.hasRobber = true;
                // タイルの少し上に盗賊を出現させる
                Vector3 pos = hex.transform.position + new Vector3(0, 0.2f, 0);
                robberInstance = Instantiate(robberPrefab, pos, Quaternion.identity);
                break;
            }
        }
    }

    public Material GetCurrentPlayerMaterial()
    {
        return playerMaterials[currentPlayerIndex];
    }

    public void EndTurn()
    {
        if (isGameOver) return;
        currentPlayerIndex++;
        if (currentPlayerIndex >= playerMaterials.Length) currentPlayerIndex = 0;
        
        ShowPopup("ターン終了！次は プレイヤー " + (currentPlayerIndex + 1) + " の番です。");
        Debug.Log("ターン終了！次は プレイヤー " + (currentPlayerIndex + 1) + " の番です。");

        UpdateUI(); // ターンが変わったので文字を更新
    }

    public void RollDice()
    {
        if (isGameOver) return;
        //盗賊待ちでサイコロをはじく
        if (isWaitingForRobber)
        {
            ShowPopup("【警告】先にタイルをクリックして、盗賊を移動させてください！");
            Debug.Log("【警告】先にタイルをクリックして、盗賊を移動させてください！");
            return; 
        }
        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        int total = dice1 + dice2;

        if (total == 7)
        {
            ShowPopup("盗賊が出た！移動させるタイルをクリックしてください。");
            Debug.Log("盗賊が出た！移動させるタイルをクリックしてください。");
            isWaitingForRobber = true; // クリック待ちモードON
            return;
        }

        HexTile[] allHexes = FindObjectsByType<HexTile>(FindObjectsSortMode.None);        foreach (HexTile hex in allHexes)
        {
            if (hex.number == total && hex.terrainType != TerrainType.Desert)
            {
                hex.DistributeResources();
            }
        }
    }

    public void AddResource(int playerIndex, TerrainType terrain, int amount)
    {
        ResourceType res = ConvertTerrainToResource(terrain);
        if (res == ResourceType.None) return;

        players[playerIndex].inventory[res] += amount;
        UpdateUI(); // 資源が増えたので文字を更新
        ShowPopup($"プレイヤー {playerIndex + 1} が {res} を獲得！ (現在の所持数: {players[playerIndex].inventory[res]} 個)");
        Debug.Log($"プレイヤー {playerIndex + 1} が {res} を獲得！ (現在の所持数: {players[playerIndex].inventory[res]} 個)");
    }

    private ResourceType ConvertTerrainToResource(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.Forest: return ResourceType.Wood;
            case TerrainType.Hill: return ResourceType.Brick;
            case TerrainType.Pasture: return ResourceType.Wool;
            case TerrainType.Field: return ResourceType.Grain;
            case TerrainType.Mountain: return ResourceType.Ore;
            default: return ResourceType.None;
        }
    }

    public bool TryPayRoadCost()
    {
        Dictionary<ResourceType, int> inv = players[currentPlayerIndex].inventory;
        if (inv[ResourceType.Wood] >= 1 && inv[ResourceType.Brick] >= 1)
        {
            inv[ResourceType.Wood] -= 1;
            inv[ResourceType.Brick] -= 1;
            UpdateUI(); // 資源を支払ったので文字を更新
            return true;
        }
        ShowPopup("資源が足りません！（道：木1, レンガ1 が必要です）");
        Debug.Log("資源が足りません！（道：木1, レンガ1 が必要です）");
        return false;
    }

    public bool TryPaySettlementCost()
    {
        Dictionary<ResourceType, int> inv = players[currentPlayerIndex].inventory;
        if (inv[ResourceType.Wood] >= 1 && inv[ResourceType.Brick] >= 1 &&
            inv[ResourceType.Grain] >= 1 && inv[ResourceType.Wool] >= 1)
        {
            inv[ResourceType.Wood] -= 1;
            inv[ResourceType.Brick] -= 1;
            inv[ResourceType.Grain] -= 1;
            inv[ResourceType.Wool] -= 1;
            UpdateUI(); // 資源を支払ったので文字を更新
            return true;
        }
        ShowPopup("資源が足りません！（開拓地：木1, レンガ1, 麦1, 羊1 が必要です）");
        Debug.Log("資源が足りません！（開拓地：木1, レンガ1, 麦1, 羊1 が必要です）");
        return false; 
    }

    // 【追加】都市（City）のコストが支払えるか確認し、可能なら支払う
    public bool TryPayCityCost()
    {
        Dictionary<ResourceType, int> inv = players[currentPlayerIndex].inventory;

        // 鉄3、麦2 があるかチェック
        if (inv[ResourceType.Ore] >= 3 && inv[ResourceType.Grain] >= 2)
        {
            inv[ResourceType.Ore] -= 3;
            inv[ResourceType.Grain] -= 2;
            UpdateUI(); 
            return true;
        }

        ShowPopup("資源が足りません！（都市：鉄3, 麦2 が必要です）");
        Debug.Log("資源が足りません！（都市：鉄3, 麦2 が必要です）");
        return false;
    }

    // 正しい位置に配置されたチートメソッド
    public void CheatGiveResources() 
    {
        int p = currentPlayerIndex;
        players[p].inventory[ResourceType.Wood] += 10;
        players[p].inventory[ResourceType.Brick] += 10;
        players[p].inventory[ResourceType.Grain] += 10;
        players[p].inventory[ResourceType.Wool] += 10;
        players[p].inventory[ResourceType.Ore] += 10;

        UpdateUI(); // チートで資源が増えたので文字を更新
        ShowPopup("チートにより資源を獲得しました。");
        Debug.Log("チートにより資源を獲得しました。");
    }

    // 【新規追加】勝利点を追加し、10点に達したかチェックするメソッド
    public void AddVictoryPoint(int playerIndex)
    {
        players[playerIndex].victoryPoints++; // 1点追加
        UpdateUI();

        // 10点に到達したかチェック
        if (players[playerIndex].victoryPoints >= 10)
        {
            isGameOver = true; // ゲーム終了状態にする
            
            ShowPopup("===============================================");
            ShowPopup($"【ゲームクリア】プレイヤー {playerIndex + 1} の勝利です！");
            ShowPopup("===============================================");
            Debug.Log($"【ゲームクリア】プレイヤー {playerIndex + 1} の勝利です！");
        }
    }

    // 【新規】画面の文字（UI）を現在の状況に合わせて書き換えるメソッド
    public void UpdateUI()
    {
        // テキスト枠が設定されていなければ無視する
        if (resourceText == null) return;

        Dictionary<ResourceType, int> inv = players[currentPlayerIndex].inventory;
        int vp = players[currentPlayerIndex].victoryPoints; // 現在の得点を取得
        // 画面に表示する文字列を組み立てる
        string armyText = (playerWithLargestArmy == currentPlayerIndex) ? " 最大騎士力" : "";
        string roadText = (playerWithLongestRoad == currentPlayerIndex) ? " 最長交易路" : "";

        string uiText = $"【 プレイヤー {currentPlayerIndex + 1} のターン 】★現在: {vp}点 / 10点 {armyText}{roadText}\n";
        uiText += $"木: {inv[ResourceType.Wood]}  |  ";
        uiText += $"レンガ: {inv[ResourceType.Brick]}  |  ";
        uiText += $"麦: {inv[ResourceType.Grain]}  |  ";
        uiText += $"羊: {inv[ResourceType.Wool]}  |  ";
        uiText += $"鉄: {inv[ResourceType.Ore]}";

        int knights = players[currentPlayerIndex].devCards[DevCardType.Knight];
        int playedKnights = players[currentPlayerIndex].playedKnights; 
        uiText += $"\n【手札】騎士カード: {knights} 枚 (使用済み: {playedKnights} 枚)";
        // 実際の画面の文字を書き換える
        resourceText.text = uiText;
    }

    // 【新規追加】毎フレーム、クリックを監視する
    void Update()
    {
        // 盗賊移動待ちの時に、画面をクリックしたら
        if (isWaitingForRobber && Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // クリックしたのが HexTile だったら
                HexTile clickedHex = hit.transform.GetComponent<HexTile>();
                if (clickedHex != null)
                {
                    MoveRobber(clickedHex);
                }
            }
        }
    }

    // 【新規追加】盗賊を新しいタイルに移動させる処理
    void MoveRobber(HexTile targetHex)
    {
        // ルール：今いる場所には移動できない
        if (targetHex == currentRobberHex)
        {
            ShowPopup("同じ場所には移動できません！別のタイルを選んでください。");
            Debug.Log("同じ場所には移動できません！別のタイルを選んでください。");
            return;
        }

        // 古いタイルの盗賊フラグを解除
        if (currentRobberHex != null) currentRobberHex.hasRobber = false;

        // 新しいタイルの盗賊フラグをON
        currentRobberHex = targetHex;
        currentRobberHex.hasRobber = true;

        // コマを物理的に移動させる
        robberInstance.transform.position = targetHex.transform.position + new Vector3(0, 0.2f, 0);

        isWaitingForRobber = false; // クリック待ちモード終了
        ShowPopup("盗賊を移動しました！");
        Debug.Log("盗賊を移動しました！");

        List<int> victims = new List<int>(); // 被害者リスト

        // 移動先のタイルの周囲にある交差点をすべてチェック
        foreach (Intersection node in targetHex.adjacentNodes)
        {
            // 誰かの村があり、かつ「今のプレイヤー（自分）」ではない場合
            if (node.ownerID != -1 && node.ownerID != currentPlayerIndex)
            {
                // まだリストに入っていなければ被害者として追加
                if (!victims.Contains(node.ownerID)) victims.Add(node.ownerID);
            }
        }

        // 被害者が1人以上いる場合
        if (victims.Count > 0)
        {
            // 誰から奪うかランダムで1人決める（※本来はプレイヤーが選びますが今回は自動化）
            int targetVictim = victims[Random.Range(0, victims.Count)];
            StealResource(currentPlayerIndex, targetVictim);
        }
        else
        {
            ShowPopup("周囲に他のプレイヤーの村がないため、誰も襲えませんでした。");
            Debug.Log("周囲に他のプレイヤーの村がないため、誰も襲えませんでした。");
        }
        // ▲▲▲ ここまで追加 ▲▲▲
    }

    // 【新規追加】実際にAさんがBさんから資源を1つ奪うメソッド
    void StealResource(int thief, int victim)
    {
        PlayerData vData = players[victim];
        List<ResourceType> availableResources = new List<ResourceType>();

        // 被害者のインベントリの中身をすべて「くじ引きの箱」に入れる
        foreach (var item in vData.inventory)
        {
            for (int i = 0; i < item.Value; i++)
            {
                availableResources.Add(item.Key); // 例：木が3つなら、Woodを3枚入れる
            }
        }

        if (availableResources.Count > 0)
        {
            // くじ引きの箱からランダムに1枚引く
            ResourceType stolen = availableResources[Random.Range(0, availableResources.Count)];
            
            // 被害者から1つ減らし、泥棒に1つ足す
            vData.inventory[stolen]--;
            players[thief].inventory[stolen]++;
            
            ShowPopup($"【強奪】プレイヤー {thief + 1} は、プレイヤー {victim + 1} から 『{stolen}』 を1つ奪った！");
            Debug.Log($"【強奪】プレイヤー {thief + 1} は、プレイヤー {victim + 1} から 『{stolen}』 を1つ奪った！");
            UpdateUI(); // 画面の数値を更新
        }
        else
        {
            ShowPopup($"プレイヤー {victim + 1} は資源を持っていなかった！");
            Debug.Log($"プレイヤー {victim + 1} は資源を持っていなかった！");
        }
    }

    // 【新規追加】発展カードを購入する処理
    public void BuyDevelopmentCard()
    {
        if (devCardDeck.Count == 0)
        {
            ShowPopup("発展カードの山札がもうありません！");
            Debug.Log("発展カードの山札がもうありません！");
            return;
        }

        Dictionary<ResourceType, int> inv = players[currentPlayerIndex].inventory;

        // 鉄1、麦1、羊1があるかチェック
        if (inv[ResourceType.Ore] >= 1 && inv[ResourceType.Grain] >= 1 && inv[ResourceType.Wool] >= 1)
        {
            // 支払い
            inv[ResourceType.Ore] -= 1;
            inv[ResourceType.Grain] -= 1;
            inv[ResourceType.Wool] -= 1;

            // 山札の先頭（0番目）からカードを引き、山札から削除する
            DevCardType drawnCard = devCardDeck[0];
            devCardDeck.RemoveAt(0);

            // 引いたカードを手札に追加
            players[currentPlayerIndex].devCards[drawnCard]++;
            
            // 勝利点カードなら、引いた瞬間に点数を追加してしまう（今回はシンプル化のため）
            if (drawnCard == DevCardType.VictoryPoint)
            {
                AddVictoryPoint(currentPlayerIndex);
                ShowPopup($"プレイヤー {currentPlayerIndex + 1} は『勝利点』カードを引いた！(現在: {players[currentPlayerIndex].victoryPoints}点)");
                Debug.Log($"プレイヤー {currentPlayerIndex + 1} は『勝利点』カードを引いた！(現在: {players[currentPlayerIndex].victoryPoints}点)");
            }
            else
            {
                ShowPopup($"プレイヤー {currentPlayerIndex + 1} は『{drawnCard}』カードを引いた！");
                Debug.Log($"プレイヤー {currentPlayerIndex + 1} は『{drawnCard}』カードを引いた！");
            }

            UpdateUI();
        }
        else
        {
            ShowPopup("資源が足りません！（発展カード：鉄1, 麦1, 羊1 が必要です）");
            Debug.Log("資源が足りません！（発展カード：鉄1, 麦1, 羊1 が必要です）");
        }
    }

    // 【新規追加】騎士カードを使用する処理
    public void UseKnightCard()
    {
        // 既にサイコロで7を出して移動待ちの場合は使えないようにする
        if (isWaitingForRobber) 
        {
            ShowPopup("現在はすでに盗賊の移動待ちです！");
            Debug.Log("現在はすでに盗賊の移動待ちです！");
            return;
        }

        // 現在のプレイヤーが騎士カードを1枚以上持っているかチェック
        if (players[currentPlayerIndex].devCards[DevCardType.Knight] > 0)
        {
            // 騎士カードを1枚消費する
            players[currentPlayerIndex].devCards[DevCardType.Knight]--;

            // ▼▼▼ 魔法の1行！これだけで盗賊モードに突入します ▼▼▼
            isWaitingForRobber = true; 
            players[currentPlayerIndex].playedKnights++; // 使った回数を増やす
            CheckLargestArmy(currentPlayerIndex);
            ShowPopup($"プレイヤー {currentPlayerIndex + 1} が【騎士カード】を使用しました！移動させるタイルをクリックしてください。");
            Debug.Log($"プレイヤー {currentPlayerIndex + 1} が【騎士カード】を使用しました！移動させるタイルをクリックしてください。");
            UpdateUI();
        }
        else
        {
            ShowPopup("騎士カードを持っていません！");
            Debug.Log("騎士カードを持っていません！");
        }
    }

    // 【新規追加】最大騎士力の条件を満たしたかチェックする
    public void CheckLargestArmy(int playerIndex)
    {
        int playedCount = players[playerIndex].playedKnights;

        // 現在の最大記録を「単独で」上回った場合（同数は奪えない）
        if (playedCount > currentLargestArmyCount)
        {
            currentLargestArmyCount = playedCount; // 記録更新

            // もし称号の持ち主が自分以外に変わったら
            if (playerWithLargestArmy != playerIndex)
            {
                // 前の持ち主から2点没収する
                if (playerWithLargestArmy != -1)
                {
                    players[playerWithLargestArmy].victoryPoints -= 2;
                    ShowPopup($"プレイヤー {playerWithLargestArmy + 1} は「最大騎士力」を失った…(-2点)");
                    Debug.Log($"プレイヤー {playerWithLargestArmy + 1} は「最大騎士力」を失った…(-2点)");
                }

                // 新しい持ち主に設定して2点付与する
                playerWithLargestArmy = playerIndex;
                players[playerIndex].victoryPoints += 2;
                
                ShowPopup($" プレイヤー {playerIndex + 1} が「最大騎士力」を獲得しました！(+2点)");
                Debug.Log($" プレイヤー {playerIndex + 1} が「最大騎士力」を獲得しました！(+2点)");

                // ここで10点に達する可能性があるので勝利判定
                if (players[playerIndex].victoryPoints >= 10)
                {
                    isGameOver = true;
                    ShowPopup($"【ゲームクリア】プレイヤー {playerIndex + 1} の勝利です！");
                    Debug.Log($"【ゲームクリア】プレイヤー {playerIndex + 1} の勝利です！");
                }
            }
        }
    }

    // 【新規追加】指定したプレイヤーの最も長い道の長さを計算する
    public int CalculateLongestRoad(int playerIndex)
    {
        int maxLength = 0;
        Road[] allRoads = FindObjectsByType<Road>(FindObjectsSortMode.None);
        List<Intersection> startingNodes = new List<Intersection>();
        
        // 1. 自分の道が繋がっている交差点（Node）をすべて抽出する
        foreach(Road r in allRoads)
        {
            if (r.ownerID == playerIndex)
            {
                foreach(Intersection node in r.connectedNodes)
                {
                    if (!startingNodes.Contains(node)) startingNodes.Add(node);
                }
            }
        }

        // 2. すべての交差点をスタート地点として、深さ優先探索（DFS）を実行
        foreach(Intersection startNode in startingNodes)
        {
            List<Road> visited = new List<Road>();
            int length = DFS(startNode, visited, playerIndex);
            if (length > maxLength) maxLength = length;
        }
        return maxLength;
    }

    // 【新規追加】深さ優先探索（DFS）の実体
    private int DFS(Intersection currentNode, List<Road> visitedRoads, int playerIndex)
    {
        // カタン公式ルール：相手の村や都市がある交差点は、道が分断される！
        if (currentNode.ownerID != -1 && currentNode.ownerID != playerIndex) return 0;

        int maxDepth = 0;

        // 今いる交差点から伸びるすべての道を調べる
        foreach (Road road in currentNode.connectedRoads)
        {
            // 自分の道であり、かつ今回のルートでまだ通っていない道なら進む
            if (road.ownerID == playerIndex && !visitedRoads.Contains(road))
            {
                visitedRoads.Add(road); // 通った記録をつける

                // 道の「反対側」の交差点を見つける
                Intersection nextNode = null;
                foreach (Intersection n in road.connectedNodes)
                {
                    if (n != currentNode) nextNode = n;
                }

                // 次の交差点へ再帰的に進む
                int depth = 1;
                if (nextNode != null)
                {
                    depth += DFS(nextNode, visitedRoads, playerIndex);
                }

                if (depth > maxDepth) maxDepth = depth;

                // バックトラック：別ルートも探せるように通った記録を消す
                visitedRoads.Remove(road); 
            }
        }
        return maxDepth;
    }

    // 【新規追加】最長交易路の条件を満たしたかチェックする
    public void CheckLongestRoad(int playerIndex)
    {
        int myRoadLength = CalculateLongestRoad(playerIndex);
        
        // ログで現在の道の長さを確認できるようにする
        Debug.Log($"プレイヤー {playerIndex + 1} の現在の道: {myRoadLength}本");

        if (myRoadLength > currentLongestRoadCount)
        {
            currentLongestRoadCount = myRoadLength; // 記録更新

            if (playerWithLongestRoad != playerIndex)
            {
                if (playerWithLongestRoad != -1)
                {
                    players[playerWithLongestRoad].victoryPoints -= 2;
                    ShowPopup($"プレイヤー {playerWithLongestRoad + 1} は「最長交易路」を失った…(-2点)");
                    Debug.Log($"プレイヤー {playerWithLongestRoad + 1} は「最長交易路」を失った…(-2点)");
                }

                playerWithLongestRoad = playerIndex;
                players[playerIndex].victoryPoints += 2;
                
                ShowPopup($" プレイヤー {playerIndex + 1} が「最長交易路」を獲得しました！(+2点)");
                Debug.Log($" プレイヤー {playerIndex + 1} が「最長交易路」を獲得しました！(+2点)");

                if (players[playerIndex].victoryPoints >= 10)
                {
                    isGameOver = true;
                    ShowPopup($"【ゲームクリア】プレイヤー {playerIndex + 1} の勝利です！");
                    Debug.Log($"【ゲームクリア】プレイヤー {playerIndex + 1} の勝利です！");
                }
            }
        }
        UpdateUI();
    }

    // 【新規追加】資源のトレード（交換）を実行する
     public void TradeResources(ResourceType payRes, ResourceType getRes)
    {
        PlayerData pData = players[currentPlayerIndex];

        // 【確実なコスト計算アルゴリズム】
        int cost = 4; // 基本は 4:1

        if (pData.has3to1Port) 
        {
            cost = 3; // 3:1港を持っていれば 3 に割引
        }

        if (pData.hasPort.ContainsKey(payRes) && pData.hasPort[payRes]) 
        {
            cost = 2; // さらに、払う資源の専門港(2:1)を持っていれば 2 に上書き（最安値）
        }

        // 取引の実行
        if (pData.inventory[payRes] >= cost)
        {
            pData.inventory[payRes] -= cost;
            pData.inventory[getRes] += 1;

            ShowPopup($" プレイヤー {currentPlayerIndex + 1} は『{payRes}』を{cost}個払って、『{getRes}』を1個手に入れた！(レート {cost}:1)");
            Debug.Log($" プレイヤー {currentPlayerIndex + 1} は『{payRes}』を{cost}個払って、『{getRes}』を1個手に入れた！(レート {cost}:1)");
            UpdateUI();
        }
        else
        {
            ShowPopup($"トレード失敗：『{payRes}』が足りません！（現在のあなたの最安レートでは {cost}個 必要です）");
            Debug.Log($"トレード失敗：『{payRes}』が足りません！（現在のあなたの最安レートでは {cost}個 必要です）");
        }
    }

    // 【テスト用のボタン呼び出しメソッド】
    // ※Unityのボタン機能は2つの引数(payとget)を直接渡せないので、お試し用のメソッドを用意します。
    //public void TestTradeWoodForOre()
    // {
    //     // 「木」を払って「鉄」をもらうテスト
    //     TradeResources(ResourceType.Wood, ResourceType.Ore);
    // }

    // 【新規追加】画面のドロップダウンの選択を読み取ってトレードを実行する
    public void ExecuteTradeFromUI()
    {
        // もしドロップダウンが設定されていなければエラーを出して止める
        if (payDropdown == null || getDropdown == null)
        {
            Debug.LogError("ドロップダウンが設定されていません！");
            return;
        }

        // ドロップダウンの選択番号（0, 1, 2, 3, 4）を、ResourceType（1, 2, 3, 4, 5）に変換する
        // ※ 0:木, 1:レンガ, 2:羊, 3:麦, 4:鉄 の順番で設定する前提です
        ResourceType payRes = (ResourceType)(payDropdown.value + 1);
        ResourceType getRes = (ResourceType)(getDropdown.value + 1);

        // 同じ資源同士を選んでいた場合は弾く
        if (payRes == getRes)
        {
            ShowPopup("同じ資源同士は交換できません！別の資源を選んでください。");
            Debug.Log("同じ資源同士は交換できません！別の資源を選んでください。");
            return;
        }

        // すでに作ってある「確実なトレード処理」に、選んだ資源を渡して実行！
        TradeResources(payRes, getRes);
    }

    // 【新規追加】画面の中央にメッセージを表示し、2秒後に消す処理
    public void ShowPopup(string message)
    {
        if (popupText == null) return;

        // もし既にポップアップが表示中なら、一旦リセットする（連続で押した時のバグ防止）
        if (popupCoroutine != null) StopCoroutine(popupCoroutine);

        // 新しいポップアップ処理（コルーチン）を開始する
        popupCoroutine = StartCoroutine(PopupRoutine(message));
    }

    // コルーチンの本体（時間を操れる特殊なメソッド）
    private IEnumerator PopupRoutine(string message)
    {
        popupText.text = message;
        popupText.gameObject.SetActive(true); // 文字を表示！

        yield return new WaitForSeconds(2.0f); // 【魔法の1行】ここで2秒間待機する

        popupText.gameObject.SetActive(false); // 2秒経ったら文字を隠す！
    }
}