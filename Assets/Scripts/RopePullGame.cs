using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 스테이지 2: 입 다물고 버티기 (줄다리기)
// 5초 유지 × 3세트
public class RopePullGame : MonoBehaviour
{
    [Header("연결")]
    public RopeVisual rope;   // 밧줄 시각화. Inspector에서 연결
    public MoonClimbFaceRunner faceRunner;   // 얼굴 인식 러너
    public TMP_Text statusText;              // 상태 표시 텍스트

    [Header("판정 기준")]
    public float holdThreshold = 0.15f;      // 이 값 이상이면 "다물고 있음"
    public float releaseThreshold = 0.08f;   // 이 값 아래면 "풀림"

    [Header("게임 설정")]
    public float holdDuration = 5f;          // 한 세트당 버텨야 하는 시간(초)
    public int totalSets = 3;                // 총 세트 수

    // ===== 내부 상태 =====
    private float currentHoldTime = 0f;      // 이번 세트에서 지금까지 버틴 시간
    private int completedSets = 0;           // 완료한 세트 수
    private bool isHolding = false;          // 지금 다물고 있는 중인지
    private bool isFinished = false;         // 전체 완료했는지

    void Update()
    {
        // 연결 안 됐거나 이미 다 끝났으면 아무것도 안 함
        if (faceRunner == null || isFinished)
        {
            return;
        }

        // --- 현재 mouthPress 평균값 구하기 ---
        float pressValue = FaceBlendshapeReader.GetAverageScore(
            faceRunner.latestResult, "mouthPressLeft", "mouthPressRight");

        // --- 히스테리시스: 다물었는지 판정 ---
        if (!isHolding && pressValue > holdThreshold)
        {
            isHolding = true;    // 다물기 시작
        }
        else if (isHolding && pressValue < releaseThreshold)
        {
            isHolding = false;   // 풀림
            currentHoldTime = 0f;  // 시간 초기화 (처음부터 다시)
        }

        // --- 다물고 있는 동안 시간 누적 ---
        if (isHolding)
        {
            // Time.deltaTime = 지난 프레임부터 지금까지 걸린 시간(초)
            // 매 프레임 더하면 실제 흐른 시간이 됨
            currentHoldTime += Time.deltaTime;

            // 목표 시간을 채웠으면 한 세트 완료
            if (currentHoldTime >= holdDuration)
            {
                CompleteOneSet();
            }
        }
        // --- 밧줄 팽팽함 갱신 ---
        if (rope != null)
        {
            // 현재 입 다문 정도를 0~1로 환산
            // holdThreshold에 도달하면 1(완전 팽팽)이 되도록 나눔
            float tension = pressValue / holdThreshold;
            rope.SetTension(tension);
        }

        UpdateStatusText(pressValue);
    }

    // ===== 한 세트 완료 처리 =====
    void CompleteOneSet()
    {
        completedSets = completedSets + 1;
        currentHoldTime = 0f;    // 다음 세트를 위해 초기화
        isHolding = false;       // 한 번 풀었다 다시 다물어야 다음 세트 시작

        Debug.Log(completedSets + "세트 완료");

        if (completedSets >= totalSets)
        {
            isFinished = true;
        }
    }

    // ===== 화면 표시 갱신 =====
    void UpdateStatusText(float pressValue)
    {
        if (statusText == null)
        {
            return;
        }

        if (isFinished)
        {
            statusText.text = "모두 완료!";
            return;
        }

        if (isHolding)
        {
            // 남은 시간 계산. 소수점 첫째 자리까지
            float remain = holdDuration - currentHoldTime;
            statusText.text = (completedSets + 1) + "세트\n버티는 중 " + remain.ToString("F1");
        }
        else
        {
            statusText.text = (completedSets + 1) + "세트\n입을 다무세요";
        }
    }
}