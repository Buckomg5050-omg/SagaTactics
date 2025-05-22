using UnityEngine;
using TMPro;

public class TileTooltipUI : MonoBehaviour
{
    public GameObject panel;
    public TextMeshProUGUI tooltipText;

    private void Start()
    {
        Hide();
    }

    public void Show(string content, Vector2 screenPosition)
    {
        tooltipText.text = content;
        panel.SetActive(true);
        panel.transform.position = screenPosition;
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}
