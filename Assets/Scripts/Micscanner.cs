using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  스테이지9 「기침 볼링」 — 마이크 소리 크기 + 기침 판정
//
//  ★이 프로젝트에서 처음으로 카메라가 아니라 소리로 판정한다.
//
//  하는 일:
//   ① 마이크 소리를 잘게 떠서 크기(RMS)를 잰다
//   ② 사람이 느끼는 방식(dB)으로 바꿔 0~100점으로 편다
//   ③ "짧고 크게 터졌다 금방 잦아드는 소리"만 기침으로 인정한다
//
//  ★원본 = 웹 js/mic.js. 상수와 판정 흐름을 그대로 옮겼다.
//    ⚠아래 수치들은 검증 4회에서 결함 11건을 고쳐 가며 맞춘 값이다.
//      손대기 전에 옆의 설명을 꼭 읽을 것.
// ============================================================
public class MicScanner : MonoBehaviour
{
    // ============================================================
    //  소리 재기 설정
    // ============================================================
    [Header("소리 재기 (웹 mic.js와 동일 — 바꾸지 말 것)")]
    public int fftSize = 2048;        // ★한 번에 들여다보는 소리 조각 길이(약 43ms)
                                      //   ⚠1024(21ms)로 줄이지 말 것 — 화면이 초당 30번만 그려지면
                                      //   프레임 사이가 33ms라 그 사이 소리를 통째로 놓친다.
                                      //   기침은 순식간이라 치명적(웹 검증에서 발견해 올린 값)

    public float dbFloor = 60f;       // 0~100점으로 펼 때의 바닥(−60dB = 0점, 0dB = 100점)
                                      //   ★원본 RMS를 그대로 쓰면 말소리가 0.05라 막대가 거의 안 움직인다.
                                      //   dB로 바꾸면 조용함 0~15 / 말소리 40~55 / 기침 75~95로 시원하게 벌어짐

    public float peakHoldSec = 1.2f;  // "가장 컸던 소리"를 이만큼 붙잡아 둠(기침은 순식간이라 눈으로 볼 시간)
    public float peakFall = 40f;      // 그 뒤로는 1초에 이만큼씩 스르르 내려옴

    [Header("마이크 장치")]
    public int sampleRate = 44100;    // 녹음 주파수(장치가 지원 안 하면 알아서 맞춤)
    public int clipSeconds = 1;       // 녹음을 담아 둘 고리 버퍼 길이(초)

    // ============================================================
    //  ★기침 판정 기준값 (캘리브레이션 전 기본값)
    //  마이크 감도가 기기마다 달라, 연습(캘리브레이션)이 이 셋을 개인 값으로 바꿔 준다.
    // ============================================================
    [Header("★기침 판정 기본값 (연습이 개인 값으로 바꿔 줌)")]
    public float coughOnset = 55f;    // 소리가 이만큼 커지면 "폭발" 후보 시작
    public float coughEnd = 45f;      // 이 아래로 내려오면 폭발이 끝난 것으로 봄
                                      //   (들어갈 때와 나올 때 문턱을 다르게 = 떨림 방지)
    public float coughPeakMin = 65f;  // 폭발의 최고점이 이 이상이어야 기침으로 인정

    [Header("★기침 모양 판정 (웹과 동일 — 손대기 전 설명 읽을 것)")]
    public float coughRise = 20f;     // ★핵심: 0.15초 전보다 이만큼 "갑자기" 커져야 폭발로 봄
                                      //   말소리는 완만히 커져서 여기서 걸린다
    public float riseBackSec = 0.15f; // 얼마 전의 크기와 견줄지
    public float coughDrop = 25f;     // ★폭발이 끝났다고 보는 또 하나의 기준: 최고점보다 이만큼 내려오면 끝
                                      //   ⚠절대 문턱(coughEnd)만 쓰면, 에어컨·TV로 배경이 45 위인 방에서
                                      //   폭발이 영영 안 끝나 기침이 전부 "말소리"로 버려지고 먹통이 된다
    public float dropMinSec = 0.15f;  // 위 기준은 폭발 시작 후 이만큼 지나야 씀
                                      //   ⚠바로 적용하면 아주 큰 소리가 1~2프레임 만에 끝나 "너무 짧다"로 거절됨
    public float coughMaxSec = 0.7f;  // 이보다 길게 이어지면 말·노래 같은 지속음으로 보고 무시
    public float coughMinSec = 0.05f; // 이보다 짧으면 딸깍 잡음(책상 두드림 등)으로 보고 무시
    public float coughRestSec = 1.0f; // 한 번 인정한 뒤 이만큼은 다시 안 셈(디바운스)
    public float histSec = 0.6f;      // 최근 소리 크기를 이만큼만 기억("0.15초 전"을 찾는 데 씀)
    public float quietWaitMax = 1.5f; // 지속음 판단 뒤 "잦아들기"를 기다리는 최대 시간
                                      //   ⚠상한이 없으면 늘 시끄러운 방에서 영영 안 풀려 기침이 하나도 안 잡힘
    public float onsetLockSec = 0.25f;// 소리 하나를 걸러낸 뒤 아주 잠깐 쉼
                                      //   ⚠이게 없으면 소리 하나가 기록에 5~9번 쌓여 말소리 판단이 엉망이 됨

