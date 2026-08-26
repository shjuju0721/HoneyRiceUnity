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
    public Transform tongueTip;         // ★혀 끝 표식 (Tongue의 자식)
    public Transform fly;               // 파리
    public TMP_Text statusText;
    public GameObject completePanel;

    [Header("파리 그림")]
    public SpriteRenderer flyRenderer;  // ★파리의 Sprite Renderer
    public Sprite flyAliveSprite;       // ★살아있는 파리
    public Sprite flyDeadSprite;        // ★죽은 파리

    [Header("판정 기준 (웹에서 검증된 값)")]
    public float ratioOn = 0.35f;       // 이만큼 차면 "혀를 내밀었다"
    public float ratioOff = 0.20f;      // 이 아래면 "혀를 넣었다"
    public float jawMin = 0.55f;        // ★입을 이만큼 벌려야 판정
    public float jawOff = 0.40f;        // 입 벌림 해제 기준

    [Header("혀 연출")]
    public float tongueMinScale = 0.1f;
    public float tongueMaxScale = 2.5f;
    public float tongueSpeed = 12f;

    [Header("게임 설정")]
    public float catchDistance = 1.2f;  // 혀끝이 이만큼 가까우면 잡음
    public int targetFlies = 5;
    public float flySpeed = 0.8f;
    public float deadTime = 1.5f;       // ★죽은 모습을 보여주는 시간(초)

    [Header("진단용 (다 되면 끄기)")]
    public bool showDistance = true;    // ★혀끝-파리 거리를 화면에 표시

    // ===== 내부 상태 =====
    private bool isTongueOut = false;
    private int caughtCount = 0;
    private bool isFinished = false;

    private float currentScale;
    private Vector3 flyBase;            // ★파리가 처음 있던 자리 (범위 기준)
    private Vector3 flyHome;            // 지금 떠다니는 중심
    private float flyTimer = 0f;
    private float lastDistance = 0f;    // 진단용
    private bool canCatch = true;       // ★지금 잡을 수 있는 상태인가 (재장전)
    private float deadTimer = 0f;       // ★죽은 연출 남은 시간

    void Start()
    {
        currentScale = tongueMinScale;

        if (fly != null)
        {
            flyBase = fly.position;
            flyHome = flyBase;
        }

        UpdateTongueVisual();
    }

    void Update()
    {
        if (faceRunner == null || tongueScanner == null || isFinished)
        {
            return;
        }

        // ★죽은 연출 중에는 파리가 멈춰 있고 잡을 수도 없음
        if (deadTimer > 0f)
        {
            deadTimer -= Time.deltaTime;

            if (deadTimer <= 0f)
            {
                RespawnFly();   // 시간이 다 되면 새 파리 등장
            }

            UpdateTongueState();
            UpdateTongueVisual();
            UpdateStatusText();
            return;
        }

        UpdateFly();
        UpdateTongueState();
        UpdateTongueVisual();
        TryCatchFly();
        UpdateStatusText();
    }

    // ===== 혀를 내밀었는지 판정 (이중 기준) =====
    void UpdateTongueState()
    {
        float jaw = faceRunner.latestJawOpen;
        float ratio = tongueScanner.ratio;

        if (!isTongueOut)
        {
            if (jaw >= jawMin && ratio > ratioOn)
            {
                isTongueOut = true;
            }
        }
        else
        {
            if (jaw < jawOff || ratio < ratioOff)
            {
                isTongueOut = false;
                canCatch = true;    // ★혀를 넣었으니 다시 잡을 수 있음
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

        currentScale = Mathf.Lerp(currentScale, target, tongueSpeed * Time.deltaTime);

        Vector3 s = frogTongue.localScale;
        frogTongue.localScale = new Vector3(currentScale, s.y, s.z);
    }

        // ===== 혀끝이 파리에 닿았는지 =====
    void TryCatchFly()
    {
        if (fly == null || tongueTip == null)
        {
            return;
        }

        lastDistance = Vector3.Distance(tongueTip.position, fly.position);

        // 혀를 내민 상태가 아니면 못 잡음
        if (!isTongueOut)
        {
            return;
        }

        // ★한 번 잡았으면 혀를 넣을 때까지 잠금
        if (!canCatch)
        {
            return;
        }

        if (lastDistance < catchDistance)
        {
            canCatch = false;   // ★잠그기
            CatchFly();
        }
    }

    // ===== 파리를 잡았다! =====
    void CatchFly()
    {
        caughtCount = caughtCount + 1;
        Debug.Log(caughtCount + "번째 파리 잡음!");

        // ★죽은 모습으로 바꾸기
        if (flyRenderer != null && flyDeadSprite != null)
        {
            flyRenderer.sprite = flyDeadSprite;
        }

        if (caughtCount >= targetFlies)
        {
            Finish();
            return;
        }

        deadTimer = deadTime;   // ★여기서부터 0.5초 세기 시작
    }

    // ★===== 죽은 연출이 끝나면 새 파리 =====
    void RespawnFly()
    {
        // 살아있는 그림으로 되돌리기
        if (flyRenderer != null && flyAliveSprite != null)
        {
            flyRenderer.sprite = flyAliveSprite;
        }

        MoveFlyToNewSpot();
    }

    // ===== 파리를 다른 곳으로 =====
    void MoveFlyToNewSpot()
    {
        if (fly == null)
        {
            return;
        }

        // ★처음 자리를 기준으로 흩뿌린다.
        //   전에는 계속 더해서 파리가 화면 밖으로 도망갔다.
        float offsetX = Random.Range(-1.5f, 1.5f);
        float offsetY = Random.Range(-1f, 1f);

        flyHome = flyBase + new Vector3(offsetX, offsetY, 0f);
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

        // ★진단용 한 줄
        if (showDistance)
        {
            line = line + "\n거리 " + lastDistance.ToString("F2")
                        + " (잡히는 거리 " + catchDistance.ToString("F2") + ")";
        }

        statusText.text = line;
    }
}