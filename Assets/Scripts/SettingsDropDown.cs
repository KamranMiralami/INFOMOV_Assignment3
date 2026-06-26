using UnityEngine;

public class SettingsDropDown : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            QuitGame();
        }
    }
    public void SpreadAmountOnValueChanged(string val)
    {
        if (int.TryParse(val, out int finalInteger))
        {
            Settings.Instance.spreadAmount = finalInteger;
            Debug.Log("Spread amount set to: " + finalInteger);
        }
    }
    public void FireRateAmountOnValueChanged(string val)
    {
        if (float.TryParse(val, out float finalInteger))
        {
            Settings.Instance.fireRate = finalInteger;
            Debug.Log("Fire rate set to: " + finalInteger);
        }
    }
    public void ConvertESPIValue(float rawValue)
    {
        int finalInteger = Mathf.RoundToInt(rawValue);
        Settings.Instance.enemySpawnsPerInterval = finalInteger;
        Debug.Log("Enemy spawns per interval set to: " + finalInteger);
    }
    public void ConvertESIValue(float rawValue)
    {
        Settings.Instance.enemySpawnInterval = rawValue;
        Debug.Log("Enemy spawn interval set to: " + rawValue);
    }
    public void DropDownOnValueChanged(int value)
    {
        switch(value)
        {
            case 0:
                MakeMonobehaviour();
                break;
            case 1:
                MakeECSOnly();
                break;
            default:
                Debug.LogWarning("Invalid setting value: " + value);
                break;
        }
    }
    void MakeMonobehaviour()
    {
        Settings.Instance.useECSforBullets = false;
        Settings.Instance.useECSforEnemies = false;
    }
    void MakeECSOnly()
    {
        Settings.Instance.useECSforBullets = true;
        Settings.Instance.useECSforEnemies = true;
    }
    private void QuitGame()
    {
        // If running in the Unity Editor, stop playing
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // If running as a built standalone application, close the app
        Application.Quit();
#endif
    }
}
