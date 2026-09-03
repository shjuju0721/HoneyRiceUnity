using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// ============================================================
//  스테이지7 「젤리 분류」 — 좌우 판정 기준 재기 (캘리브레이션)
//
//  왜 필요한가:
//   ① 사람마다 "가만히 있을 때"의 값이 0이 아니다 (실측 +0.07)
//   ② 좌우로 가는 정도가 사람마다 다르다 (편측 마비면 한쪽만 잘 감)
//   → 고정 판정선을 쓰면 약한 쪽은 영영 인정이 안 된다.
//
//  순서:
//   ① 가운데 2초   → 기준값(base)
//   ② 왼쪽 3회     → 왼쪽 최대치
//   ③ 오른쪽 3회   → 오른쪽 최대치
//   → 방향별 판정선 = 그 사람 최대치의 1/4 (0.04 ~ 0.10 사이로 가둠)
// ============================================================
public class TongueLRCalibration : MonoBehaviour
{
    // ===== 진행 단계 =====
    public enum Step
    {
        Idle,     // 아직 시작 안 함
        Center,   // ① 가운데 기준 재는 중
        Left,     // ② 왼쪽 3회
        Right,    // ③ 오른쪽 3회
        Done      // 끝
    }

    [Header("연결")]
    public TongueScanner scanner;            // 혀 색 인식 (TongueManager에 붙어 있는 것)
    public MoonClimbFaceRunner faceRunner;   // ★입 벌림(jawOpen)을 읽어오는 곳

    [Header("화면 표시 (없어도 동작함)")]
    public TMP_Text guideText;               // 큰 안내 문구
    public TMP_Text progressText;            // "●●○" 같은 진행 표시
    public TMP_Text valueText;               // 지금 값 (연습 중 참고용, 비워도 됨)

    // ===== 기준 재기 설정 =====
    [Header("① 가운데 기준")]
    public float centerHoldSec = 2.0f;       // 이만큼 "제대로" 유지해야 함

    [Header("② ③ 좌우 반복")]
    public int repsPerSide = 3;              // 방향마다 몇 번
    public float detectThr = 0.04f;          // 이만큼 벗어나면 "그쪽으로 갔다"고 봄
    public float sideHoldSec = 0.3f;         // 그 상태를 이만큼 유지해야 1회 인정
    public float returnThr = 0.03f;          // 이 안으로 돌아와야 다음 회차 인정

    [Header("판정선 계산")]
    public float thrRatio = 0.25f;           // 최대치의 몇 배를 판정선으로 쓸지
    public float thrMin = 0.04f;             // ★너무 낮으면 가만히 있어도 통과함
    public float thrMax = 0.10f;             // ★너무 높으면 끝까지 뻗어도 안 통과함

    [Header("★측정 가능 조건 (안전장치)")]
    public float jawMin = 0.55f;             // ★입을 이만큼 벌려야 함
                                             //   웹 스펙이 "제거 금지"라고 못 박은 안전장치.
                                             //   입 다문 채 나온 값으로 기준이 잡히는 사고를 막는다.
    public float minRatio = 0.30f;           // 혀가 이만큼은 보여야 값을 믿음
    public int minMouthPix = 150;            // 입 안 점 수가 이만큼은 돼야 함

    [Header("값 다듬기")]
    public float smoothTau = 0.12f;          // 값 떨림 줄이기 (클수록 차분·느림)

    // ===== 결과 (읽기만 할 것) =====
    [Header("★결과")]
    public Step step = Step.Idle;
    public float lrBase = 0f;                // 가운데 기준값
    public float leftMax = 0f;               // 왼쪽 최대치 (기준 뺀 값)
    public float rightMax = 0f;              // 오른쪽 최대치 (기준 뺀 값, 양수로 저장)
    public float thrLeft = 0.07f;            // 왼쪽 판정선
    public float thrRight = 0.07f;           // 오른쪽 판정선
    public bool ready = false;               // 기준 재기가 끝났는가

    // 끝났을 때 알려주기 (게임 스크립트가 받아서 다음으로 넘어가면 됨)
    public System.Action onFinished;

