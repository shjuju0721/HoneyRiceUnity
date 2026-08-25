using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 블렌드셰이프 값을 화면에 보여주는 디버그 도구
public class JawOpenDisplay : MonoBehaviour
{
    public MoonClimbFaceRunner runner;
    public TMP_Text display;
    public TongueScanner tongueScanner;   // ★혀 인식 결과

    void Update()
    {
        string tongueLine = "tongue: -";

        if (tongueScanner != null)
        {
            tongueLine = "tongue: " + tongueScanner.ratio.ToString("F2") + "\n"
                + "R" + tongueScanner.redCount
                + " W" + tongueScanner.whiteCount
                + " Y" + tongueScanner.yellowCount
                + " /" + tongueScanner.mouthCount + "\n"
                + "avgV: " + tongueScanner.avgV.ToString("F0");
        }

        display.text =
            "jawOpen: " + runner.latestJawOpen.ToString("F2") + "\n" +
            tongueLine;
    }
}