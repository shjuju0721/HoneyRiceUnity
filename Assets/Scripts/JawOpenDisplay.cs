using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;  // MoonClimbFaceRunner가 이 네임스페이스에 있음

// jawOpen 값을 화면에 실시간으로 보여주는 스크립트 (테스트용)
public class JawOpenDisplay : MonoBehaviour
{
    public MoonClimbFaceRunner runner;   // 값을 읽어올 러너. Inspector에서 연결
    public TMP_Text display;             // 값을 표시할 텍스트. Inspector에서 연결

    // 매 프레임 실행. 화면 갱신은 메인 스레드에서 해야 안전함
    void Update()
    {
        if (runner == null || display == null)
        {
            return;
        }

        // F2 = 소수점 둘째 자리까지 표시 (예: 0.42)
        display.text = "jawOpen: " + runner.latestJawOpen.ToString("F2");
    }
}