    // ===== 저장 이름 (PlayerPrefs) =====
    const string KEY_READY = "s7_lr_ready";
    const string KEY_BASE = "s7_lr_base";
    const string KEY_THR_L = "s7_lr_thr_left";
    const string KEY_THR_R = "s7_lr_thr_right";
    const string KEY_MAX_L = "s7_lr_max_left";
    const string KEY_MAX_R = "s7_lr_max_right";

    // ===== 내부 =====
    private float smoothed = 0f;             // 다듬은 balance
    private bool hasSmoothed = false;

    private float centerTime = 0f;           // 가운데 유지한 시간
    private double centerSum = 0.0;          // 가운데 값 합계
    private int centerN = 0;

    private int repDone = 0;                 // 이번 방향에서 인정된 횟수
    private float[] peaks;                   // 회차별 최대치
    private bool holding = false;            // 지금 그쪽으로 대고 있는 중인가
    private float holdTime = 0f;
    private float holdPeak = 0f;
    private bool armed = true;               // 다음 회차를 받을 준비가 됐는가
    private float returnTime = 0f;

    private string notice = "";              // 상태 안내 ("입을 벌려요" 등)

    void Awake()
    {
        peaks = new float[Mathf.Max(1, repsPerSide)];
        LoadSaved();
    }

