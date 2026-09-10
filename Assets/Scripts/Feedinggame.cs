using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// ============================================================
//  스테이지10 「떠먹여주기」 — 다섯 동작 상태 머신 + 판정
//
//  ★★이 스테이지의 정체성
//   입 벌리기 → 입 다물기 → 숨 참기 → 턱 당겨 꿀꺽 → 기침
//   이건 게임을 위해 지어낸 순서가 아니라 실제 연하재활 기법
//   「성문 위 삼킴법(supraglottic swallow)」이다.
//   (입으로 받고 → 머금고 → 숨을 참아 기도를 닫고 →
//    턱을 당긴 채 삼키고 → 기침으로 남은 것을 뱉어냄)
//
//   ⚠순서를 바꾸거나 섞으면 안 된다. 잘못된 삼킴법을 가르치게 된다.
//   ⚠숨 참기를 "2초 세기"로 되돌리지 말 것 — 숨을 참은 그 상태로
//     곧바로 삼켜야 기법이 안 끊긴다(임상적으로도 이쪽이 맞음).
//   ⚠턱 당기기는 "아래로 숙일 때만" 인정. 고개 젖히기는 chin tuck의
//     정반대이고 사레 위험을 높이는 자세다.
// ============================================================
public class FeedingGame : MonoBehaviour
{
    // ===== 다섯 동작 =====
    public enum Step
    {
        Open = 0,      // ① 입 벌리기
        Close = 1,     // ② 입 다물기
        Hold = 2,      // ③ 숨 참기 (판정 없음 — 안내만)
        Swallow = 3,   // ④ 턱 당겨 꿀꺽
        Cough = 4      // ⑤ 기침
    }

    public enum Mode
    {
        Ready,     // 시작 전
        Play,      // 동작 진행 중
        Gap,       // 술 사이 쉬는 중
        Victory    // 다 끝남
    }

    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;
    public MicScanner mic;                    // 스테이지9의 마이크 (기침 판정용)
    public FeedingSpoon spoon;                // 숟가락 연출
    public CoughCalibration coughCalib;

    [Header("화면 표시")]
    public TMP_Text guideText;                // 지금 무엇을 할지 (큰 글자)
    public TMP_Text praiseText;               // 칭찬 문구
    public TMP_Text progressText;             // "2 / 5 술"
    public TMP_Text hintText;                 // 6초 넘게 못 넘길 때 도움말
    public TMP_Text stepBarText;              // 다섯 단계 표시줄 (간단 버전)
    public GameObject completePanel;

    // ============================================================
    //  판정 문턱 — 전부 앞 스테이지에서 검증된 값
    // ============================================================
    [Header("① 입 벌리기 / ② 입 다물기 (스테이지1 검증값)")]
    public float openThr = 0.60f;             // jawOpen 이상이면 벌린 것
    public float closeThr = 0.25f;            // jawOpen 이하면 다문 것

    [Header("③ 숨 참기 — ★판정 없음, 안내만")]
    public float holdShowSec = 1.3f;          // ⚠이 시간 동안 안내만 보여주고 자동으로 넘김
                                              //   ★2초 세기로 되돌리지 말 것

    [Header("④ 턱 당겨 꿀꺽 (★아래로 숙일 때만)")]
    public float chinPitchDeg = 12f;          // 각도가 이만큼 숙여져야
    public float chinNoseDown = 0.010f;       // 그리고 코끝이 이만큼 내려가야 (AND)
    public float chinNoseOnly = 0.025f;       // 각도를 못 받는 기기는 코끝만, 대신 2.5배 엄격
    public bool pitchDownIsPositive = true;   // ★실측 확정: 숙이면 pitchA가 양수

    [Header("전환 완충")]
    public float lockSec = 1.0f;              // 인정 직후 다음 판정을 잠깐 안 받음
    public float lockAfterHoldSec = 0.25f;    // ★숨 참기 다음만 짧게
                                              //   (참았던 숨이 풀리기 전에 삼켜야 해서)

    [Header("게임 규칙")]
    public int totalSpoons = 5;               // 총 몇 술
    public float gapSec = 3.0f;               // 술 사이 쉬는 시간
    public float praiseSec = 1.0f;            // 칭찬 문구 보여주는 시간
    public float hintSec = 6.0f;              // 이만큼 못 넘기면 도움말
    public float clearWaitSec = 3.0f;         // 축하 뒤 완료 패널까지

    // ===== 결과 =====
    [Header("★결과")]
    public Mode mode = Mode.Ready;
    public Step step = Step.Open;
    public int spoonNo = 1;                   // 지금 몇 술째
    public int doneSteps = 0;                 // 해낸 동작 수 (총 25)

