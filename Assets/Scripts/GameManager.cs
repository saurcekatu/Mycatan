using UnityEngine;
using System.Collections.Generic;
using TMPro; // 【追加】UI（文字）を操作するために必要

public enum ResourceType { None, Wood, Brick, Wool, Grain, Ore }

// 【追加】発展カードの種類（騎士、勝利点、収穫、独占、街道建設）
public enum DevCardType { Knight, VictoryPoint, YearOfPlenty, Monopoly, RoadBuilding }



public class PlayerData
{
    public int victoryPoints = 0;
    
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

    [Header("盗賊システム")]
    public GameObject robberPrefab;
    private GameObject robberInstance; // 画面にいる盗賊の実体
    public HexTile currentRobberHex;   // 今盗賊がいるタイル
    public bool isWaitingForRobber = false; // 盗賊の移動先をクリック待ちか

    // 既存の変数の下あたりに追加
    [Header("発展カード")]
    public List<DevCardType> devCardDeck = new List<DevCardType>();

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
        
        Debug.Log("ターン終了！次は プレイヤー " + (currentPlayerIndex + 1) + " の番です。");
        UpdateUI(); // ターンが変わったので文字を更新
    }

    public void RollDice()
    {
        if (isGameOver) return;
        //盗賊待ちでサイコロをはじく
        if (isWaitingForRobber)
        {
            Debug.Log("【警告】先にタイルをクリックして、盗賊を移動させてください！");
            return; 
        }
        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        int total = dice1 + dice2;

        if (total == 7)
        {
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
            
            Debug.Log("===============================================");
            Debug.Log($"🎉【ゲームクリア】プレイヤー {playerIndex + 1} の勝利です！🎉");
            Debug.Log("===============================================");
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
        string uiText = $"【 プレイヤー {currentPlayerIndex + 1} のターン 】★現在: {vp}点 / 10点\n";
        uiText += $"木: {inv[ResourceType.Wood]}  |  ";
        uiText += $"レンガ: {inv[ResourceType.Brick]}  |  ";
        uiText += $"麦: {inv[ResourceType.Grain]}  |  ";
        uiText += $"羊: {inv[ResourceType.Wool]}  |  ";
        uiText += $"鉄: {inv[ResourceType.Ore]}";

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
            
            Debug.Log($"【強奪】プレイヤー {thief + 1} は、プレイヤー {victim + 1} から 『{stolen}』 を1つ奪った！");
            UpdateUI(); // 画面の数値を更新
        }
        else
        {
            Debug.Log($"プレイヤー {victim + 1} は資源を持っていなかった！");
        }
    }

    // 【新規追加】発展カードを購入する処理
    public void BuyDevelopmentCard()
    {
        if (devCardDeck.Count == 0)
        {
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
                Debug.Log($"プレイヤー {currentPlayerIndex + 1} は『勝利点』カードを引いた！(現在: {players[currentPlayerIndex].victoryPoints}点)");
            }
            else
            {
                Debug.Log($"プレイヤー {currentPlayerIndex + 1} は『{drawnCard}』カードを引いた！");
            }

            UpdateUI();
        }
        else
        {
            Debug.Log("資源が足りません！（発展カード：鉄1, 麦1, 羊1 が必要です）");
        }
    }
}