    [Header("★말소리 억제")]
    public float chatterWinSec = 1.5f;// 최근 이 시간 안에
    public int chatterMax = 3;        // 폭발이 이만큼 있었으면 말소리로 보고 아예 시작하지 않음
                                      //   ★기침은 디바운스(1초) 때문에 이 창에 많아야 2번이라 안 걸린다

    // ============================================================
    //  바깥에서 읽어가는 결과
    // ============================================================
    [Header("결과")]
    public float level = 0f;          // 지금 소리 크기(0~100점)
    public float peak = 0f;           // 최근 가장 컸던 소리(0~100점)
    public float rawRms = 0f;         // 원본 소리 크기(0~1 — 진단용)
    public int coughTotal = 0;        // 인정된 기침 총 횟수
    public bool listening = false;    // 지금 "소리 폭발"을 지켜보는 중인가

    [Header("마이크 상태")]
    public bool micReady = false;     // 준비가 끝났는가
    public bool micDenied = false;    // 권한이 막혔는가
    public bool micFailed = false;    // 마이크가 없거나 다른 문제인가

    // ===== ★진단 표시 =====
    [Header("★진단 표시")]
    public bool showDebug = false;
    public Vector2 debugPos = new Vector2(10f, 10f);
    public float debugWidth = 420f;

    // ============================================================
    //  인정·거절 결과 (게임 스크립트가 가져감)
    // ============================================================
    public class CoughEvent
    {
        public float peak;   // 최고점(0~100)
        public float dur;    // 이어진 시간(초)
        public int n;        // 몇 번째 기침인가
    }

    public class RejectEvent
    {
        public string reason; // "long"=말소리 / "weak"=작음 / "short"=딸깍 / "rest"=디바운스
        public float peak;
        public float dur;
    }

    private CoughEvent coughEvent = null;
    private RejectEvent rejectEvent = null;

    // ============================================================
    //  내부 작업용
    // ============================================================
    private string device = null;     // 쓰고 있는 마이크 이름
    private AudioClip clip = null;    // 녹음이 담기는 고리 버퍼
    private int clipSamples = 0;      // 그 버퍼의 칸 수
    private float[] allBuf = null;    // 버퍼를 통째로 읽어올 그릇
    private float[] window = null;    // 그중 최근 조각만 담을 그릇

    private float peakHold = 0f;      // 최고 기록을 붙잡아 둔 남은 시간

    // 기침 판정 상태
    private float micT = 0f;          // 마이크가 켜진 뒤 흐른 시간(초)
    private struct Hist { public float t; public float lv; }
    private List<Hist> hist = new List<Hist>();
    private bool burstOn = false;     // 지금 폭발 중인가
    private float burstStart = 0f;
    private float burstPeak = 0f;
    private bool waitQuiet = false;   // 말소리 판단 뒤 잦아들기를 기다리는 중인가
    private float waitQuietT = 0f;
    private float onsetLockT = 0f;
    private List<float> burstTimes = new List<float>();
    private float lastCoughT = -99f;

    // 기본값 기억(되돌리기용)
    private float defOnset, defEnd, defPeakMin;

    // ============================================================
    //  바깥에서 쓰는 함수
    // ============================================================

    // 이번에 인정된 기침 가져가기 (가져가면 비워짐)
    public CoughEvent TakeCough()
    {
        CoughEvent e = coughEvent;
        coughEvent = null;
        return e;
    }

    // 방금 "기침이 아니다"로 넘어간 소리의 이유 가져가기
    public RejectEvent TakeReject()
    {
        RejectEvent e = rejectEvent;
        rejectEvent = null;
        return e;
    }