    public System.Action onCleared;

    // ===== 내부 =====
    private float lockT = 0f;                 // 완충 남은 시간
    private float praiseT = 0f;               // 칭찬 남은 시간
    private float stepT = 0f;                 // 지금 동작에 머문 시간 (도움말용)
    private float holdT = 0f;                 // 숨 참기 안내 시간
    private float gapT = 0f;
    private float clearT = 0f;
    private bool panelShown = false;

    // ★턱 당기기 기준 — "숨 참기" 동작 동안에만 잰다
    //   ⚠"턱을 당기라고 한 순간"에 잡으면 이미 숙인 분은 영영 인정 안 됨(옛 3D판 실제 버그)
    private double baseSumPitch = 0.0;
    private double baseSumNose = 0.0;
    private int baseN = 0;
    private float basePitch = 0f;
    private float baseNose = 0f;
    private bool hasBase = false;

    // ★판정 방식은 술마다 한 번만 결정
    //   매 프레임 정하면 각도가 간헐적으로 안 오는 프레임마다
    //   "코끝만 보는" 느슨한 방식으로 새어 10도 조건이 통째로 무너진다
    private bool useP = true;

    // ★완충 중에 들어온 기침을 맡아 둔다
    //   "꿀꺽" 직후 바로 콜록! 이 가장 자연스러운데 그게 버려지면 안 됨
    private bool pendingCough = false;

    private string praiseMsg = "";

    // ===== 동작별 문구 =====
    string StepLabel(Step s)
    {
        switch (s)
        {
            case Step.Open: return "입 벌리기";
            case Step.Close: return "입 다물기";
            case Step.Hold: return "숨 참기";
            case Step.Swallow: return "턱 당겨 꿀꺽";
            case Step.Cough: return "기침";
        }
        return "";
    }

    string StepGuide(Step s)
    {
        switch (s)
        {
            case Step.Open: return "입을 아~ 크게 벌려요";
            case Step.Close: return "입을 다물고\n죽을 머금어요";
            case Step.Hold: return "숨을 참으세요!";
            case Step.Swallow: return "턱을 당기고 꿀꺽 삼켜요";
            case Step.Cough: return "콜록! 하고 뱉어내요";
        }
        return "";
    }

    string StepPraise(Step s)
    {
        switch (s)
        {
            case Step.Open: return "좋아요!";
            case Step.Close: return "잘 머금었어요!";
            case Step.Hold: return "그대로!";
            case Step.Swallow: return "잘 삼키셨어요!";
            case Step.Cough: return "시원하게 뱉었어요!";
        }
        return "";
    }

    string StepHint(Step s)
    {
        switch (s)
        {
            case Step.Open: return "턱이 아플 만큼은 말고, 편하게 아~";
            case Step.Close: return "입술을 가볍게 붙여 주세요";
            case Step.Hold: return "";
            case Step.Swallow: return "고개를 젖히지 말고, 턱을 가슴 쪽으로 당겨요";
            case Step.Cough: return "가볍게 콜록! 소리가 나게 해보세요";
        }
        return "";
    }

    // ============================================================
    //  바깥에서 부르는 것들
    // ============================================================
    void Start()
    {
        if (completePanel != null) completePanel.SetActive(false);
    }

    public void StartGame()
    {
        mode = Mode.Play;
        step = Step.Open;
        spoonNo = 1;
        doneSteps = 0;

        lockT = 0f;
        praiseT = 0f;
        stepT = 0f;
        holdT = 0f;
        gapT = 0f;
        clearT = 0f;
        panelShown = false;
        pendingCough = false;

        ResetBase();

        if (completePanel != null) completePanel.SetActive(false);
        if (mic != null) mic.ResetMic();

        BeginSpoon();
    }

    public void StopGame()
    {
        mode = Mode.Ready;
    }

    void ResetBase()
    {
        baseSumPitch = 0.0;
        baseSumNose = 0.0;
        baseN = 0;
        hasBase = false;
        useP = true;
    }

    // 새 술 시작 — 숟가락을 가져온다
    void BeginSpoon()
    {
        step = Step.Open;
        stepT = 0f;
        holdT = 0f;
        ResetBase();

        if (spoon != null) spoon.Come();
    }

    // ============================================================
    //  매 프레임
    // ============================================================
    void Update()
    {
        float dt = Time.deltaTime;

        if (praiseT > 0f) praiseT -= dt;
        if (lockT > 0f) lockT -= dt;

        switch (mode)
        {
            case Mode.Play:
                UpdatePlay(dt);
                break;

            case Mode.Gap:
                gapT -= dt;

                if (gapT <= 0f)
                {
                    mode = Mode.Play;
                    BeginSpoon();
                }
                break;

            case Mode.Victory:
                if (!panelShown)
                {
                    clearT -= dt;

                    if (clearT <= 0f) ShowComplete();
                }
                break;
        }

        UpdateTexts();
    }

