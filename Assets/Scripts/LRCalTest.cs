using UnityEngine;

public class LRCalTest : MonoBehaviour
{
    public TongueLRCalibration cal;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            cal.StartCalibration();
        }
    }
}