using UnityEngine;


public class Road : MonoBehaviour
{
    [Header("建設後のマテリアル（色）")]
    

    private Renderer myRenderer;
    private bool isBuilt = false;

    void Start()
    {
        myRenderer = GetComponent<Renderer>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.transform == this.transform)
                {
                    if (!isBuilt)
                    {
                        BuildRoad();
                    }
                    else
                    {
                        Debug.Log("ここはすでに道が引かれています！");
                    }
                }
            }
        }
    }

    void BuildRoad()
    {
        isBuilt = true;
        myRenderer.material = GameManager.Instance.GetCurrentPlayerMaterial();
        // 道は形を変えずに色だけ変える
        Debug.Log(gameObject.name + " に道を建設しました！");
    }
}