    // ★판정 문턱 바꾸기 (캘리브레이션이 부름)
    public void SetCoughThresholds(float onset, float end, float peakMin)
    {
        coughOnset = onset;
        coughEnd = end;
        coughPeakMin = peakMin;
    }

    // 기본값으로 되돌리기
    public void ResetCoughThresholds()
    {
        coughOnset = defOnset;
        coughEnd = defEnd;
        coughPeakMin = defPeakMin;
    }

    // 새 판 시작: 지난 판의 기록·판정 상태 지우기
    // ★문턱은 안 건드린다(개인 기준이 날아가면 안 됨)
    public void ResetMic()
    {
        peak = 0f;
        peakHold = 0f;
        hist.Clear();
        burstOn = false;
        waitQuiet = false;
        waitQuietT = 0f;
        onsetLockT = 0f;
        burstTimes.Clear();
        burstPeak = 0f;
        lastCoughT = -99f;
        coughTotal = 0;
        coughEvent = null;
        rejectEvent = null;
    }

    // ============================================================
    //  준비
    // ============================================================
    void Start()
    {
        // 기본값 기억(캘리브레이션을 되돌릴 때 씀)
        defOnset = coughOnset;
        defEnd = coughEnd;
        defPeakMin = coughPeakMin;

        StartMic();
    }

    public void StartMic()
    {
        if (micReady) return;

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            micFailed = true;
            Debug.LogError("[Mic] 마이크를 찾을 수 없습니다.");
            return;
        }

        device = Microphone.devices[0];

        // 장치가 지원하는 주파수 확인
        int minF, maxF;
        Microphone.GetDeviceCaps(device, out minF, out maxF);

        int rate = sampleRate;

        if (maxF > 0)
        {
            rate = Mathf.Clamp(sampleRate, minF > 0 ? minF : 8000, maxF);
        }

        // ★loop = true 로 고리 버퍼를 만든다(끝에 닿으면 앞으로 돌아가며 계속 덮어씀)
        clip = Microphone.Start(device, true, clipSeconds, rate);

        if (clip == null)
        {
            micFailed = true;
            Debug.LogError("[Mic] 녹음을 시작할 수 없습니다: " + device);
            return;
        }

        clipSamples = clip.samples;
        allBuf = new float[clipSamples];
        window = new float[fftSize];

        micReady = true;
        micDenied = false;
        micFailed = false;

