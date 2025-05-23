using UnityEngine;

public class UnitAP : MonoBehaviour
{
    [Header("Action Points")]
    [SerializeField] private int maxActionPoints = 6;
    [SerializeField] private int startingActionPoints = -1; // Use -1 to default to max

    private int currentActionPoints;

    public int CurrentAP => currentActionPoints;
    public int MaxAP => maxActionPoints;

    void Awake()
    {
        currentActionPoints = (startingActionPoints >= 0) ? startingActionPoints : maxActionPoints;
    }

    public bool CanSpend(int amount)
    {
        return currentActionPoints >= amount;
    }

    public void Spend(int amount)
    {
        currentActionPoints = Mathf.Max(currentActionPoints - amount, 0);
    }

    public void RestoreFull()
    {
        currentActionPoints = maxActionPoints;
    }

    public void SetAP(int value)
    {
        currentActionPoints = Mathf.Clamp(value, 0, maxActionPoints);
    }
}
