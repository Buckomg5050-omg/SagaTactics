using UnityEngine;

public class HoverPulse : MonoBehaviour
{
    public float pulseSpeed = 5f;
    public float pulseAmount = 0.07f;
    private Vector3 baseScale;

    void Start()
    {
        baseScale = transform.localScale;
    }

    void Update()
    {
        float pulse = 1 + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = new Vector3(baseScale.x * pulse, baseScale.y, baseScale.z * pulse);
    }
}
