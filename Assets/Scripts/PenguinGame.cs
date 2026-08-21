using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 스테이지 4: 볼 부풀려 모았다가 풀면 펭귄이 미끄러짐
public class PenguinGame : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;
    public Transform penguin;              // 펭귄 오브젝트
    public Animator penguinAnimator;       // 펭귄의 Animator
    public Slider chargeGauge;             // 충전 게이지
    public TMP_Text statusText;
    public GameObject completePanel;

    [Header("애니메이션 컨트롤러")]
    public RuntimeAnimatorController idleController;    // 평소
    public RuntimeAnimatorController preslideController;  // ★힘 모으는 중
    public RuntimeAnimatorController slideController;   // 미끄러질 때

    [Header("판정 기준 (실측값)")]
    public float cheekBaseline = 0.2202f;     // 평상시 볼 너비
    public float cheekThreshold = 0.013f;     // 볼이 이만큼 커져야 부풀림으로 인정

    public float puckerBaseline = 0.55f;      // 평상시 입술 오므림
    public float puckerThreshold = 0.27f;     // 입술이 이만큼 올라가야 부풀림으로 인정
    public float releaseThreshold = 0.06f;    // 입술 변화가 이 아래면 "힘을 풀었다"

    [Header("게임 설정")]
    public float maxChargeTime = 3.5f;   // 최대 충전 시간(초)
    public float minDistance = 1.0f;     // 충전 0일 때 미끄러지는 거리
    public float maxDistance = 4.0f;     // 만충일 때 거리
    public float slideDuration = 1.1f;   // 미끄러지는 데 걸리는 시간
    public float goalX = 8f;             // 이 X를 넘으면 완료

    // ===== 내부 상태 =====
    private bool isCharging = false;     // 지금 부풀리는 중인가
    private float chargeTime = 0f;       // 얼마나 오래 부풀렸나
    private bool isSliding = false;      // 지금 미끄러지는 중인가
    private bool isFinished = false;     // 게임이 끝났나

    private Vector3 slideFrom;           // 미끄러지기 시작한 위치
    private Vector3 slideTo;             // 도착할 위치
    private float slideTimer = 0f;       // 미끄러진 지 얼마나 됐나

    private int launchCount = 0;         // 몇 번 발사했나

    void Start()
    {
        SetAnimator(idleController);
        UpdateGauge(0f);
    }

    void Update()
    {
        if (faceRunner == null || isFinished)
        {
            return;
        }

        // 미끄러지는 중에는 얼굴 입력을 안 받음
        if (isSliding)
        {
            UpdateSlide();
            return;
        }

        // 평소보다 얼마나 변했는지 계산
        float cheekDelta = faceRunner.latestCheekWidth - cheekBaseline;
        float puckerDelta = faceRunner.latestMouthPucker - puckerBaseline;

        // --- 부풀리기 시작: 볼과 입술을 둘 다 넘어야 인정 ---
        if (!isCharging && cheekDelta > cheekThreshold && puckerDelta > puckerThreshold)
        {
            isCharging = true;
            chargeTime = 0f;
            FreezeAnimatorAt(preslideController, 0.8f);   // ★웅크린 자세로 고정
        }
        // --- 부풀린 채 유지 중: 입술이 아직 안 풀렸으면 계속 충전 ---
        else if (isCharging && puckerDelta > releaseThreshold)
        {
            chargeTime += Time.deltaTime;

            if (chargeTime > maxChargeTime)
            {
                chargeTime = maxChargeTime;
            }
        }
        // --- 입술을 풀었으면 발사 ---
        else if (isCharging && puckerDelta <= releaseThreshold)
        {
            StartSlide();
            isCharging = false;
        }

        UpdateGauge(chargeTime / maxChargeTime);
        UpdateStatusText();
    }

    // ===== 미끄러지기 시작 =====
    void StartSlide()
    {
        if (penguin == null)
        {
            return;
        }

        // 충전 정도를 0~1로 환산
        float chargeRatio = chargeTime / maxChargeTime;

        // 충전량에 비례한 거리
        float distance = Mathf.Lerp(minDistance, maxDistance, chargeRatio);

        slideFrom = penguin.position;
        slideTo = penguin.position + new Vector3(distance, 0f, 0f);
        slideTimer = 0f;
        isSliding = true;

        SetAnimator(slideController);   // 미끄러지는 그림으로 교체

        launchCount = launchCount + 1;
        Debug.Log(launchCount + "번째 미끄러짐 / 거리 " + distance.ToString("F2"));
    }

    // ===== 미끄러지는 중 매 프레임 =====
    void UpdateSlide()
    {
        slideTimer += Time.deltaTime;

        // 0~1 사이의 진행도
        float t = slideTimer / slideDuration;

        if (t > 1f)
        {
            t = 1f;
        }

        // 처음엔 빠르고 끝에서 느려지게 (미끄러지다 멈추는 느낌)
        // 처음에 확 튀어나가고 끝에서 스르륵 멈춤 (세제곱 감속)
        float inv = 1f - t;
        float eased = 1f - (inv * inv * inv);

        penguin.position = Vector3.Lerp(slideFrom, slideTo, eased);

        // --- 다 미끄러졌으면 ---
        if (t >= 1f)
        {
            isSliding = false;
            chargeTime = 0f;
            UpdateGauge(0f);
            SetAnimator(idleController);   // 다시 평소 모습

            // 목표 지점을 넘었는지 확인
            if (penguin.position.x >= goalX)
            {
                Finish();
            }
        }
    }

    void SetAnimator(RuntimeAnimatorController controller)
    {
        if (penguinAnimator == null || controller == null)
        {
            return;
        }

        penguinAnimator.speed = 1f;   // ★멈춰뒀던 재생 속도 복구
        penguinAnimator.runtimeAnimatorController = controller;
    }

        // ===== 애니메이션을 특정 지점에서 멈춰 세우기 =====
    // normalizedTime: 0 = 클립 처음, 0.5 = 중간, 1 = 끝
    void FreezeAnimatorAt(RuntimeAnimatorController controller, float normalizedTime)
    {
        if (penguinAnimator == null || controller == null)
        {
            return;
        }

        penguinAnimator.runtimeAnimatorController = controller;
        penguinAnimator.speed = 0f;   // 재생 정지

        // 0번 레이어의 현재 상태를 지정한 지점으로 이동시킴
        penguinAnimator.Play(0, 0, normalizedTime);
        penguinAnimator.Update(0f);   // 즉시 반영
    }

    // ===== 게이지 갱신 =====
    void UpdateGauge(float ratio)
    {
        if (chargeGauge == null)
        {
            return;
        }

        chargeGauge.value = ratio;
    }

    // ===== 완료 =====
    void Finish()
    {
        isFinished = true;

        if (statusText != null)
        {
            statusText.text = "도착했어요!";
        }

        if (completePanel != null)
        {
            completePanel.SetActive(true);
        }
    }

    // ===== 안내 문구 =====
    void UpdateStatusText()
    {
        if (statusText == null)
        {
            return;
        }

        if (isCharging)
        {
            int percent = (int)((chargeTime / maxChargeTime) * 100f);
            statusText.text = "부풀리는 중! " + percent + "%";
        }
        else
        {
            statusText.text = "볼을 빵빵하게 부풀려 보세요";
        }
    }
}