    void Update()
    {
        if (scanner == null) return;

        // --- 값 다듬기 (시간 기준이라 프레임 수와 무관) ---
        float raw = scanner.balance;

        if (!hasSmoothed)
        {
            smoothed = raw;
            hasSmoothed = true;
        }
        else
        {
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, smoothTau));
            smoothed = Mathf.Lerp(smoothed, raw, k);
        }

        // --- 지금 값을 믿을 수 있는 상태인가 ---
        bool measurable = IsMeasurable();

        switch (step)
        {
            case Step.Center:
                UpdateCenter(measurable);
                break;

            case Step.Left:
                UpdateSide(measurable, +1);
                break;

            case Step.Right:
                UpdateSide(measurable, -1);
                break;
        }

        UpdateTexts();
    }

    // ===== 지금 재도 되는 상태인가 =====
    // ★셋 다 통과해야 값을 쓴다. 하나라도 어긋나면 진행이 멈춘다(되돌리진 않음).
    bool IsMeasurable()
    {
        // ① 입을 충분히 벌렸는가
        if (faceRunner != null && faceRunner.latestJawOpen < jawMin)
        {
            notice = "입을 아~ 크게 벌려요";
            return false;
        }

        // ② 입 안이 제대로 보이는가
        if (scanner.mouthCount < minMouthPix)
        {
            notice = "입을 아~ 크게 벌려요";
            return false;
        }

        // ③ 혀가 보이는가
        if (scanner.ratio < minRatio)
        {
            notice = "혀가 보이게 두세요";
            return false;
        }

        notice = "";
        return true;
    }

    // ===== 바깥에서 부르는 것들 =====

    // 기준 재기 시작
    public void StartCalibration()
    {
        step = Step.Center;

        centerTime = 0f;
        centerSum = 0.0;
        centerN = 0;

        lrBase = 0f;
        leftMax = 0f;
        rightMax = 0f;
        ready = false;

        hasSmoothed = false;

        ResetSideState();
    }

    // 중간에 그만두기
    public void Cancel()
    {
        step = Step.Idle;
        ResetSideState();
    }

    // 저장된 기준 불러오기
    public void LoadSaved()
    {
        if (PlayerPrefs.GetInt(KEY_READY, 0) == 1)
        {
            lrBase = PlayerPrefs.GetFloat(KEY_BASE, 0f);
            thrLeft = PlayerPrefs.GetFloat(KEY_THR_L, 0.07f);
            thrRight = PlayerPrefs.GetFloat(KEY_THR_R, 0.07f);
            leftMax = PlayerPrefs.GetFloat(KEY_MAX_L, 0f);
            rightMax = PlayerPrefs.GetFloat(KEY_MAX_R, 0f);
            ready = true;
            step = Step.Done;
        }
    }

    // 저장한 기준 지우기 (다시 재고 싶을 때)
    public void ClearSaved()
    {
        PlayerPrefs.DeleteKey(KEY_READY);
        PlayerPrefs.DeleteKey(KEY_BASE);
        PlayerPrefs.DeleteKey(KEY_THR_L);
        PlayerPrefs.DeleteKey(KEY_THR_R);
        PlayerPrefs.DeleteKey(KEY_MAX_L);
        PlayerPrefs.DeleteKey(KEY_MAX_R);
        PlayerPrefs.Save();

        ready = false;
        step = Step.Idle;
    }

    // ★게임에서 쓸 방향 판정: +1 왼쪽 / 0 가운데 / −1 오른쪽
    public int Direction()
    {
        if (!ready) return 0;
        if (!IsMeasurable()) return 0;

        float v = smoothed - lrBase;

        if (v >= thrLeft) return +1;
        if (v <= -thrRight) return -1;

        return 0;
    }

    // 지금 값 (기준 뺀 것) — 화면 표시용
    public float CurrentValue()
    {
        return smoothed - lrBase;
    }

    // ===== ① 가운데 기준 재기 =====
    // ★관대형: 제대로 못 하고 있으면 시간을 되돌리지 않고 "멈춰서 기다린다".
    //   제대로 하는 프레임만 평균에 넣어 기준이 더러워지지 않게 한다.
    void UpdateCenter(bool measurable)
    {
        if (!measurable) return;

        centerTime += Time.deltaTime;
        centerSum += smoothed;
        centerN++;

        if (centerTime >= centerHoldSec)
        {
            lrBase = centerN > 0 ? (float)(centerSum / centerN) : 0f;

            step = Step.Left;
            ResetSideState();
        }
    }

    // ===== ② ③ 좌우 반복 =====
    // sign = +1 이면 왼쪽, −1 이면 오른쪽
    void UpdateSide(bool measurable, int sign)
    {
        if (!measurable)
        {
            // 못 재는 동안은 진행을 멈춘다 (되돌리지는 않음)
            holding = false;
            return;
        }

        float v = (smoothed - lrBase) * sign;   // 그쪽 방향이면 양수

        // --- 다음 회차를 받을 준비: 가운데로 돌아와야 함 ---
        if (!armed)
        {
            if (Mathf.Abs(smoothed - lrBase) < returnThr)
            {
                returnTime += Time.deltaTime;

                if (returnTime >= 0.15f)
                {
                    armed = true;
                    returnTime = 0f;
                }
            }
            else
            {
                returnTime = 0f;
            }

            return;
        }

        // --- 그쪽으로 대고 있는 중인가 ---
        if (!holding)
        {
            if (v >= detectThr)
            {
                holding = true;
                holdTime = 0f;
                holdPeak = v;
            }
        }
        else
        {
            holdTime += Time.deltaTime;

            if (v > holdPeak) holdPeak = v;      // ★유지하는 동안의 최대치를 잡는다

            if (v < detectThr)
            {
                // 가운데로 돌아왔다 → 충분히 오래 댔으면 1회 인정
                holding = false;

                if (holdTime >= sideHoldSec)
                {
                    if (repDone < peaks.Length)
                    {
                        peaks[repDone] = holdPeak;
                    }

                    repDone++;
                    armed = false;
                    returnTime = 0f;

                    if (repDone >= repsPerSide)
                    {
                        FinishSide(sign);
                    }
                }
            }
        }
    }

    // 한 방향이 끝났을 때
    void FinishSide(int sign)
    {
        float med = Median(peaks, repsPerSide);

        if (sign > 0)
        {
            leftMax = med;
            thrLeft = Mathf.Clamp(med * thrRatio, thrMin, thrMax);

            step = Step.Right;
            ResetSideState();
        }
        else
        {
            rightMax = med;
            thrRight = Mathf.Clamp(med * thrRatio, thrMin, thrMax);

            Finish();
        }
    }

    // 전부 끝
    void Finish()
    {
        ready = true;
        step = Step.Done;

        PlayerPrefs.SetInt(KEY_READY, 1);
        PlayerPrefs.SetFloat(KEY_BASE, lrBase);
        PlayerPrefs.SetFloat(KEY_THR_L, thrLeft);
        PlayerPrefs.SetFloat(KEY_THR_R, thrRight);
        PlayerPrefs.SetFloat(KEY_MAX_L, leftMax);
        PlayerPrefs.SetFloat(KEY_MAX_R, rightMax);
        PlayerPrefs.Save();

        if (onFinished != null) onFinished();
    }

    void ResetSideState()
    {
        repDone = 0;
        holding = false;
        holdTime = 0f;
        holdPeak = 0f;
        armed = true;
        returnTime = 0f;

        for (int i = 0; i < peaks.Length; i++) peaks[i] = 0f;
    }

    // ===== 3개 중 가운데 값 =====
    // ★평균이 아니라 중앙값을 쓴다 — 한 번 잘못 나온 값에 안 휘둘리게
    float Median(float[] arr, int n)
    {
        if (n <= 0) return 0f;

        n = Mathf.Min(n, arr.Length);

        float[] copy = new float[n];
        for (int i = 0; i < n; i++) copy[i] = arr[i];

        for (int a = 1; a < n; a++)
        {
            float key = copy[a];
            int b = a - 1;

            while (b >= 0 && copy[b] > key)
            {
                copy[b + 1] = copy[b];
                b--;
            }

            copy[b + 1] = key;
        }

        if (n % 2 == 1) return copy[n / 2];

        return (copy[n / 2 - 1] + copy[n / 2]) * 0.5f;
    }

    // ===== 화면 문구 =====
    void UpdateTexts()
    {
        if (guideText != null)
        {
            guideText.text = GuideMessage();
        }

        if (progressText != null)
        {
            progressText.text = ProgressMessage();
        }

        if (valueText != null)
        {
            if (step == Step.Idle)
            {
                valueText.text = "";
            }
            else
            {
                valueText.text = "지금 " + CurrentValue().ToString("+0.00;-0.00");
            }
        }
    }

    string GuideMessage()
    {
        // 상태 안내가 있으면 그게 먼저 (입을 벌려요 등)
        if (step != Step.Idle && step != Step.Done && notice != "")
        {
            return notice;
        }

        switch (step)
        {
            case Step.Idle:
                return "";

            case Step.Center:
                // ★§17.3 교훈: 안내 문구가 곧 인식률.
                //   "혀를 내밀어요"라고 하면 앞으로 빼서 값이 안 나온다.
                return "입을 아~ 벌리고\n혀끝을 입 한가운데에 두세요";

            case Step.Left:
                return "혀를 앞으로 빼지 말고\n혀끝을 왼쪽 입꼬리에 콕! 대었다가\n가운데로 돌아오세요";

            case Step.Right:
                return "혀를 앞으로 빼지 말고\n혀끝을 오른쪽 입꼬리에 콕! 대었다가\n가운데로 돌아오세요";

            case Step.Done:
                return "다 됐어요! 잘하셨어요 👏";
        }

        return "";
    }

    string ProgressMessage()
    {
        if (step == Step.Center)
        {
            return "그대로 " + Mathf.CeilToInt(centerHoldSec - centerTime) + "초";
        }

        if (step == Step.Left || step == Step.Right)
        {
            string s = "";

            for (int i = 0; i < repsPerSide; i++)
            {
                s += (i < repDone) ? "●" : "○";
            }

            return s;
        }

        return "";
    }

    // ===== ★진단 (화면 아래쪽에 표시 — 진단 그림과 안 겹치게) =====
    void OnGUI()
    {
        if (scanner == null || !scanner.showDebug) return;

        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontSize = 16;
        st.normal.textColor = Color.yellow;

        string dirText = "가운데";
        int d = Direction();
        if (d == 1) dirText = "◀ 왼쪽";
        else if (d == -1) dirText = "오른쪽 ▶";

        string info =
            "[기준재기] " + step + "   " + ProgressMessage() + "\n" +
            "jawOpen " + (faceRunner != null ? faceRunner.latestJawOpen.ToString("F2") : "-") +
            "   잴수있음 " + (IsMeasurable() ? "O" : "X  " + notice) + "\n" +
            "base " + lrBase.ToString("F3") +
            "   지금 " + CurrentValue().ToString("+0.000;-0.000") + "\n" +
            "왼쪽 최대 " + leftMax.ToString("F3") + " → 판정선 " + thrLeft.ToString("F3") + "\n" +
            "오른쪽 최대 " + rightMax.ToString("F3") + " → 판정선 " + thrRight.ToString("F3") + "\n" +
            "방향 " + dirText;

        // ★화면 왼쪽 아래에 그린다 (위쪽 진단 그림과 겹치지 않게)
        float boxH = 150f;
        GUI.Label(new Rect(10f, Screen.height - boxH - 10f, 480f, boxH), info, st);
    }
}