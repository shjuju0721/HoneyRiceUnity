using UnityEngine;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// ============================================================
//  스테이지10 「떠먹여주기」 — 숟가락 + 팔 연출
//
//  1인칭 보호자 시점: 화면 오른쪽 바깥에서 팔이 뻗어 들어와
//  숟가락이 입 앞까지 다가왔다가, 입에 쏙 넣고, 다시 빠져나간다.
//
//  ★핵심은 armT 하나로 전부 표현하는 것 (웹 방식 그대로)
//     0.00 = 화면 밖 오른쪽
//     0.55 = 입 앞에서 대기
//     0.72 = 입 가까이
//     1.00 = 입에 쏙 (입술에 닿음)
//
//  ★숟가락 크기는 "화면에 보이는 얼굴 크기"에 맞춘다.
//    고정 크기로 두면 큰 모니터에서 입의 절반만 해진다(웹에서 겪은 결함).
//
//  ⚠팔은 숟가락을 따라간다. 숟가락이 주인공이고 팔은 뒤를 따르는 구조라
//    입이 화면 어디에 있든 자연스럽게 이어진다.
// ============================================================
public class FeedingSpoon : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;
    public Camera targetCamera;               // 비우면 Camera.main

    [Header("그림")]
    public Transform arm;                     // 팔 (Arm.png)
    public Transform spoon;                   // 숟가락 (SpoonFull / SpoonEmpty 를 바꿔 낌)
    public SpriteRenderer spoonRenderer;      // 숟가락의 SpriteRenderer
    public Sprite spoonFull;                  // 죽 담긴 숟가락
    public Sprite spoonEmpty;                 // 빈 숟가락

    [Header("★좌표 맞추기 (MouthFollowProbe에서 정한 값과 같게)")]
    public bool flipX = false;
    public bool flipY = false;
    public Vector2 viewMin = new Vector2(0f, 0f);
    public Vector2 viewMax = new Vector2(1f, 1f);
    public float zDepth = 0f;

    [Header("입 위치 미세 조정")]
    public Vector2 mouthOffset = new Vector2(0f, -0.3f);   // 20점 평균이 살짝 위라 내려 준다
    public float followTau = 0.06f;           // 입을 따라오는 부드러움

    [Header("★크기 (얼굴 크기에 맞춰 자동)")]
    public bool autoScale = true;             // 끄면 아래 고정값을 쓴다
    public float spoonPerFaceHeight = 0.55f;  // 숟가락 길이 ÷ 화면 속 얼굴 높이
    public float fixedSpoonScale = 1f;        // autoScale이 꺼졌을 때 쓸 값
    public float armScaleRatio = 2.2f;        // 팔은 숟가락의 몇 배로 그릴지
    // ★멀어지는 느낌 — 입에 가까워질수록 작아진다
    public float scaleNear = 1.4f;            // 화면 밖(armT 0)일 때 배율 = 가까워서 큼
    public float scaleFar = 0.85f;            // 입에 닿을 때(armT 1) 배율 = 멀어서 작음

    [Header("다가오는 자리")]
    public float restT = 0f;                  // 쉴 때 (화면 밖)
    public float waitT = 0.55f;               // 대기 자리
    public float nearT = 0.72f;               // 입 앞
    public float touchT = 1f;                 // 입에 쏙

    public float offscreenX = 3.5f;           // 입에서 오른쪽으로 이만큼 밖에서 시작
    public float armGapX = 1.6f;              // 팔 손잡이가 숟가락 뒤로 떨어진 거리
    public float armGapY = 0f;                // ★손과 숟가락의 높이 차이 (양수면 팔이 위로)

    [Header("움직이는 시간")]
    public float comeSec = 1.0f;              // 들어오는 시간
    public float intoMouthSec = 0.45f;        // 입에 쏙 들어가는 시간
    public float stayInSec = 0.35f;           // 입에 머무는 시간
    public float leaveSec = 0.8f;             // 빠져나가는 시간

    [Header("★테스트")]
    public bool testWithSpace = true;         // 스페이스로 한 번 왕복 (판정 붙이면 끌 것)
    public bool showDebug = true;

    // ===== 상태 =====
    public enum Phase
    {
        Away,       // 화면 밖 (쉬는 중)
        Coming,     // 들어오는 중
        Waiting,    // 입 앞에서 대기 (여기서 판정을 기다린다)
        Feeding,    // 입에 쏙 들어가는 중
        InMouth,    // 입에 머무는 중
        Leaving     // 빠져나가는 중
    }

    [Header("상태 (보기용)")]
    public Phase phase = Phase.Away;
    public float armT = 0f;

    // 입에 다 넣었을 때 알려주기 (죽이 줄어드는 그릇 등에 쓸 것)
    public System.Action onFedOnce;

    // ===== 내부 =====
    private float t = 0f;                     // 지금 단계에서 흐른 시간
    private float fromT = 0f;                 // 움직임 시작 armT
    private float toT = 0f;                   // 움직임 목표 armT

    private Vector3 mouthPos = Vector3.zero;  // 부드럽게 따라가는 입 위치
    private bool hasMouth = false;
    private float faceHeightWorld = 0f;       // 화면 속 얼굴 높이(월드 단위)

    private float spoonBaseLen = 1f;          // 숟가락 스프라이트의 원래 가로 길이

    void Start()
    {
        // 숟가락 원본 크기를 기억해 둔다 (배율 계산의 기준)
        if (spoonRenderer != null && spoonRenderer.sprite != null)
        {
            spoonBaseLen = spoonRenderer.sprite.bounds.size.x;

            if (spoonBaseLen < 0.0001f) spoonBaseLen = 1f;
        }

        SetSpoonFull(true);
        HideAway();
    }

    void Update()
    {
        UpdateMouth();

        if (testWithSpace && Input.GetKeyDown(KeyCode.Space))
        {
            if (phase == Phase.Away) Come();
            else if (phase == Phase.Waiting) Feed();
        }

        UpdatePhase();
        Place();
    }

    // ===== 바깥에서 부르는 것들 =====

    // 숟가락을 입 앞까지 가져온다
    public void Come()
    {
        if (phase != Phase.Away) return;

        SetSpoonFull(true);
        Move(Phase.Coming, armT, waitT, comeSec);
    }

    // 입에 쏙 넣는다 (판정이 통과했을 때 부른다)
    public void Feed()
    {
        if (phase != Phase.Waiting) return;

        Move(Phase.Feeding, armT, touchT, intoMouthSec);
    }

    // 지금 입 앞에서 기다리는 중인가
    public bool IsWaiting()
    {
        return phase == Phase.Waiting;
    }

    // 화면 밖으로 치운다
    public void HideAway()
    {
        phase = Phase.Away;
        armT = restT;
        t = 0f;
    }

    void Move(Phase next, float a, float b, float sec)
    {
        phase = next;
        fromT = a;
        toT = b;
        t = 0f;

        if (sec <= 0f) sec = 0.01f;

        moveSec = sec;
    }

    private float moveSec = 1f;

    // ===== 단계 진행 =====
    void UpdatePhase()
    {
        float dt = Time.deltaTime;
        t += dt;

        switch (phase)
        {
            case Phase.Coming:
            {
                float p = Mathf.Clamp01(t / moveSec);
                armT = Mathf.Lerp(fromT, toT, EaseOutCubic(p));

                if (p >= 1f)
                {
                    phase = Phase.Waiting;
                    t = 0f;
                }
                break;
            }

            case Phase.Feeding:
            {
                float p = Mathf.Clamp01(t / moveSec);
                armT = Mathf.Lerp(fromT, toT, EaseInOutQuad(p));

                if (p >= 1f)
                {
                    phase = Phase.InMouth;
                    t = 0f;

                    // 입에 넣는 순간 죽이 사라진다
                    SetSpoonFull(false);

                    if (onFedOnce != null) onFedOnce();
                }
                break;
            }

            case Phase.InMouth:
            {
                if (t >= stayInSec)
                {
                    Move(Phase.Leaving, armT, restT, leaveSec);
                }
                break;
            }

            case Phase.Leaving:
            {
                float p = Mathf.Clamp01(t / moveSec);
                armT = Mathf.Lerp(fromT, toT, EaseInOutQuad(p));

                if (p >= 1f)
                {
                    phase = Phase.Away;
                    armT = restT;
                    t = 0f;
                }
                break;
            }
        }
    }

    // ===== 입 위치 따라가기 =====
    void UpdateMouth()
    {
        if (faceRunner == null || !faceRunner.latestHasLip)
        {
            hasMouth = false;
            return;
        }

        var lip = faceRunner.latestInnerLip;

        float sx = 0f, sy = 0f;

        for (int i = 0; i < 20; i++)
        {
            sx += lip[i].x;
            sy += lip[i].y;
        }

        float nx = sx / 20f;
        float ny = sy / 20f;

        if (flipX) nx = 1f - nx;
        if (flipY) ny = 1f - ny;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        if (cam == null)
        {
            hasMouth = false;
            return;
        }

        float vx = Mathf.Lerp(viewMin.x, viewMax.x, nx);
        float vy = Mathf.Lerp(viewMin.y, viewMax.y, 1f - ny);

        float dist = Mathf.Abs(zDepth - cam.transform.position.z);

        Vector3 world = cam.ViewportToWorldPoint(new Vector3(vx, vy, dist));
        world.z = zDepth;
        world.x += mouthOffset.x;
        world.y += mouthOffset.y;

        if (!hasMouth)
        {
            mouthPos = world;
        }
        else
        {
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, followTau));
            mouthPos = Vector3.Lerp(mouthPos, world, k);
        }

        hasMouth = true;

        // ★얼굴 높이 재기 — 숟가락 크기의 기준
        //   턱끝(152)과 이마(10)를 쓰고 싶지만 여기선 입술 20점만 있으므로
        //   입 너비로 대신한다. 입 너비 × 3 ≒ 얼굴 높이 정도로 잡는다.
        float minX = lip[0].x, maxX = lip[0].x;

        for (int i = 1; i < 20; i++)
        {
            if (lip[i].x < minX) minX = lip[i].x;
            if (lip[i].x > maxX) maxX = lip[i].x;
        }

        float mouthWidthNorm = maxX - minX;

        // 정규화 폭 → 월드 폭
        Vector3 a = cam.ViewportToWorldPoint(new Vector3(viewMin.x, 0.5f, dist));
        Vector3 b = cam.ViewportToWorldPoint(new Vector3(viewMax.x, 0.5f, dist));
        float viewWidthWorld = Mathf.Abs(b.x - a.x);

        faceHeightWorld = mouthWidthNorm * viewWidthWorld * 3f;
    }

    // ===== 실제로 놓기 =====
    void Place()
    {
        if (!hasMouth) return;

        // --- 크기 ---
        float spoonLen = autoScale
            ? faceHeightWorld * spoonPerFaceHeight
            : fixedSpoonScale * spoonBaseLen;

        float scale = spoonLen / spoonBaseLen;

        // ★armT가 커질수록(입에 가까워질수록) 작아진다 = 멀어지는 느낌
        scale *= Mathf.Lerp(scaleNear, scaleFar, armT);

        if (spoon != null)
        {
            spoon.localScale = Vector3.one * scale;
        }

        if (arm != null)
        {
            arm.localScale = Vector3.one * scale * armScaleRatio;
        }

        // --- 자리 ---
        // armT 0 = 입에서 오른쪽으로 offscreenX 만큼 밖 / 1 = 입에 닿음
        float startX = mouthPos.x + offscreenX * Mathf.Max(0.4f, scale);

        Vector3 spoonPos = mouthPos;
        spoonPos.x = Mathf.Lerp(startX, mouthPos.x, armT);

        // 들어올 때 살짝 위에서 내려오는 느낌
        spoonPos.y = mouthPos.y + (1f - armT) * 0.25f * faceHeightWorld;

        if (spoon != null)
        {
            spoon.position = spoonPos;
        }

        // 팔은 숟가락 뒤를 따라간다
        if (arm != null)
        {
            Vector3 armPos = spoonPos;
            armPos.x += armGapX * scale;
            armPos.y += armGapY * scale;
            arm.position = armPos;

            // ★팔이 화면 오른쪽 끝을 넘어가도록 가로만 늘린다
            //   (허공에서 뚝 끊겨 보이는 것 방지)
            float armScale = scale * armScaleRatio;

            Camera c = targetCamera != null ? targetCamera : Camera.main;

            if (c != null)
            {
                float d = Mathf.Abs(zDepth - c.transform.position.z);
                float rightEdge = c.ViewportToWorldPoint(new Vector3(1f, 0.5f, d)).x;

                var sr = arm.GetComponent<SpriteRenderer>();

                if (sr != null && sr.sprite != null)
                {
                    float armLen = sr.sprite.bounds.size.x;
                    float needX = (rightEdge - armPos.x) / armLen * 2.2f;

                    arm.localScale = new Vector3(
                        Mathf.Max(armScale, needX),
                        armScale,
                        1f);
                }
                else
                {
                    arm.localScale = Vector3.one * armScale;
                }
            }
        }
    }

    void SetSpoonFull(bool full)
    {
        if (spoonRenderer == null) return;

        Sprite s = full ? spoonFull : spoonEmpty;

        if (s != null) spoonRenderer.sprite = s;
    }

    // ===== 부드러운 곡선 =====
    float EaseOutCubic(float p)
    {
        float q = 1f - p;
        return 1f - q * q * q;
    }

    float EaseInOutQuad(float p)
    {
        return p < 0.5f ? 2f * p * p : 1f - Mathf.Pow(-2f * p + 2f, 2f) / 2f;
    }

    // ===== ★진단 =====
    void OnGUI()
    {
        if (!showDebug) return;

        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontSize = 18;
        st.normal.textColor = Color.white;

        string info =
            "[숟가락 연출]  " + phase + "   armT " + armT.ToString("F2") + "\n"
            + (hasMouth ? "입 (" + mouthPos.x.ToString("F2") + ", " + mouthPos.y.ToString("F2") + ")"
                        : "입을 못 찾음") + "\n"
            + "얼굴 높이(추정) " + faceHeightWorld.ToString("F2") + "\n\n"
            + (testWithSpace
                ? "★스페이스 = 가져오기 / (대기 중에) 입에 넣기"
                : "");

        GUI.Label(new Rect(20f, 250f, 640f, 160f), info, st);
    }
}