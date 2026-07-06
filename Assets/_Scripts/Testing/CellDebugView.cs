using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class CellDebugView : MonoBehaviour
{
    public enum DisplayMode { Default, Arrows, BestCost }

    [SerializeField] SpriteRenderer circle;
    [SerializeField] Transform arrowPivot;
    [SerializeField] TextMeshProUGUI bestCostText;

    public void Show(Color color, DisplayMode mode, float2 moveVector, int bestCost, bool isTarget = false)
    {
        circle.gameObject.SetActive(mode == DisplayMode.Default || isTarget);
        circle.color = color;

        arrowPivot.gameObject.SetActive(mode == DisplayMode.Arrows);
        bestCostText.gameObject.SetActive(mode == DisplayMode.BestCost);

        if (mode == DisplayMode.Arrows && math.lengthsq(moveVector) > 0f)
        {
            Vector3 dir = new Vector3(moveVector.x, 0f, moveVector.y);
            arrowPivot.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        if (mode == DisplayMode.BestCost)
            bestCostText.text = bestCost == -1 ? "?" : bestCost.ToString();
    }
}