    void UpdatePlay(float dt)
    {
        stepT += dt;

        // ★기침은 완충 중에도 일단 받아 둔다 (버리지 않기)
        if (mic != null)
        {
            var c = mic.TakeCough();

            if (c != null) pendingCough = true;
        }

        // 완충 중에는 판정을 안 받는다 (칭찬을 읽을 시간이기도 하다)
        if (lockT > 0f) return;

        // ③ 숨 참기 — 판정 없이 안내만 보여주고 자동으로 넘어간다
        if (step == Step.Hold)
        {
            holdT += dt;

            // ★이 동안에만 턱 당기기 기준을 잰다
            //   턱 당기기 바로 앞 동작이라 가장 가깝고, 이때 고개는 반드시 편한 자세다
            CollectBase();

            if (holdT >= holdShowSec)
            {
                FinishBase();
                StepDone();
            }

            return;
        }

        // ⑤ 기침 — ★얼굴 검사에서 제외
        //   입을 가리고 기침하는 건 임상에서 실제로 가르치는 자세인데
        //   얼굴이 사라졌다고 버리면 그 자세가 막힌다
        if (step == Step.Cough)
        {
            if (pendingCough)
            {
                pendingCough = false;
                StepDone();
            }

            return;
        }

        // 나머지는 얼굴이 보여야 한다
        if (faceRunner == null) return;

        switch (step)
        {
            case Step.Open:
                if (faceRunner.latestJawOpen >= openThr) StepDone();
                break;

            case Step.Close:
                if (faceRunner.latestJawOpen <= closeThr) StepDone();
                break;

            case Step.Swallow:
                if (JudgeChinTuck()) StepDone();
                break;
        }
    }

    // ============================================================
    //  ★턱 당기기 기준 재기 — "숨 참기" 동안에만
    // ============================================================
    void CollectBase()
    {
        if (faceRunner == null) return;
        if (!faceRunner.latestHasNose) return;

        baseSumNose += faceRunner.latestNose.y;

        if (faceRunner.latestHasPitch)
        {
            baseSumPitch += faceRunner.latestPitchA;
        }

        baseN++;
    }

    void FinishBase()
    {
        if (baseN <= 0)
        {
            hasBase = false;
            useP = false;      // 기준을 못 쟀으면 코끝만, 엄격하게
            return;
        }

        basePitch = (float)(baseSumPitch / baseN);
        baseNose = (float)(baseSumNose / baseN);
        hasBase = true;

        // ★판정 방식을 여기서 한 번만 정한다 (술마다 한 번)
        useP = faceRunner != null && faceRunner.latestHasPitch;
    }

    // ============================================================
    //  ★턱 당기기 판정 — 아래로 숙일 때만
    //
    //  각도만 보면 부호가 기기마다 달라 고개를 젖혀도 통과하고,
    //  코끝만 보면 몸을 앞으로 기울여도 통과한다.
    //  둘을 함께 봐야 "정말 고개를 숙였을 때"만 걸린다.
    // ============================================================
    bool JudgeChinTuck()
    {
        if (!hasBase) return false;
        if (faceRunner == null || !faceRunner.latestHasNose) return false;

        // 코끝이 아래로 내려갔는가 (y는 위가 0이라 커지면 아래)
        float dNose = faceRunner.latestNose.y - baseNose;

        if (!useP)
        {
            // 각도를 못 쓰면 코끝만, 대신 2.5배 엄격하게
            return dNose >= chinNoseOnly;
        }

        if (!faceRunner.latestHasPitch) return false;

        float dPitch = faceRunner.latestPitchA - basePitch;

        // ★실측 확정: 숙이면 양수. 젖히면 음수라 여기서 걸린다.
        if (!pitchDownIsPositive) dPitch = -dPitch;

        return dPitch >= chinPitchDeg && dNose >= chinNoseDown;
    }

    // ============================================================
    //  한 동작을 해냈을 때
    // ============================================================
    void StepDone()
    {
        doneSteps++;

        praiseMsg = StepPraise(step);
        praiseT = praiseSec;

        // ★숨 참기 다음만 완충을 짧게
        lockT = (step == Step.Hold) ? lockAfterHoldSec : lockSec;

        // ① 입 벌리기를 해내면 숟가락이 입에 들어간다
        if (step == Step.Open && spoon != null) spoon.Feed();

        if (step == Step.Cough)
        {
            // 한 술 끝
            if (spoonNo >= totalSpoons)
            {
                mode = Mode.Victory;
                clearT = clearWaitSec;
                praiseMsg = "다 드셨어요! 참 잘하셨어요";
                praiseT = clearWaitSec;
                return;
            }

            spoonNo++;
            mode = Mode.Gap;
            gapT = gapSec;
            return;
        }

        step = (Step)((int)step + 1);
        stepT = 0f;
        holdT = 0f;
    }

