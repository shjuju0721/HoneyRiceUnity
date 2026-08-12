using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 블렌드셰이프 값을 화면에 보여주는 디버그 도구
public class JawOpenDisplay : MonoBehaviour
{
    public MoonClimbFaceRunner runner;
    public TMP_Text display;

    void Update()
    {
        if (runner == null || display == null)
        {
            return;
        }

        // 러너가 미리 뽑아둔 값들을 읽기만 함
        display.text =
            "jawOpen: " + runner.latestJawOpen.ToString("F2") + "\n" +
            "mouthFunnel: " + runner.latestMouthFunnel.ToString("F2") + "\n" +
            "mouthPucker: " + runner.latestMouthPucker.ToString("F2");
    }
}