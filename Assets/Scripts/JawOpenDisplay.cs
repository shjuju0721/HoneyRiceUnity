using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 여러 블렌드셰이프 값을 한 번에 화면에 보여주는 디버그 도구
// 스테이지마다 보고 싶은 값이 달라서 목록을 Inspector에서 지정할 수 있게 만듦
public class JawOpenDisplay : MonoBehaviour
{
    public MoonClimbFaceRunner runner;   // 값을 읽어올 러너
    public TMP_Text display;             // 표시할 텍스트

    // 화면에 보여줄 블렌드셰이프 이름 목록
    // Inspector에서 Size를 늘리고 이름을 직접 입력할 수 있음
    public string[] watchNames = { "jawOpen", "mouthPressLeft", "mouthPressRight" };

    void Update()
    {
        if (runner == null || display == null)
        {
            return;
        }

        // 러너가 보관 중인 최신 결과를 가져옴
        var result = runner.latestResult;

        // 여러 줄을 이어붙이기 위한 임시 문자열
        string text = "";

        // 목록에 있는 이름들을 하나씩 꺼내서 점수를 조회
        foreach (string name in watchNames)
        {
            float score = FaceBlendshapeReader.GetScore(result, name);
            // \n = 줄바꿈 (파이썬과 동일)
            text += name + ": " + score.ToString("F2") + "\n";
        }

        display.text = text;
    }
}