    void ShowComplete()
    {
        panelShown = true;

        // ★판 기록 저장 (완료한 술 수)
        RecordStore.SaveCountOnly(10, totalSpoons);

        if (completePanel != null) completePanel.SetActive(true);
        if (onCleared != null) onCleared();
    }

    // ============================================================
    //  화면 문구
    //  ★그리는 순서 = 축하 → 칭찬 → 쉬기
    //    (기침 칭찬이 곧바로 "쉬는 중"에 덮여 한 번도 안 보이던 결함)
    // ============================================================
    void UpdateTexts()
    {
        if (progressText != null)
        {
            progressText.text = mode == Mode.Victory
                ? totalSpoons + " / " + totalSpoons + ""
                : spoonNo + " / " + totalSpoons + "";
        }

        if (praiseText != null)
        {
            praiseText.text = (praiseT > 0f) ? praiseMsg : "";
        }

        if (guideText != null)
        {
            guideText.text = GuideMessage();
        }

        if (hintText != null)
        {
            hintText.text = HintMessage();
        }

        if (stepBarText != null)
        {
            stepBarText.text = StepBar();
        }
    }

    string GuideMessage()
    {
        if (mode == Mode.Victory) return "";
        if (mode == Mode.Ready) return "";

        if (mode == Mode.Gap) return "잘 했어요!\n잠깐 쉬어요";

        // ★마이크가 막혀 있으면 기침 차례에서 영영 멈춘다 — 반드시 알려야 한다
        if (step == Step.Cough && mic != null && !mic.micReady)
        {
            return "마이크를 쓸 수 없어요\n소리 설정을 확인해 주세요";
        }

        // ★연습을 안 했으면 기본 문턱(65)으로 판정해 작은 기침이 안 세진다
        if (step == Step.Cough && coughCalib != null && !coughCalib.HasBaseline())
        {
            return "먼저 9번 기침 게임에서\n연습을 한 번 해주세요";
        }

        if (praiseT > 0f) return "";

        return StepGuide(step);
    }

    string HintMessage()
    {
        if (mode != Mode.Play) return "";
        if (stepT < hintSec) return "";
        if (praiseT > 0f) return "";

        return StepHint(step);
    }

    // 다섯 단계 표시줄 (간단 버전 — 나중에 그림으로 바꿔도 됨)
    string StepBar()
    {
        if (mode == Mode.Ready) return "";

        string s = "";

        for (int i = 0; i < 5; i++)
        {
            string label = StepLabel((Step)i);

            if (mode == Mode.Victory || i < (int)step) s += "● " + label;
            else if (i == (int)step) s += "▶ " + label;
            else s += "○ " + label;

            if (i < 4) s += "   ";
        }

        return s;
    }

    // ============================================================
    //  ★진단
    // ============================================================
    [Header("★진단 표시")]
    public bool showDebug = true;

    void OnGUI()
    {
        if (!showDebug) return;

        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontSize = 16;
        st.normal.textColor = Color.green;

        string chin = "-";

        if (hasBase && faceRunner != null && faceRunner.latestHasNose)
        {
            float dn = faceRunner.latestNose.y - baseNose;
            float dp = faceRunner.latestHasPitch ? faceRunner.latestPitchA - basePitch : 0f;

            chin = "Δpitch " + dp.ToString("+0.0;-0.0") + " (필요 " + chinPitchDeg + ")"
                 + "   Δnose " + dn.ToString("+0.000;-0.000") + " (필요 " + chinNoseDown + ")";
        }

        string info =
            "[떠먹여주기] " + mode + "   " + spoonNo + "/" + totalSpoons + "술   " + StepLabel(step) + "\n"
            + "완충 " + Mathf.Max(0f, lockT).ToString("F2") + "초   머문 시간 " + stepT.ToString("F1") + "초\n"
            + "기준 " + (hasBase ? "잼 (방식 " + (useP ? "각도+코끝" : "코끝만") + ")" : "아직") + "\n"
            + "턱: " + chin + "\n"
            + "맡아둔 기침 " + (pendingCough ? "있음" : "없음")
            + "   해낸 동작 " + doneSteps + " / " + (totalSpoons * 5);

        GUI.Label(new Rect(20f, Screen.height - 150f, 760f, 140f), info, st);
    }
}