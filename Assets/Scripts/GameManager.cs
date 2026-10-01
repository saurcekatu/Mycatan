using UnityEngine;
using System.Collections.Generic;
using TMPro; // 【追加】UI（文字）を操作するために必要

public enum ResourceType { None, Wood, Brick, Wool, Grain, Ore }

public class PlayerData
{
    public Dictionary<ResourceType, int> inventory = new Dictionary<ResourceType, int>()
    {
        { ResourceType.Wood, 0 },
        { ResourceType.Brick, 0 },
        { ResourceType.Wool, 0 },
        { ResourceType.Grain, 0 },
        { ResourceType.Ore, 0 }
    };
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("プレイヤーの色設定")]
    public Material[] playerMaterials;
    
    [Header("現在の状態")]
    public int currentPlayerIndex = 0;

    public PlayerData[] players = new PlayerData[4];

    // 【追加】画面に表示するUIテキストの枠
    [Header("UI設定")]
    public TextMeshProUGUI resourceText; 

    void Awake()
    {
        if (Instance == null) Instance = this;
        for (int i = 0; i < 4; i++) players[i] = new PlayerData();
    }

    void Start()
    {
        UpdateUI(); // ゲーム開始時に画面の文字を更新する
    }

    public Material GetCurrentPlayerMaterial()
    {
        return playerMaterials[currentPlayerIndex];
    }

    public void EndTurn()
    {
        currentPlayerIndex++;
        if (currentPlayerIndex >= playerMaterials.Length) currentPlayerIndex = 0;
        
        Debug.Log("ターン終了！次は プレイヤー " + (currentPlayerIndex + 1) + " の番です。");
        UpdateUI(); // ターンが変わったので文字を更新
    }

    public void RollDice()
    {
        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        int total = dice1 + dice2;

        if (total == 7)
        {
            Debug.Log("盗賊が出た！誰も資源を獲得できません。");
            return;
        }

        HexTile[] allHexes = FindObjectsOfType<HexTile>();
        foreach (HexTile hex in allHexes)
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

    // 正しい位置に配置されたチートメソッド
    public void CheatGiveResources() 
    {
        int p = currentPlayerIndex;
        players[p].inventory[ResourceType.Wood] += 10;
        players[p].inventory[ResourceType.Brick] += 10;
        players[p].inventory[ResourceType.Grain] += 10;
        players[p].inventory[ResourceType.Wool] += 10;
        
        UpdateUI(); // チートで資源が増えたので文字を更新
        Debug.Log("チートにより資源を獲得しました。");
    }

    // 【新規】画面の文字（UI）を現在の状況に合わせて書き換えるメソッド
    public void UpdateUI()
    {
        // テキスト枠が設定されていなければ無視する
        if (resourceText == null) return;

        Dictionary<ResourceType, int> inv = players[currentPlayerIndex].inventory;
        
        // 画面に表示する文字列を組み立てる
        string uiText = $"【 プレイヤー {currentPlayerIndex + 1} のターン 】\n";
        uiText += $"木: {inv[ResourceType.Wood]}  |  ";
        uiText += $"レンガ: {inv[ResourceType.Brick]}  |  ";
        uiText += $"麦: {inv[ResourceType.Grain]}  |  ";
        uiText += $"羊: {inv[ResourceType.Wool]}  |  ";
        uiText += $"鉄: {inv[ResourceType.Ore]}";

        // 実際の画面の文字を書き換える
        resourceText.text = uiText;
    }
}