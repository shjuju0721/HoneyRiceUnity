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

    // ===== 내부 상태 =====
    private bool isCharging = false;
    private float chargeTime = 0f;
    private int successCount = 0;
    private bool isFinished = false;

    void Update()
    {
        if (faceRunner == null || isFinished)
        {
            return;
        }

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

        successCount = successCount + 1;
        Debug.Log(successCount + "번째 하트 발사");

        if (successCount >= targetCount)
        {
            isFinished = true;

            // 완료 패널 켜기
            if (completePanel != null)
            {
                completePanel.SetActive(true);
            }
        }
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