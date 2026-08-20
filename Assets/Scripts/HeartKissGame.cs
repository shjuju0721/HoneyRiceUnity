using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 스테이지 3: 입술 오므렸다 풀어서 하트 발사
public class HeartKissGame : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;
    public TMP_Text statusText;
    public GameObject heartPrefab;      // 발사할 하트 프리팹
    public Transform launchPoint;       // 하트가 생겨나는 위치
    public Transform balloon;           // 목표인 풍선
    public GameObject completePanel;   // 완료 시 띄울 패널. Inspector에서 연결

    [Header("판정 기준")]
    public float chargeThreshold = 0.25f;   // 이 값 이상이면 충전 시작
    public float releaseThreshold = 0.10f;  // 이 값 아래면 발사

    [Header("게임 설정")]
    public float maxChargeTime = 2f;    // 최대 충전 시간(초)
    public int targetCount = 5;         // 클리어에 필요한 발사 횟수
    public float minSpeed = 3f;         // 충전 0일 때 속도
    public float maxSpeed = 10f;        // 만충일 때 속도
    public float minScale = 0.3f;       // 충전 0일 때 하트 크기
    public float maxScale = 1f;         // 만충일 때 하트 크기

    [Header("하트 색칠 연출")]
    public SpriteRenderer balloonRenderer;   // 풍선(빈 하트)의 그림을 담당하는 부품
    public Sprite emptyHeartSprite;          // 시작할 때 보여줄 빈 하트
    public Sprite filledHeartSprite;         // 하트가 닿은 뒤 보여줄 색 하트
    public float hitDistance = 0.6f;         // ★이만큼 가까워지면 "닿았다"로 침
    public float fillHoldTime = 0.8f;        // ★색 하트를 몇 초 보여줄지
    public GameObject popHeartPrefab;   // ★채워질 때 튀어나올 하트
    public int popCount = 5;            // ★몇 개 뿌릴지
    public float popSpreadX = 1.5f;   // ★좌우로 흩어지는 폭
    public float popSpreadY = 1.0f;   // ★위아래로 흩어지는 폭
    public Vector3 popOffset = Vector3.zero;   // ★뿌리는 중심을 미세 조정

    // ===== 내부 상태 =====
    private bool isCharging = false;
    private float chargeTime = 0f;
    private int successCount = 0;
    private bool isFinished = false;

    private Transform activeHeart = null;    // ★지금 날아가는 중인 하트를 기억
    private float fillTimer = 0f;            // ★색 하트를 되돌릴 때까지 남은 시간

    // ===== 게임 시작 시 빈 하트로 맞춰두기 =====
    void Start()
    {
        SetBalloonSprite(emptyHeartSprite);   // ★
    }

    void Update()
    {
        if (faceRunner == null || isFinished)
        {
            return;
        }

        CheckHeartArrived();   // ★하트가 풍선에 닿았는지 매 프레임 확인
        UpdateFillTimer();     // ★색 하트를 되돌릴 시간이 됐는지 확인

        float funnelValue = faceRunner.latestMouthFunnel;

        // --- 충전 시작 ---
        if (!isCharging && funnelValue > chargeThreshold)
        {
            isCharging = true;
            chargeTime = 0f;
        }
        // --- 충전 중 ---
        else if (isCharging && funnelValue > releaseThreshold)
        {
            chargeTime += Time.deltaTime;

            // 최대치를 넘지 않도록 제한
            if (chargeTime > maxChargeTime)
            {
                chargeTime = maxChargeTime;
            }
        }
        // --- 입술을 풀었으면 발사 ---
        else if (isCharging && funnelValue <= releaseThreshold)
        {
            FireHeart();
            isCharging = false;
        }

        UpdateStatusText();
    }

    // ★===== 날아가는 하트가 풍선에 닿았는지 확인 =====
    void CheckHeartArrived()
    {
        // 날아가는 하트가 없으면 볼 것도 없음
        // (파괴된 오브젝트도 유니티에서는 null로 취급됨)
        if (activeHeart == null || balloon == null)
        {
            return;
        }

        // 하트와 풍선 사이의 거리를 잼
        float distance = Vector3.Distance(activeHeart.position, balloon.position);

        if (distance < hitDistance)
        {
            SetBalloonSprite(filledHeartSprite);  // 빈 하트 → 색 하트
            SpawnPopHearts();                     // 축하 하트 뿌리기
            fillTimer = fillHoldTime;             // 되돌릴 시간 예약
            activeHeart = null;                   // 이 하트는 처리 끝

            // ★여기서 성공을 셈 (발사가 아니라 도착이 성공)
            successCount = successCount + 1;
            Debug.Log(successCount + "번째 하트 도착");

            if (successCount >= targetCount)
            {
                Invoke("ShowCompletePanel", fillHoldTime);   // ★연출이 끝난 뒤 패널
            }
        }
    }

    // ★===== 색 하트를 다시 빈 하트로 되돌리기 =====
    void UpdateFillTimer()
    {
        // 예약된 시간이 없으면 아무것도 안 함
        if (fillTimer <= 0f)
        {
            return;
        }

        fillTimer -= Time.deltaTime;

        if (fillTimer <= 0f)
        {
            SetBalloonSprite(emptyHeartSprite);   // 다시 빈 하트로
        }
    }

    // ★===== 풍선 그림 바꾸기 (연결 안 됐어도 에러 안 나게) =====
    void SetBalloonSprite(Sprite newSprite)
    {
        if (balloonRenderer == null || newSprite == null)
        {
            return;
        }

        balloonRenderer.sprite = newSprite;
    }

        // ★===== 채워진 순간 하트를 여러 개 뿌리기 =====
    void SpawnPopHearts()
    {
        if (popHeartPrefab == null || balloon == null)
        {
            return;
        }

        for (int i = 0; i < popCount; i++)
        {
            // 풍선 주변 아무 데나 조금씩 어긋나게 배치
            float offsetX = Random.Range(-popSpreadX, popSpreadX);   // ★
            float offsetY = Random.Range(-popSpreadY, popSpreadY);   // ★
            Vector3 spawnPos = balloon.position + popOffset + new Vector3(offsetX, offsetY, -1f);   // ★

            GameObject pop = Instantiate(popHeartPrefab, spawnPos, Quaternion.identity);

            // 크기도 조금씩 다르게 (0.6배~1.0배)
            float s = Random.Range(0.6f, 1.0f);
            pop.transform.localScale = new Vector3(s, s, 1f);
        }
    }

    // ★===== 축하 연출이 끝난 뒤 완료 패널 띄우기 =====
    void ShowCompletePanel()
    {
        isFinished = true;

        if (completePanel != null)
        {
            completePanel.SetActive(true);
        }
    }

    // ===== 하트 발사 =====
    void FireHeart()
    {
        if (heartPrefab == null || launchPoint == null || balloon == null)
        {
            return;
        }

        // 충전 정도를 0~1로 환산
        float chargeRatio = chargeTime / maxChargeTime;

        // 프리팹을 복제해서 발사 지점에 생성
        GameObject heart = Instantiate(heartPrefab, launchPoint.position, Quaternion.identity);

        // 충전량에 따라 크기 조절
        float scale = Mathf.Lerp(minScale, maxScale, chargeRatio);
        heart.transform.localScale = new Vector3(scale, scale, 1f);

        // 충전량에 따라 속도 조절
        float speed = Mathf.Lerp(minSpeed, maxSpeed, chargeRatio);

        // 하트에게 목표와 속도를 알려줌
        HeartProjectile projectile = heart.GetComponent<HeartProjectile>();
        if (projectile != null)
        {
            projectile.Launch(balloon.position, speed);
        }

        activeHeart = heart.transform;   // ★이 하트를 감시 대상으로 기억

        Debug.Log("하트 발사 (도착하면 카운트됨)");
        // ★성공 카운트는 CheckHeartArrived()에서 처리합니다
    }

    void UpdateStatusText()
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

        if (isCharging)
        {
            // 충전 정도를 퍼센트로 표시
            int percent = (int)((chargeTime / maxChargeTime) * 100f);
            statusText.text = successCount + " / " + targetCount + "\n충전 " + percent + "%";
        }
        else
        {
            statusText.text = successCount + " / " + targetCount + "\n입술을 오므리세요";
        }
    }
}