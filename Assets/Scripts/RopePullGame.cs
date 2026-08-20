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
    public GameObject completePanel;
    

    [Header("씨름 선수들")]
    public GameObject[] wrestlers;        // 선수 3명. Inspector에서 순서대로 연결
    public Vector3[] ropeEndPoints;       // 선수 수에 맞는 밧줄 끝 위치. 3개

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
    // 세트 완료 후, 입을 한 번 풀어야 다음 세트가 시작되도록 하는 잠금장치
    private bool waitingForRelease = false;

    void Update()
    {
        // 연결 안 됐거나 이미 다 끝났으면 아무것도 안 함
        if (faceRunner == null || isFinished)
        {
            return;
        }

        // 러너가 미리 뽑아둔 값을 그냥 평균냄 (리스트를 안 건드리므로 안전)
        float pressValue = (faceRunner.latestMouthPressLeft + faceRunner.latestMouthPressRight) / 2f;

        // --- 세트 완료 후 잠금 상태: 입을 풀어야 해제 ---
        if (waitingForRelease)
        {
            if (pressValue < releaseThreshold)
            {
                waitingForRelease = false;   // 풀었으니 다음 세트 준비 완료
            }
            // 잠금 중에는 시간 누적을 하지 않으므로 아래로 안 내려감
            UpdateStatusText(pressValue);
            return;
        }

        // --- 히스테리시스: 다물었는지 판정 ---
        if (!isHolding && pressValue > holdThreshold)
        {
            isHolding = true;
        }
        else if (isHolding && pressValue < releaseThreshold)
        {
            isHolding = false;
            currentHoldTime = 0f;
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
        currentHoldTime = 0f;
        isHolding = false;
        waitingForRelease = true;   // 추가: 풀 때까지 잠금
        UpdateWrestlers();   // 선수 추가 + 밧줄 길이 갱신

        Debug.Log(completedSets + "세트 완료");

        if (completedSets >= totalSets)
        {
            isFinished = true;

            if (completePanel != null)
            {
                completePanel.SetActive(true);
                completePanel.SetActive(true);
                rope.gameObject.SetActive(false);   // ★.gameObject를 거쳐야 함
            }
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

        if (waitingForRelease)
        {
            statusText.text = completedSets + "세트 완료!\n힘을 빼세요";
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

    // Play 시작할 때 1명만 보이도록 초기화
    void Start()
    {
        UpdateWrestlers();
    }

    // ===== 완료한 세트 수에 맞춰 선수를 보이게 하고 밧줄을 늘림 =====
    void UpdateWrestlers()
    {
        // 지금 보여야 할 선수 수 = 완료 세트 + 1 (0세트면 1명)
        int visibleCount = completedSets + 1;

        // 전체 세트를 다 끝냈으면 마지막 상태 유지
        if (visibleCount > totalSets)
        {
            visibleCount = totalSets;
        }

        // --- 선수 표시/숨김 ---
        if (wrestlers != null)
        {
            for (int i = 0; i < wrestlers.Length; i++)
            {
                if (wrestlers[i] == null)
                {
                    continue;   // 연결 안 된 칸은 건너뜀
                }

                // SetActive(true) = 오브젝트 켜기, false = 끄기
                // i가 visibleCount보다 작으면 보이기
                wrestlers[i].SetActive(i < visibleCount);
            }
        }

        // --- 밧줄 오른쪽 끝 갱신 ---
        // 선수 수에 맞는 끝점이 지정돼 있으면 사용
        if (rope != null && ropeEndPoints != null && visibleCount <= ropeEndPoints.Length)
        {
            rope.SetRightPoint(ropeEndPoints[visibleCount - 1]);
        }
    }
}