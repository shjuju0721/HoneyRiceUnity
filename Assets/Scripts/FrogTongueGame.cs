using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 스테이지 6: 혀를 내밀면 개구리 혀가 뻗어 파리를 잡음
public class FrogTongueGame : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;
    public TongueScanner tongueScanner;
    public Transform frogTongue;        // 개구리 혀 (Frog의 자식)
    public Transform fly;               // 파리
    public TMP_Text statusText;
    public GameObject completePanel;

    [Header("판정 기준 (웹에서 검증된 값)")]
    public float ratioOn = 0.35f;       // 이만큼 차면 "혀를 내밀었다"
    public float ratioOff = 0.20f;      // 이 아래면 "혀를 넣었다"
    public float jawMin = 0.55f;        // ★입을 이만큼 벌려야 판정 (치아 오인식 방지)
    public float jawOff = 0.40f;        // 입 벌림 해제 기준

    [Header("혀 연출")]
    public float tongueMinScale = 0.1f;   // 넣었을 때 길이
    public float tongueMaxScale = 2.5f;   // 최대로 뻗었을 때 길이
    public float tongueSpeed = 12f;       // 뻗고 들어가는 속도

    [Header("게임 설정")]
    public float catchDistance = 1.2f;    // 혀끝이 이만큼 가까우면 잡음
    public int targetFlies = 5;           // 몇 마리 잡으면 완료
    public float flySpeed = 0.8f;         // 파리가 떠다니는 속도

    // ===== 내부 상태 =====
    private bool isTongueOut = false;     // 지금 혀를 내민 상태인가
    private int caughtCount = 0;          // 잡은 파리 수
    private bool isFinished = false;

    private float currentScale;           // 지금 혀 길이
    private Vector3 flyHome;              // 파리가 떠다니는 중심
    private float flyTimer = 0f;

    void Start()
    {
        currentScale = tongueMinScale;

        if (fly != null)
        {
            flyHome = fly.position;
        }

        UpdateTongueVisual();
    }

    void Update()
    {
        if (faceRunner == null || tongueScanner == null || isFinished)
        {
            return;
        }

        UpdateFly();          // 파리 떠다니기
        UpdateTongueState();  // 혀 내밀었는지 판정
        UpdateTongueVisual(); // 혀 길이 부드럽게 바꾸기
        UpdateStatusText();
    }

    // ===== 혀를 내밀었는지 판정 (이중 기준) =====
    void UpdateTongueState()
    {
        float jaw = faceRunner.latestJawOpen;
        float ratio = tongueScanner.ratio;

        if (!isTongueOut)
        {
            // ★입을 충분히 벌리고 + 혀 비율이 넘어야 인정
            if (jaw >= jawMin && ratio > ratioOn)
            {
                isTongueOut = true;
                TryCatchFly();   // 내미는 순간 파리 잡기 시도
            }
        }
        else
        {
            // 혀를 넣었거나 입을 정말 다물었으면 해제
            if (jaw < jawOff || ratio < ratioOff)
            {
                isTongueOut = false;
            }
        }
    }

    // ===== 혀 길이를 목표까지 부드럽게 =====
    void UpdateTongueVisual()
    {
        if (frogTongue == null)
        {
            return;
        }

        float target = isTongueOut ? tongueMaxScale : tongueMinScale;

        // Lerp = 지금 값에서 목표까지 조금씩 다가감 (뚝 끊기지 않게)
        currentScale = Mathf.Lerp(currentScale, target, tongueSpeed * Time.deltaTime);

        Vector3 s = frogTongue.localScale;
        frogTongue.localScale = new Vector3(currentScale, s.y, s.z);
    }

    // ===== 혀끝이 파리에 닿았는지 =====
    void TryCatchFly()
    {
        if (fly == null || frogTongue == null)
        {
            return;
        }

        // 혀끝의 세계 좌표 구하기
        // 피벗이 왼쪽이라, 오른쪽으로 (최대길이 × 스프라이트폭)만큼 간 지점이 끝
        Vector3 tipPos = GetTongueTip();

        float distance = Vector3.Distance(tipPos, fly.position);

        if (distance < catchDistance)
        {
            CatchFly();
        }
    }

    // ===== 혀끝 위치 계산 =====
    Vector3 GetTongueTip()
    {
        // 혀가 뻗은 방향(오른쪽) × 최대 길이만큼 이동한 지점
        float tongueWidth = 1f;

        SpriteRenderer sr = frogTongue.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            tongueWidth = sr.sprite.bounds.size.x;
        }

        return frogTongue.position + frogTongue.right * (tongueMaxScale * tongueWidth);
    }

    // ===== 파리를 잡았다! =====
    void CatchFly()
    {
        caughtCount = caughtCount + 1;
        Debug.Log(caughtCount + "번째 파리 잡음!");

        if (caughtCount >= targetFlies)
        {
            Finish();
            return;
        }

        // 다음 파리를 새 위치에 배치
        MoveFlyToNewSpot();
    }

    // ===== 파리를 다른 곳으로 =====
    void MoveFlyToNewSpot()
    {
        if (fly == null)
        {
            return;
        }

        float offsetX = Random.Range(-1.5f, 1.5f);
        float offsetY = Random.Range(-1f, 1f);

        flyHome = flyHome + new Vector3(offsetX, offsetY, 0f);
        flyTimer = 0f;
    }

    // ===== 파리가 둥실둥실 떠다니기 =====
    void UpdateFly()
    {
        if (fly == null)
        {
            return;
        }

        flyTimer += Time.deltaTime * flySpeed;

        // sin/cos로 8자 모양으로 떠다니게
        float x = Mathf.Sin(flyTimer) * 0.5f;
        float y = Mathf.Sin(flyTimer * 1.7f) * 0.3f;

        fly.position = flyHome + new Vector3(x, y, 0f);
    }

    // ===== 완료 =====
    void Finish()
    {
        isFinished = true;
        isTongueOut = false;

        if (fly != null)
        {
            fly.gameObject.SetActive(false);
        }

        if (statusText != null)
        {
            statusText.text = "파리를 모두 잡았어요!";
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

        string line = "파리 " + caughtCount + " / " + targetFlies + "\n";

        float jaw = faceRunner.latestJawOpen;

        if (jaw < jawMin)
        {
            line = line + "입을 아~ 크게 벌려요";
        }
        else if (isTongueOut)
        {
            line = line + "잘하고 있어요!";
        }
        else
        {
            line = line + "혀를 쏙 내밀어 보세요";
        }

        statusText.text = line;
    }
}