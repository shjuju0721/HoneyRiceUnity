using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 스테이지 5: 미소 사진관 (A단계 - 판정 + 게이지, 사진 캡처는 아직 없음)
public class SmileStudioGame : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;
    public TMP_Text statusText;
    public Slider smileGauge;           // 미소 유지 게이지
    public GameObject completePanel;

    [Header("판정 기준 (실측값)")]
    public float detectThreshold = 0.46f;   // 이 값을 넘으면 미소로 인정
    public float releaseThreshold = 0.35f;  // 이 아래로 내려오면 표정을 푼 것

    [Header("게임 설정")]
    public float holdTime = 3.5f;       // 이만큼 유지하면 찰칵
    public float waitTime = 2.5f;       // 찰칵 후 다음 촬영까지 대기
    public int targetPhotos = 5;        // 몇 장 찍으면 완료

    // ===== 내부 상태 =====
    private int photoCount = 0;         // 지금까지 찍은 장수
    private float holdTimer = 0f;       // 미소를 유지한 시간
    private float waitTimer = 0f;       // 촬영 후 대기 남은 시간
    private bool isArmed = true;        // 다음 촬영 준비가 됐는가
    private bool isFinished = false;

    void Update()
    {
        if (faceRunner == null || isFinished)
        {
            return;
        }

        float smile = faceRunner.latestSmile;

        // --- 촬영 직후 대기 중 ---
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;

            // 대기 중에 표정을 풀었으면 재장전
            if (smile < releaseThreshold)
            {
                isArmed = true;
            }

            UpdateGauge(0f);
            UpdateStatusText();
            return;
        }

        // --- 재장전이 안 됐으면: 표정을 풀 때까지 기다림 ---
        if (!isArmed)
        {
            if (smile < releaseThreshold)
            {
                isArmed = true;
            }

            UpdateGauge(0f);
            UpdateStatusText();
            return;
        }

        // --- 미소 유지 시간 재기 ---
        if (smile >= detectThreshold)
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdTime)
            {
                TakePhoto();
            }
        }
        else
        {
            holdTimer = 0f;   // 중간에 풀리면 처음부터
        }

        UpdateGauge(holdTimer / holdTime);
        UpdateStatusText();
    }

    // ===== 찰칵 =====
    void TakePhoto()
    {
        photoCount = photoCount + 1;
        holdTimer = 0f;
        waitTimer = waitTime;
        isArmed = false;      // 표정을 한 번 풀어야 다음 촬영 가능

        Debug.Log("찰칵! " + photoCount + " / " + targetPhotos);

        if (photoCount >= targetPhotos)
        {
            Finish();
        }
    }

    // ===== 완료 =====
    void Finish()
    {
        isFinished = true;

        UpdateGauge(0f);

        if (statusText != null)
        {
            statusText.text = "다섯 장 모두 찍었어요!";
        }

        if (completePanel != null)
        {
            completePanel.SetActive(true);
        }
    }

    // ===== 게이지 갱신 =====
    void UpdateGauge(float ratio)
    {
        if (smileGauge == null)
        {
            return;
        }

        smileGauge.value = ratio;
    }

    // ===== 안내 문구 =====
    void UpdateStatusText()
    {
        if (statusText == null)
        {
            return;
        }

        string line = "사진 " + photoCount + " / " + targetPhotos + "\n";

        if (waitTimer > 0f)
        {
            line = line + "참 멋진 미소예요!";
        }
        else if (!isArmed)
        {
            line = line + "표정을 풀면 다음 사진을 준비할게요";
        }
        else if (holdTimer > 0f)
        {
            line = line + "좋아요~ 그대로요!";
        }
        else
        {
            line = line + "활짝 웃어 보세요";
        }

        statusText.text = line;
    }
}