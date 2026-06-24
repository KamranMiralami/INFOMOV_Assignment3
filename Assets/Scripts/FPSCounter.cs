using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fpsText;
    [SerializeField] private float updateRate = 0.05f;

    private int frameCount;
    private float dt;
    void Update()
    {
        frameCount++;
        dt += Time.unscaledDeltaTime;
        if (dt >= updateRate)
        {
            float fps = frameCount / dt;
            float ms = (dt / frameCount) * 1000f;
            fpsText.text = $"{Mathf.RoundToInt(fps)} FPS ({ms:F1} ms)";
            if (fps >= 60)
                fpsText.color = Color.green;
            else
                fpsText.color = Color.red;
            frameCount = 0;
            dt -= updateRate;
        }
    }
}