        Debug.Log("[Mic] 마이크 준비 완료: " + device + " (" + rate + "Hz, " + clipSamples + " samples)");
    }

    // ============================================================
    //  매 프레임
    // ============================================================
    void Update()
    {
        if (!micReady || clip == null) return;

        float dt = Time.deltaTime;

        // --- ① 지금 이 순간의 소리 조각 가져오기 ---
        if (!ReadWindow()) return;

        // --- ② 소리 크기 = 조각의 RMS(제곱 평균 제곱근) ---
        double sum = 0.0;

        for (int i = 0; i < window.Length; i++)
        {
            sum += (double)window[i] * window[i];
        }

        rawRms = Mathf.Sqrt((float)(sum / window.Length));

        // --- ③ dB로 바꿔 0~100점으로 펴기 ---
        float db = 20f * Mathf.Log10(Mathf.Max(rawRms, 1e-6f));
        level = Mathf.Clamp(((db + dbFloor) / dbFloor) * 100f, 0f, 100f);

        // --- ④ "가장 컸던 소리" 붙잡아 두기 ---
        if (level >= peak)
        {
            peak = level;
            peakHold = peakHoldSec;
        }
        else if (peakHold > 0f)
        {
            peakHold -= dt;
        }
        else
        {
            peak = Mathf.Max(level, peak - peakFall * dt);
        }

        // --- ⑤ 기침인지 살펴보기 ---
        JudgeCough(dt);
    }

    // ============================================================
    //  ① 고리 버퍼에서 "가장 최근 조각"만 떠 오기
    //
    //  ★마이크 녹음은 고리 모양이라, 지금 녹음 머리(position) 바로 앞의
    //    fftSize칸이 "방금 난 소리"다. 끝을 지나 앞으로 돌아간 경우도 이어 붙인다.
    // ============================================================
    bool ReadWindow()
    {
        int pos = Microphone.GetPosition(device);

        if (pos < 0 || clipSamples <= 0) return false;

        // 버퍼를 통째로 읽어 온다 (고리 이음매를 직접 다루는 것보다 안전하다)
        if (!clip.GetData(allBuf, 0)) return false;

        int start = pos - window.Length;

        for (int i = 0; i < window.Length; i++)
        {
            int idx = start + i;

            if (idx < 0) idx += clipSamples;      // ★고리를 넘어갔으면 뒤쪽에서 이어 온다

            window[i] = allBuf[idx];
        }

        return true;
    }

    // ============================================================
    //  ★기침 판정
    //  흐름: [조용] → 갑자기 커짐 = 폭발 시작 → 끝날 때 길이·최고점으로 결정
    // ============================================================
    void JudgeCough(float dt)
    {
        micT += dt;

        // 지금 크기를 기록해 두고, 오래된 것은 버림
        hist.Add(new Hist { t = micT, lv = level });

        while (hist.Count > 0 && hist[0].t < micT - histSec)
        {
            hist.RemoveAt(0);
        }

        // --- ① "얼마 전"의 소리 크기 찾기 (갑자기 커졌는지 견줄 상대) ---
        float past = level;

        for (int i = hist.Count - 1; i >= 0; i--)
        {
            if (hist[i].t <= micT - riseBackSec)
            {
                past = hist[i].lv;
                break;
            }
        }

        // --- ② 말소리 판단 직후라면 소리가 잦아들 때까지 기다림 ---
        if (waitQuiet)
        {
            waitQuietT += dt;

            if (level <= coughEnd || waitQuietT >= quietWaitMax)
            {
                waitQuiet = false;   // ★시간 상한이 없으면 시끄러운 방에서 영영 안 풀린다
            }

            listening = false;
            return;
        }

        // --- ③ 폭발이 시작됐나? ("충분히 크다" + "갑자기 커졌다") ---
        if (!burstOn)
        {
            listening = false;

            if (onsetLockT > 0f)
            {
                onsetLockT -= dt;
                return;
            }

            if (!(level >= coughOnset && level - past >= coughRise))
            {
                return;
            }

            // 오래된 폭발 기록은 버리고
            while (burstTimes.Count > 0 && burstTimes[0] < micT - chatterWinSec)
            {
                burstTimes.RemoveAt(0);
            }

            int recent = burstTimes.Count;   // 이번 것 빼고 최근에 몇 번 있었나

            // ★인정되든 안 되든 모든 소리를 기록한다.
            //   인정된 것만 넣으면, 기침이 한 번 잘못 인정된 순간부터
            //   말소리 방어가 통째로 무력해진다(웹 검증에서 발견)
            burstTimes.Add(micT);

            // ⓐ 방금 센 기침의 여운·연달아 나오는 콜록 (디바운스)
            //    ★0.02초 여유를 빼는 이유: 시계를 매 프레임 더해 만들다 보니
            //      딱 1초를 재도 0.9999처럼 살짝 모자라게 나온다.
            //      여유가 없으면 1초 간격 기침의 절반이 "쉬었다"로 거절됐다
            if (micT - lastCoughT < coughRestSec - 0.02f)
            {
                rejectEvent = new RejectEvent { reason = "rest", peak = Mathf.Round(level), dur = 0f };
                onsetLockT = onsetLockSec;
                return;
            }

            // ⓑ 짧은 사이에 소리가 줄줄이면 = 말소리
            if (recent >= chatterMax)
            {
                rejectEvent = new RejectEvent
                {
                    reason = (micT - lastCoughT < 2f) ? "rest" : "long",
                    peak = Mathf.Round(level),
                    dur = 0f,
                };
                onsetLockT = onsetLockSec;
                return;
            }

            burstOn = true;
            burstStart = micT;
            burstPeak = level;
            listening = true;
            return;
        }

        // --- ④ 폭발 중 — 최고점을 갱신하며 언제 끝나는지 지켜봄 ---
        listening = true;
        burstPeak = Mathf.Max(burstPeak, level);

        float dur = micT - burstStart;

        // ⓐ 너무 길게 이어지면 = 말·노래 같은 지속음
        if (dur > coughMaxSec)
        {
            burstOn = false;
            waitQuiet = true;
            waitQuietT = 0f;
            listening = false;
            rejectEvent = new RejectEvent { reason = "long", peak = Mathf.Round(burstPeak), dur = dur };
            return;
        }

        // ★끝났는지 두 가지로 본다:
        //   조용해졌거나(절대 문턱) / 0.15초 지난 뒤 최고점에서 충분히 꺾였거나(시끄러운 방 대비)
        bool ended = level <= coughEnd
                     || (dur >= dropMinSec && level <= burstPeak - coughDrop);

        if (!ended) return;

        // --- ⑤ 폭발이 끝났다 — 기침이었는지 결정 ---
        burstOn = false;
        listening = false;

        if (dur < coughMinSec)
        {
            // ⓑ 너무 짧으면 딸깍 잡음
            rejectEvent = new RejectEvent { reason = "short", peak = Mathf.Round(burstPeak), dur = dur };
        }
        else if (burstPeak < coughPeakMin)
        {
            // ⓒ 최고점이 낮으면 작은 소리
            rejectEvent = new RejectEvent { reason = "weak", peak = Mathf.Round(burstPeak), dur = dur };
        }
        else
        {
            // ⓓ 통과! 기침으로 인정
            // ★인정 시각은 소리가 "시작된" 때로 기억한다.
            //   끝난 때로 재면 디바운스가 "1초 + 소리 길이"가 되어
            //   1초 간격 기침의 절반이 안 세졌다(웹 검증에서 발견)
            lastCoughT = burstStart;
            coughTotal += 1;

            coughEvent = new CoughEvent
            {
                peak = Mathf.Round(burstPeak),
                dur = dur,
                n = coughTotal,
            };
        }
    }

    // ============================================================
    //  ★진단 창 — 소리 크기 막대 + 판정 상태
    // ============================================================
    void OnGUI()
    {
        if (!showDebug) return;

        float x = debugPos.x;
        float y = debugPos.y;
        float w = debugWidth;

        GUI.Box(new Rect(x - 6, y - 6, w + 12, 116), GUIContent.none);

        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontSize = 15;
        st.normal.textColor = Color.white;

        if (!micReady)
        {
            string why = micDenied ? "마이크가 막혀 있어요"
                       : micFailed ? "마이크를 찾을 수 없어요"
                       : "마이크 준비 중…";

            GUI.Label(new Rect(x, y, w, 24), "★마이크 — " + why, st);
            return;
        }

        // --- 소리 크기 막대 ---
        float barY = y + 26f;
        float barH = 18f;

        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        GUI.DrawTexture(new Rect(x, barY, w, barH), Texture2D.whiteTexture, ScaleMode.StretchToFill, false);

        float lw = w * Mathf.Clamp01(level / 100f);

        GUI.color = level >= 70f ? new Color(0.91f, 0.35f, 0.05f)
                  : level >= 40f ? new Color(0.94f, 0.65f, 0f)
                                 : new Color(0.18f, 0.62f, 0.27f);

        GUI.DrawTexture(new Rect(x, barY, lw, barH), Texture2D.whiteTexture, ScaleMode.StretchToFill, false);

        // 흰 눈금 = 가장 컸던 소리
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(x + w * Mathf.Clamp01(peak / 100f) - 1.5f, barY - 4f, 3f, barH + 8f),
                        Texture2D.whiteTexture, ScaleMode.StretchToFill, false);

        // 금색 눈금 = 기침 인정선
        GUI.color = new Color(0.96f, 0.62f, 0f);
        GUI.DrawTexture(new Rect(x + w * Mathf.Clamp01(coughPeakMin / 100f) - 1.5f, barY - 4f, 3f, barH + 8f),
                        Texture2D.whiteTexture, ScaleMode.StretchToFill, false);

        GUI.color = Color.white;

        GUI.Label(new Rect(x, y, w, 24),
            "★소리 " + Mathf.RoundToInt(level) + "   최대 " + Mathf.RoundToInt(peak) +
            "   원본 " + rawRms.ToString("F4") + (listening ? "   [폭발 지켜보는 중]" : ""), st);

        GUI.Label(new Rect(x, barY + barH + 6f, w, 24),
            "기침 " + coughTotal + "번   문턱: 시작 " + Mathf.RoundToInt(coughOnset) +
            " / 끝 " + Mathf.RoundToInt(coughEnd) +
            " / 인정선 " + Mathf.RoundToInt(coughPeakMin), st);

        GUI.Label(new Rect(x, barY + barH + 30f, w, 24),
            "흰 눈금 = 가장 컸던 소리   금색 눈금 = 기침 인정선", st);
    }

    void OnDestroy()
    {
        if (device != null && Microphone.IsRecording(device))
        {
            Microphone.End(device);
        }
    }
}