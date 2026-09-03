using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// ============================================================
//  스테이지7 「젤리 분류」 — 좌우 판정 기준 재기 (캘리브레이션)
//
//  왜 필요한가:
//   ① 사람마다 "가만히 있을 때"의 값이 0이 아니다
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
    public enum Step
    {
        Idle,     // 아직 시작 안 함
        Center,   // ① 가운데 기준 재는 중
        Left,     // ② 왼쪽 3회
        Right,    // ③ 오른쪽 3회
        Done      // 끝
    }

    [Header("연결")]
    public TongueScanner scanner;
    public MoonClimbFaceRunner faceRunner;   // ★입 벌림(jawOpen)을 읽어오는 곳

    [Header("화면 표시 (없어도 동작함)")]
    public TMP_Text guideText;
    public TMP_Text progressText;
    public TMP_Text valueText;

    [Header("① 가운데 기준")]
    public float centerHoldSec = 2.0f;

    [Header("② ③ 좌우 반복")]
    public int repsPerSide = 3;
    public float detectThr = 0.04f;          // 이만큼 벗어나면 "그쪽으로 갔다"고 봄
    public float sideHoldSec = 0.3f;         // 그 상태를 이만큼 유지해야 1회 인정
    public float returnThr = 0.03f;          // 이 안으로 돌아와야 다음 회차 인정

    [Header("판정선 계산")]
    public float thrRatio = 0.25f;
    public float thrMin = 0.04f;
    public float thrMax = 0.10f;

    [Header("★측정 가능 조건 (안전장치)")]
    public float jawMin = 0.45f;             // ★입을 이만큼 벌려야 함
    public float minRatio = 0.30f;           // 혀가 이만큼은 보여야 함
    public int minMouthPix = 150;            // 입 안 점 수

    [Header("값 다듬기")]
    public float smoothTau = 0.12f;

    [Header("★결과")]
    public Step step = Step.Idle;
    public float lrBase = 0f;
    public float leftMax = 0f;
    public float rightMax = 0f;
    public float thrLeft = 0.07f;
    public float thrRight = 0.07f;
    public bool ready = false;

    public System.Action onFinished;

    const string KEY_READY = "s7_lr_ready";
    const string KEY_BASE = "s7_lr_base";
    const string KEY_THR_L = "s7_lr_thr_left";
    const string KEY_THR_R = "s7_lr_thr_right";
    const string KEY_MAX_L = "s7_lr_max_left";
    const string KEY_MAX_R = "s7_lr_max_right";

    private float smoothed = 0f;
    private bool hasSmoothed = false;

    private float centerTime = 0f;
    private double centerSum = 0.0;
    private int centerN = 0;

    private int repDone = 0;
    private float[] peaks;
    private bool holding = false;
    private float holdTime = 0f;
    private float holdPeak = 0f;
    private bool armed = true;
    private float returnTime = 0f;

    private string notice = "";

    void Awake()
    {
        peaks = new float[Mathf.Max(1, repsPerSide)];
        LoadSaved();
    }

    void Update()
    {
        if (scanner == null) return;

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

        bool measurable = CanMeasure();

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

    // ===== ★지금 재도 되는 상태인가 (바깥에서도 쓴다) =====
    public bool CanMeasure()
    {
        if (scanner == null)
        {
            notice = "";
            return false;
        }

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

    // 왜 못 재는지 (바깥에서 안내 문구로 쓴다)
    public string NoticeText()
    {
        return notice;
    }

    // ===== 바깥에서 부르는 것들 =====

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

    public void Cancel()
    {
        step = Step.Idle;
        ResetSideState();
    }

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
        if (!CanMeasure()) return 0;

        float v = smoothed - lrBase;

        if (v >= thrLeft) return +1;
        if (v <= -thrRight) return -1;

        return 0;
    }

    // 지금 값 (기준 뺀 것)
    public float CurrentValue()
    {
        return smoothed - lrBase;
    }

    // ===== ① 가운데 기준 재기 =====
    // ★관대형: 제대로 못 하고 있으면 시간을 되돌리지 않고 "멈춰서 기다린다".
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
    void UpdateSide(bool measurable, int sign)
    {
        if (!measurable)
        {
            holding = false;
            return;
        }

        float v = (smoothed - lrBase) * sign;

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

            if (v > holdPeak) holdPeak = v;   // ★유지하는 동안의 최대치

            if (v < detectThr)
            {
                holding = false;

                if (holdTime >= sideHoldSec)
                {
                    if (repDone < peaks.Length) peaks[repDone] = holdPeak;

                    repDone++;
                    armed = false;
                    returnTime = 0f;

                    if (repDone >= repsPerSide) FinishSide(sign);
                }
            }
        }
    }

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
    // ★평균이 아니라 중앙값 — 한 번 잘못 나온 값에 안 휘둘리게
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
        if (guideText != null) guideText.text = GuideMessage();
        if (progressText != null) progressText.text = ProgressMessage();

        if (valueText != null)
        {
            valueText.text = (step == Step.Idle)
                ? ""
                : "지금 " + CurrentValue().ToString("+0.00;-0.00");
        }
    }

    string GuideMessage()
    {
        if (step != Step.Idle && step != Step.Done && notice != "") return notice;

        switch (step)
        {
            case Step.Idle:
                return "";

            case Step.Center:
                // ★§17.3 교훈: 안내 문구가 곧 인식률
                return "입을 아~ 벌리고\n혀끝을 입 한가운데에 두세요";

            case Step.Left:
                return "혀를 앞으로 빼지 말고\n혀끝을 왼쪽 입꼬리에 콕! 대었다가\n가운데로 돌아오세요";

            case Step.Right:
                return "혀를 앞으로 빼지 말고\n혀끝을 오른쪽 입꼬리에 콕! 대었다가\n가운데로 돌아오세요";

            case Step.Done:
                return "다 됐어요! 잘하셨어요";
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

    // ===== ★진단 (화면 왼쪽 아래) =====
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
            "   잴수있음 " + (CanMeasure() ? "O" : "X  " + notice) + "\n" +
            "base " + lrBase.ToString("F3") +
            "   지금 " + CurrentValue().ToString("+0.000;-0.000") + "\n" +
            "왼쪽 최대 " + leftMax.ToString("F3") + " → 판정선 " + thrLeft.ToString("F3") + "\n" +
            "오른쪽 최대 " + rightMax.ToString("F3") + " → 판정선 " + thrRight.ToString("F3") + "\n" +
            "방향 " + dirText;

        float boxH = 150f;
        GUI.Label(new Rect(10f, Screen.height - boxH - 10f, 480f, boxH), info, st);
    }
}