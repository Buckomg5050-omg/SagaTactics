using TMPro;
using UnityEngine;

public class TurnIndicatorUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI turnText;

    public void ShowTurn(string unitName, bool isPlayer)
    {
        turnText.text = isPlayer ? $"Your Turn: {unitName}" : $"Enemy Turn: {unitName}";
        turnText.color = isPlayer ? Color.cyan : Color.red;
    }
}
