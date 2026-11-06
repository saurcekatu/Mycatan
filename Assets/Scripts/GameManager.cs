using UnityEngine;
using System.Collections.Generic;
using TMPro; // 【追加】UI（文字）を操作するために必要
using System.Collections;
using UnityEngine.SceneManagement;

public enum ResourceType { None, Wood, Brick, Wool, Grain, Ore, Any }

// 【追加】発展カードの種類（騎士、勝利点、収穫、独占、街道建設）
public enum DevCardType { Knight, VictoryPoint, YearOfPlenty, Monopoly, RoadBuilding }

// ▼▼▼ 新規追加：ゲームの進行状態 ▼▼▼
public enum GameState 
{ 
    Setup,          // 初期配置フェーズ（次回実装します）
    WaitingForRoll, // サイコロを振る前
    MainAction,     // サイコロを振った後（家を建てたりトレードできる）
    Trading,
    Discarding,
    RobberMoving,   // 盗賊の移動待ち（7が出た時や騎士を使った時）
    GameOver        // ゲーム終了
}


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
    public GameState currentState = GameState.Setup;
    public bool hasRollDice = false;

    public PlayerData[] players = new PlayerData[4];
    public int setupRound = 1; // 1週目(1->2->3->4) か 2週目(4->3->2->1) か
    public bool isPlacingSetupRoad = false; // 村を置いた後、道を置くのを待っているか
    public Intersection lastPlacedSetupNode = null;
    // 【追加】画面に表示するUIテキストの枠
    [Header("UI設定")]
    public TextMeshProUGUI resourceText; 
    public TextMeshProUGUI popupText; // ポップアップ用のテキスト枠
    private Coroutine popupCoroutine; // 実行中のコルーチンを記憶する変数

    [Header("トレードUI")]
    public TMP_Dropdown payDropdown; // 払う資源を選ぶドロップダウン
    public TMP_Dropdown getDropdown; // もらう資源を選ぶドロップダウン

    [Header("対人トレードUI")]
    public GameObject tradePanel;      // トレード画面全体のパネル（透明度80%の背景）
    public GameObject offerUI;         // 「提案を作る」部分のグループ
    public GameObject approveUI;       // 「承認を待つ」部分のグループ
    public TextMeshProUGUI approveText;// 「〇〇を渡すので〇〇をください」という確認テキスト

    public TMP_Dropdown targetPlayerDropdown; // 誰と取引するか選ぶドロップダウン
    public TextMeshProUGUI offerSummaryText; // 【新規】現在の提案内容を文字で表示する用

    // （1=木, 2=レンガ, 3=羊, 4=麦, 5=鉄）
    private int[] tradeGive = new int[6]; 
    private int[] tradeGet = new int[6];
    // ▼ 今回のトレードの「約束の内容」を一時的に覚えておく変数 ▼
    private int tradeTargetPlayer = -1;
    private ResourceType tradePayRes;
    private ResourceType tradeGetRes;

    public TextMeshProUGUI targetInventoryText;

    [Header("バースト（資源半減）UI")]
    public GameObject discardPanel;           // バースト画面全体のパネル
    public TextMeshProUGUI discardInstructionText; // 「プレイヤーX：あと〇枚選んでください」の案内
    public TextMeshProUGUI discardSummaryText;     // 現在選んでいる資源と手持ちの表示

    private Queue<int> burstPlayersQueue = new Queue<int>(); // バーストした人を順番に並べる列
    private int currentBurstPlayer = -1;      // 今まさに捨てる資源を選んでいるプレイヤー
    private int requiredDiscardCount = 0;     // 捨てなければならない合計枚数
    private int[] discardAmounts = new int[6];// 各資源を何枚捨てるか（1=木 ～ 5=鉄）

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
        
    [Header("リザルト画面")]
    public GameObject resultPanel; // ゲーム終了時に表示する画面全体    

    [Header("サウンド設定")]
    public AudioSource audioSource;
    public AudioClip errorSound;          // エラー音

    // ▼ 建築音を3種類に分割 ▼
    public AudioClip buildRoadSound;       // 道の建築音
    public AudioClip buildSettlementSound; // 開拓地の建築音
    public AudioClip buildCitySound;       // 都市の建築音

    // ▼ サイコロ音を配列化（何種類でも登録可能に） ▼
    public AudioClip[] diceSounds;         

    public AudioClip tradeSound;          // 取引音
    public AudioClip buttonClickSound;    // 【追加】ボタンクリック音


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
        currentState = GameState.Setup;
        setupRound = 1;
        isPlacingSetupRoad = false;
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
        HexTile[] allHexes = FindObjectsByType<HexTile>(FindObjectsSortMode.None);
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
        if (currentState != GameState.MainAction)
        {
            ShowPopup("まだターンを終了できません！（先にサイコロを振ってください）");
            PlaySound(errorSound);
            return;
        }
        currentPlayerIndex++;
        if (currentPlayerIndex >= playerMaterials.Length) currentPlayerIndex = 0;
        currentState = GameState.WaitingForRoll;
        hasRollDice = false;
        ShowPopup("ターン終了！次は プレイヤー " + (currentPlayerIndex + 1) + " の番です。");
        Debug.Log("ターン終了！次は プレイヤー " + (currentPlayerIndex + 1) + " の番です。");

        UpdateUI(); // ターンが変わったので文字を更新
    }

    // 【新規追加】初期配置フェーズのターン送り
    public void AdvanceSetupTurn()
    {
        isPlacingSetupRoad = false; // 次の人はまた「村」からスタート

        if (setupRound == 1)
        {
            if (currentPlayerIndex < players.Length - 1)
            {
                currentPlayerIndex++; // 1->2->3->4 と進む
            }
            else
            {
                setupRound = 2; // 4番手のまま2週目へ突入
            }
        }
        else if (setupRound == 2)
        {
            if (currentPlayerIndex > 0)
            {
                currentPlayerIndex--; // 4->3->2->1 と戻る
            }
            else
            {
                // 1番手まで戻ってきたら初期配置終了！いよいよゲームスタート
                currentState = GameState.WaitingForRoll;
                ShowPopup("初期配置が完了しました！プレイヤー 1 はサイコロを振ってください。");
                UpdateUI();
                return;
            }
        }

        UpdateUI();
        ShowPopup($"プレイヤー {currentPlayerIndex + 1} の番です。まずは村を配置してください。");
    }
    public void RollDice()
    {
        if (isGameOver) return;
        //盗賊待ちでサイコロをはじく
        if (currentState != GameState.WaitingForRoll)
        {
            ShowPopup("今はサイコロを振れません！（すでに振ったか、他のアクション中です）");
            PlaySound(errorSound);            
            return; 
        }

        hasRollDice = true;

        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        int total = dice1 + dice2;

        // もしサイコロ音が1つ以上登録されていれば、ランダムに1つ選んで鳴らす
        if (diceSounds.Length > 0)
        {
            int randomIndex = Random.Range(0, diceSounds.Length);
            PlaySound(diceSounds[randomIndex]);
        }

        if (total == 7)
        {
            // バースト判定
            burstPlayersQueue.Clear();

            for (int i = 0; i < players.Length; i++)
            {
                int totalCards = 0;
                foreach (var count in players[i].inventory.Values) totalCards += count;

                // 手札が8枚以上のプレイヤーを待ち行列（Queue）に追加
                if (totalCards >= 8)
                {
                    burstPlayersQueue.Enqueue(i);
                }
            }

            // バーストした人が1人でもいれば、廃棄フェーズへ移行
            if (burstPlayersQueue.Count > 0)
            {
                currentState = GameState.Discarding;
                ProcessNextBurstPlayer();
            }
            else
            {
                // 誰もバーストしていなければ、そのまま盗賊の移動へ
                ShowPopup("7が出ました！盗賊を移動させるタイルをクリックしてください。");
                Debug.Log("7が出ました！盗賊を移動させるタイルをクリックしてください。");
                isWaitingForRobber = true;
                currentState = GameState.RobberMoving;
            }
            UpdateUI();
            return;
        }

        HexTile[] allHexes = FindObjectsByType<HexTile>(FindObjectsSortMode.None);        foreach (HexTile hex in allHexes)
        {
            if (hex.number == total && hex.terrainType != TerrainType.Desert)
            {
                hex.DistributeResources();
            }
        }
        currentState = GameState.MainAction;
        UpdateUI();
    }

    public void GiveInitialResources(Intersection node, int playerIndex)
    {
        HexTile[] hexes = FindObjectsByType<HexTile>(FindObjectsSortMode.None);
        foreach (HexTile hex in hexes)
        {
            if (hex.terrainType != TerrainType.Desert && hex.adjacentNodes.Contains(node))
            {
                AddResource(playerIndex, hex.terrainType, 1);
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
        if (currentState != GameState.MainAction)
        {
            ShowPopup("このアクションはサイコロを振った後にしかできません！");
            PlaySound(errorSound);
            return false; // ※ TradeResources や BuyDevelopmentCard の場合は return; だけ
        }
        Dictionary<ResourceType, int> inv = players[currentPlayerIndex].inventory;
        if (inv[ResourceType.Wood] >= 1 && inv[ResourceType.Brick] >= 1)
        {
            inv[ResourceType.Wood] -= 1;
            inv[ResourceType.Brick] -= 1;
            UpdateUI(); // 資源を支払ったので文字を更新
            PlaySound(buildRoadSound);
            return true;
        }
        ShowPopup("資源が足りません！（道：木1, レンガ1 が必要です）");
        Debug.Log("資源が足りません！（道：木1, レンガ1 が必要です）");
        PlaySound(errorSound);
        return false;
    }

    public bool TryPaySettlementCost()
    {
        if (currentState != GameState.MainAction)
        {
            ShowPopup("このアクションはサイコロを振った後にしかできません！");
            PlaySound(errorSound);
            return false; // ※ TradeResources や BuyDevelopmentCard の場合は return; だけ
        }
        Dictionary<ResourceType, int> inv = players[currentPlayerIndex].inventory;
        if (inv[ResourceType.Wood] >= 1 && inv[ResourceType.Brick] >= 1 &&
            inv[ResourceType.Grain] >= 1 && inv[ResourceType.Wool] >= 1)
        {
            inv[ResourceType.Wood] -= 1;
            inv[ResourceType.Brick] -= 1;
            inv[ResourceType.Grain] -= 1;
            inv[ResourceType.Wool] -= 1;
            UpdateUI(); // 資源を支払ったので文字を更新
            PlaySound(buildSettlementSound);
            return true;
        }
        ShowPopup("資源が足りません！（開拓地：木1, レンガ1, 麦1, 羊1 が必要です）");
        Debug.Log("資源が足りません！（開拓地：木1, レンガ1, 麦1, 羊1 が必要です）");
        PlaySound(errorSound);
        return false; 
    }

    // 【追加】都市（City）のコストが支払えるか確認し、可能なら支払う
    public bool TryPayCityCost()
    {
        if (currentState != GameState.MainAction)
        {
            ShowPopup("このアクションはサイコロを振った後にしかできません！");
            PlaySound(errorSound);
            return false; // ※ TradeResources や BuyDevelopmentCard の場合は return; だけ
        }
        Dictionary<ResourceType, int> inv = players[currentPlayerIndex].inventory;

        // 鉄3、麦2 があるかチェック
        if (inv[ResourceType.Ore] >= 3 && inv[ResourceType.Grain] >= 2)
        {
            inv[ResourceType.Ore] -= 3;
            inv[ResourceType.Grain] -= 2;
            UpdateUI();
            PlaySound(buildCitySound); 
            return true;
        }

        ShowPopup("資源が足りません！（都市：鉄3, 麦2 が必要です）");
        Debug.Log("資源が足りません！（都市：鉄3, 麦2 が必要です）");
        PlaySound(errorSound);
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
            
            if (resultPanel != null)
            {
                resultPanel.SetActive(true); 
            }

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
        string stateText = "";
        if (currentState == GameState.Setup)
        {
            stateText = isPlacingSetupRoad ? $"【初期配置({setupRound}巡目)：道を置いてください】" : $"【初期配置({setupRound}巡目)：村を置いてください】";
        }
        else if (currentState == GameState.WaitingForRoll) stateText = "【サイコロを振ってください】";
        else if (currentState == GameState.MainAction) stateText = "【建設・交易フェーズ】";
        else if (currentState == GameState.Discarding) stateText = "【バースト：資源廃棄待ち】";
        string uiText = $"【 プレイヤー {currentPlayerIndex + 1} のターン 】{stateText}\n★現在: {vp}点 / 10点 {armyText}{roadText}\n";
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
            PlaySound(errorSound);
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

        if (hasRollDice)
        {
            currentState = GameState.MainAction;
        }
        else
        {
            currentState = GameState.WaitingForRoll;
        }
        UpdateUI();
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
        if (currentState != GameState.MainAction)
        {
            ShowPopup("このアクションはサイコロを振った後にしかできません！");
            PlaySound(errorSound);
            return; // ※ TradeResources や BuyDevelopmentCard の場合は return; だけ
        }
        if (devCardDeck.Count == 0)
        {
            ShowPopup("発展カードの山札がもうありません！");
            Debug.Log("発展カードの山札がもうありません！");
            PlaySound(errorSound);
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
            PlaySound(errorSound);
        }
    }

    // 【新規追加】騎士カードを使用する処理
    public void UseKnightCard()
    {
        // 既にサイコロで7を出して移動待ちの場合は使えないようにする
        if (currentState == GameState.RobberMoving || isWaitingForRobber) 
        {
            ShowPopup("現在はすでに盗賊の移動待ちです！");
            Debug.Log("現在はすでに盗賊の移動待ちです！");
            PlaySound(errorSound);
            return;
        }

        // 現在のプレイヤーが騎士カードを1枚以上持っているかチェック
        if (players[currentPlayerIndex].devCards[DevCardType.Knight] > 0)
        {
            // 騎士カードを1枚消費する
            players[currentPlayerIndex].devCards[DevCardType.Knight]--;

            // ▼▼▼ 魔法の1行！これだけで盗賊モードに突入します ▼▼▼
            currentState = GameState.RobberMoving;
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
            PlaySound(errorSound);
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
                    if (resultPanel != null)
                    {
                        resultPanel.SetActive(true); 
                    }
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
                    if (resultPanel != null)
                    {
                        resultPanel.SetActive(true); 
                    }
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
        if (currentState != GameState.MainAction)
        {
            ShowPopup("このアクションはサイコロを振った後にしかできません！");
            PlaySound(errorSound);
            return; // ※ TradeResources や BuyDevelopmentCard の場合は return; だけ
        }
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
            PlaySound(tradeSound);
            UpdateUI();
        }
        else
        {
            ShowPopup($"トレード失敗：『{payRes}』が足りません！（現在のあなたの最安レートでは {cost}個 必要です）");
            Debug.Log($"トレード失敗：『{payRes}』が足りません！（現在のあなたの最安レートでは {cost}個 必要です）");
            PlaySound(errorSound);
        }
    }

   
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
            PlaySound(errorSound);
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

    // 【新規追加】トレード画面を開く（メインアクション中のみ可能）
    // ▼▼▼ トレード画面を開く処理の修正 ▼▼▼
    public void OpenTradeWindow()
    {
        if (currentState != GameState.MainAction)
                {
                    ShowPopup("このアクションはサイコロを振った後にしかできません！");
                    PlaySound(errorSound);
                    return; 
                }
        currentState = GameState.Trading;
        tradePanel.SetActive(true);       
        offerUI.SetActive(true);          
        approveUI.SetActive(false);       
        
        // 提案内容をリセット
        tradeGive = new int[6];
        tradeGet = new int[6];
        UpdateOfferSummary(); // 画面の文字を更新
        OnTargetPlayerChanged();
    }
    public void OnTargetPlayerChanged()
    {
        int target = targetPlayerDropdown.value;
        if (target == currentPlayerIndex)
        {
            targetInventoryText.text = "（自分自身です）";
            return;
        }

        // 相手の資源をテキスト化
        string invStr = $"【プレイヤー {target + 1} の手持ち】\n";
        for (int i = 1; i <= 5; i++)
        {
            int count = players[target].inventory[(ResourceType)i];
            if (count > 0) invStr += $"{(ResourceType)i}: {count}個\n";
        }
        targetInventoryText.text = invStr;
    }
    // ▼▼▼ 数の増減メソッド（新規追加） ▼▼▼
    // resType には 1(木) ～ 5(鉄) の数字を渡す
    public void AddTradeGive(int resType)
    {
        // 自分の所持数を超えて提案できないようにする
        if (players[currentPlayerIndex].inventory[(ResourceType)resType] > tradeGive[resType])
        {
            tradeGive[resType]++;
            UpdateOfferSummary();
        }
        else PlaySound(errorSound);
    }
    public void RemoveTradeGive(int resType)
    {
        if (tradeGive[resType] > 0) { tradeGive[resType]--; UpdateOfferSummary(); }
    }
    public void AddTradeGet(int resType)
    {
        tradeGet[resType]++; UpdateOfferSummary();
    }
    public void RemoveTradeGet(int resType)
    {
        if (tradeGet[resType] > 0) { tradeGet[resType]--; UpdateOfferSummary(); }
    }

    // 現在の提案内容をテキストにまとめる
    private void UpdateOfferSummary()
    {
        string giveStr = "", getStr = "";
        for (int i = 1; i <= 5; i++)
        {
            if (tradeGive[i] > 0) giveStr += $"{(ResourceType)i} x{tradeGive[i]} ";
            if (tradeGet[i] > 0) getStr += $"{(ResourceType)i} x{tradeGet[i]} ";
        }
        if (giveStr == "") giveStr = "なし";
        if (getStr == "") getStr = "なし";

        if (offerSummaryText != null)
        {
            offerSummaryText.text = $"【出す資源】\n{giveStr}\n\n【欲しい資源】\n{getStr}";
        }
    }

    // 【新規追加】トレードをキャンセルして画面を閉じる
    public void CancelTrade()
    {
        currentState = GameState.MainAction; // 状態を元に戻す
        tradePanel.SetActive(false);         // パネルを隠す
        PlaySound(buttonClickSound);
    }
    // 【新規追加】選んだ条件で相手にオファーを出す
    public void SendTradeOffer()
    {
        tradeTargetPlayer = targetPlayerDropdown.value; 

        if (tradeTargetPlayer == currentPlayerIndex)
        {
            ShowPopup("自分自身とはトレードできません！");
            PlaySound(errorSound); return;
        }
        for (int i = 1; i <= 5; i++)
        {
            if (tradeGive[i] > 0 && tradeGet[i] > 0)
            {
                ShowPopup($"同じ資源（{(ResourceType)i}）を同時に出す・貰うことはできません！");
                PlaySound(errorSound);
                return;
            }
        }
        // 提案が空っぽでないかチェック
        int totalGive = 0, totalGet = 0;
        for (int i = 1; i <= 5; i++) { totalGive += tradeGive[i]; totalGet += tradeGet[i]; }
        if (totalGive == 0 || totalGet == 0)
        {
            ShowPopup("出す資源と欲しい資源をそれぞれ1つ以上設定してください！");
            PlaySound(errorSound); return;
        }

        PlaySound(buttonClickSound);
        offerUI.SetActive(false);
        approveUI.SetActive(true);

        // 相手へのメッセージを作成（UpdateOfferSummaryの文字列を再利用）
        approveText.text = $"【プレイヤー {tradeTargetPlayer + 1} へ】\n" +
                           $"以下のトレードを承認しますか？\n\n" +
                           $"{offerSummaryText.text}"; 
    }

    public void AcceptTrade()
    {
        // 相手が要求された資源をすべて持っているか確認
        for (int i = 1; i <= 5; i++)
        {
            if (players[tradeTargetPlayer].inventory[(ResourceType)i] < tradeGet[i])
            {
                ShowPopup($"プレイヤー {tradeTargetPlayer + 1} は「{(ResourceType)i}」が足りません！");
                PlaySound(errorSound);
                DeclineTrade();
                return;
            }
        }

        // トレード成立！（ループで全種類の資源を足し引き）
        for (int i = 1; i <= 5; i++)
        {
            players[currentPlayerIndex].inventory[(ResourceType)i] -= tradeGive[i];
            players[currentPlayerIndex].inventory[(ResourceType)i] += tradeGet[i];
            
            players[tradeTargetPlayer].inventory[(ResourceType)i] -= tradeGet[i];
            players[tradeTargetPlayer].inventory[(ResourceType)i] += tradeGive[i];
        }

        PlaySound(tradeSound);
        ShowPopup("トレード成立！");
        UpdateUI();
        CancelTrade(); 
    }
    
    // 【新規追加】相手が「拒否（NO）」を押した時
    public void DeclineTrade()
    {
        ShowPopup("トレードが拒否（またはキャンセル）されました。");
        PlaySound(errorSound);
        
        // 提案画面に戻す（条件を変えて再提案できるようにする）
        approveUI.SetActive(false);
        offerUI.SetActive(true);
    }

    // 【新規追加】タイトル画面に戻るメソッド
    public void ReturnToTitle()
    {
        SceneManager.LoadScene("TitleScene");
    }

    public void PlaySound(AudioClip clip)
    {
        // 音声データがセットされていれば、音が重なっても途切れない「PlayOneShot」で鳴らす
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // 【新規追加】ボタンが押された時にUIから呼び出すメソッド
    public void PlayClickSound()
    {
        PlaySound(buttonClickSound);
    }

    // =========================================================
    // バーストシステム 
    // =========================================================

    // 次のバースト対象者の画面を開く
    private void ProcessNextBurstPlayer()
    {
        if (burstPlayersQueue.Count > 0)
        {
            currentBurstPlayer = burstPlayersQueue.Dequeue(); // 列の先頭から1人取り出す

            int totalCards = 0;
            foreach (var count in players[currentBurstPlayer].inventory.Values) totalCards += count;

            requiredDiscardCount = totalCards / 2; // 切り捨てで半分（例：8枚なら4枚、9枚なら4枚）
            discardAmounts = new int[6];           // 選択状態をリセット

            discardPanel.SetActive(true);
            UpdateDiscardUI();
            ShowPopup($"バースト！プレイヤー {currentBurstPlayer + 1} は資源を {requiredDiscardCount} 枚捨ててください。");
        }
        else
        {
            // 全員の廃棄が終わったらパネルを閉じて盗賊移動へ！
            discardPanel.SetActive(false);
            currentState = GameState.RobberMoving;
            isWaitingForRobber = true;
            ShowPopup("全員の廃棄が完了しました！盗賊を移動させるタイルをクリックしてください。");
            UpdateUI();
        }
    }

    // 捨てる資源を増やす（ボタンの OnClick から 1～5 を指定して呼ぶ）
    public void AddDiscard(int resType)
    {
        int currentSelectedTotal = 0;
        for (int i = 1; i <= 5; i++) currentSelectedTotal += discardAmounts[i];

        // まだ必要枚数に達しておらず、かつその資源を手札に持っているなら増やせる
        if (currentSelectedTotal < requiredDiscardCount &&
            players[currentBurstPlayer].inventory[(ResourceType)resType] > discardAmounts[resType])
        {
            discardAmounts[resType]++;
            PlaySound(buttonClickSound);
            UpdateDiscardUI();
        }
        else
        {
            PlaySound(errorSound);
        }
    }

    // 捨てる資源を減らす（ボタンの OnClick から 1～5 を指定して呼ぶ）
    public void RemoveDiscard(int resType)
    {
        if (discardAmounts[resType] > 0)
        {
            discardAmounts[resType]--;
            PlaySound(buttonClickSound);
            UpdateDiscardUI();
        }
        else
        {
            PlaySound(errorSound);
        }
    }

    // バースト画面のテキスト更新
    private void UpdateDiscardUI()
    {
        int currentSelectedTotal = 0;
        for (int i = 1; i <= 5; i++) currentSelectedTotal += discardAmounts[i];
        int remaining = requiredDiscardCount - currentSelectedTotal;

        if (discardInstructionText != null)
        {
            discardInstructionText.text = $"【バースト発生！】\n" +
                                          $"プレイヤー {currentBurstPlayer + 1} は手札が8枚以上あるため、\n" +
                                          $"合計 {requiredDiscardCount} 枚の資源を捨てなければなりません。\n" +
                                          $"（あと {remaining} 枚選んでください）";
        }

        if (discardSummaryText != null)
        {
            string statusStr = "【現在の手持ち と 捨てる数】\n";
            for (int i = 1; i <= 5; i++)
            {
                ResourceType r = (ResourceType)i;
                int have = players[currentBurstPlayer].inventory[r];
                int disc = discardAmounts[i];
                statusStr += $"{r} : 所持 {have} 個  ➔  捨てる: {disc} 個\n";
            }
            discardSummaryText.text = statusStr;
        }
    }

    // 「確定」ボタンを押した時の処理
    public void ConfirmDiscard()
    {
        int currentSelectedTotal = 0;
        for (int i = 1; i <= 5; i++) currentSelectedTotal += discardAmounts[i];

        // ぴったりの枚数を選んでいなければエラー
        if (currentSelectedTotal != requiredDiscardCount)
        {
            ShowPopup($"まだ枚数が足りません！ちょうど {requiredDiscardCount} 枚選んでください。");
            PlaySound(errorSound);
            return;
        }

        // 実際に資源を減らす
        for (int i = 1; i <= 5; i++)
        {
            players[currentBurstPlayer].inventory[(ResourceType)i] -= discardAmounts[i];
        }

        PlaySound(tradeSound); // 確定音
        UpdateUI();

        // 次のバースト対象者へ（いなければ盗賊移動へ自動で進む）
        ProcessNextBurstPlayer();
    }
}