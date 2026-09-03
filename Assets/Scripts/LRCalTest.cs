using UnityEngine;

public class LRCalTest : MonoBehaviour
{
    public TongueLRCalibration cal;
    public JellySortGame game;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) cal.StartCalibration();
        if (Input.GetKeyDown(KeyCode.G)) game.StartGame();
    }
}