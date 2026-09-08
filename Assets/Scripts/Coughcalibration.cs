using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ============================================================
//  스테이지9 「기침 캘리브레이션」 — 판정 문턱을 내 기준으로
//
//  왜 필요한가:
//   마이크 감도·목소리 크기·방 소음이 사람마다 기기마다 다르다.
//   고정 문턱(65)으로는 "작게 기침하면 안 세어지는" 일이 생긴다.
//   ★웹 2단계 테스트에서 실제로 나온 문제이고, 이 캘리브레이션이 그 해결책이다.
//
//  흐름:
//   ① 안내 읽을 여유 2초
//   ② 조용히 — 배경 소음 재기 2초
//   ③ 콜록! 2번 — 나의 기침 세기 재기
//   ④ 완료 → 문턱을 개인 값으로 교체 + 저장
//
//  ⚠연습 기침이 2회인 이유: 게임에서 9번 하므로 총 10회 이내를 지키려면
//    연습은 최소여야 한다(임상 안전). 늘리지 말 것.
// ============================================================
public class CoughCalibration : MonoBehaviour
{
    [Header("연결")]
    public MicScanner mic;
    public TMP_Text guideText;      // 큰 안내 문구
    public TMP_Text progressText;   // ●● 진행 표시

    [Header("시간 (웹과 동일)")]
    public float waitSec = 2.0f;    // 안내문을 읽을 여유
                                    //   ★"연습 시작" 버튼 누르는 소리가 소음 측정에 섞이지 않게 하는 역할도 한다
    public float noiseSec = 2.0f;   // 배경 소음을 재는 시간
    public float doneSec = 1.5f;    // 완료 문구를 보여줄 시간

    [Header("연습 기침")]
    public int practiceReps = 2;    // ★2회. ⚠임상 안전(총 10회 이내) — 늘리지 말 것

    [Header("판정 문턱 만드는 공식 (웹 applyCoughCalib과 동일)")]
    public float thrRatio = 0.50f;  // ★인정선 = 내 기침 세기의 50%
                                    //   복어·미소의 60%보다 낮다 = 재활 격려 우선(사용자 확정)
    public float noiseTooLoud = 55f;// 소음이 이보다 크면 한 번만 다시 잰다
    public float retryPauseSec = 1.5f; // 다시 재기 전 숨 고르는 시간

    // ===== 결과 =====
    [Header("잰 값")]
    public float micNoise = 0f;     // 배경 소음(0~100점. 조용한 방이면 0도 정상)
    public float myCoughMax = 0f;   // 나의 기침 세기(연습 중 가장 컸던 최고점)
    public bool ready = false;      // 캘리브레이션을 마쳤는가

    // 끝났을 때 게임에게 알려 주는 장치
    public System.Action onFinished;

    // ===== 진행 단계 =====
    private enum Phase { Idle, Wait, Noise, Cough, Done }
    private Phase phase = Phase.Idle;

    private float timer = 0f;
    private List<float> noiseBuf = new List<float>();
    private bool noiseRetried = false;      // 다시 재기 기회는 딱 한 번
    private List<float> coughPeaks = new List<float>();
    private float noticeT = 0f;             // 안내를 지켜 주는 시간

    // 저장 이름표
    private const string KEY_NOISE = "dandelion_mic_noise";
    private const string KEY_MAX = "dandelion_cough_max";
    private const string KEY_READY = "dandelion_cough_ready";

    // ============================================================
    //  저장된 값 불러오기 (씬이 열릴 때)
    // ============================================================
    void Awake()
    {
        LoadSaved();
    }

    public void LoadSaved()
    {
        if (PlayerPrefs.GetInt(KEY_READY, 0) != 1) return;

        micNoise = PlayerPrefs.GetFloat(KEY_NOISE, 0f);
        myCoughMax = PlayerPrefs.GetFloat(KEY_MAX, 0f);

        // ★"한 벌" 검사 — 기침 세기가 없으면 인정선을 잡을 수 없다
        if (myCoughMax <= 0f)
        {
            ready = false;
            return;
        }

        ready = true;
        Apply();
    }

    // 저장된 기준이 있는가 (게임이 "바로 시작"을 열지 판단)
    public bool HasBaseline()
    {
        return ready && myCoughMax > 0f;
    }

    // ============================================================
    //  연습 시작
    // ============================================================
    public void StartCalibration()
    {
        phase = Phase.Wait;
        timer = 0f;

        noiseBuf.Clear();
        noiseRetried = false;
        coughPeaks.Clear();
        noticeT = 0f;

        micNoise = 0f;
        myCoughMax = 0f;
        ready = false;

        if (mic != null)
        {
            mic.ResetCoughThresholds();   // 일단 기본값으로
            mic.ResetMic();
        }

        ShowGuide("먼저, 조용히\n잠깐 기다려 주세요");
        ShowProgress("");
    }

    // ============================================================
    //  매 프레임
    // ============================================================
    void Update()
    {
        if (phase == Phase.Idle) return;

        float dt = Time.deltaTime;

        if (noticeT > 0f) noticeT -= dt;

        // --- 판정 결과는 단계와 상관없이 매 프레임 가져와 비운다 ---
        //   ★안 가져가면 낡은 결과가 남아 엉뚱한 때 쓰인다
        MicScanner.CoughEvent cough = (mic != null) ? mic.TakeCough() : null;
        MicScanner.RejectEvent reject = (mic != null) ? mic.TakeReject() : null;

        // --- 마이크가 준비 안 됐으면 기다림 ---
        if (mic == null || !mic.micReady)
        {
            string why = (mic != null && mic.micDenied) ? "마이크가 막혀 있어요"
                       : (mic != null && mic.micFailed) ? "마이크를 찾을 수 없어요"
                       : "마이크 준비 중… 잠시만요";

            ShowProgress(why);
            return;
        }

        switch (phase)
        {
            case Phase.Wait:
                UpdateWait(dt);
                break;

            case Phase.Noise:
                UpdateNoise(dt);
                break;

            case Phase.Cough:
                UpdateCough(dt, cough, reject);
                break;

            case Phase.Done:
                UpdateDone(dt);
                break;
        }
    }

    // ===== ① 안내 읽을 여유 =====
    void UpdateWait(float dt)
    {
        if (noticeT <= 0f)
        {
            ShowProgress("천천히 읽고, 조용히 계시면 돼요");
        }

        timer += dt;

        if (timer >= waitSec)
        {
            phase = Phase.Noise;
            timer = 0f;
            noiseBuf.Clear();
        }
    }

    // ===== ② 배경 소음 재기 =====
    void UpdateNoise(float dt)
    {
        timer += dt;

        // ★음수 = 다시 재기 전 숨 고르는 중(조용해질 틈을 준다)
        if (timer < 0f) return;

        noiseBuf.Add(mic.level);

        if (noticeT <= 0f)
        {
            int left = Mathf.Max(1, Mathf.CeilToInt(noiseSec - timer));
            ShowProgress("주변 소리를 재는 중… " + left);
        }

        if (timer < noiseSec) return;

        // --- 표본을 작은 순서로 세워 아래쪽 80%만 평균 ---
        //   ★재는 중에 헛기침·부스럭 소리가 한 번 끼어도 값이 덜 튄다
        List<float> sorted = new List<float>(noiseBuf);
        sorted.Sort();

        int useN = Mathf.Max(1, Mathf.FloorToInt(sorted.Count * 0.8f));
        float sum = 0f;

        for (int i = 0; i < useN; i++) sum += sorted[i];

        float avg = sum / useN;

        // --- 너무 시끄럽게 재졌으면 딱 한 번만 다시 ---
        if (avg >= noiseTooLoud && !noiseRetried)
        {
            noiseRetried = true;
            timer = -retryPauseSec;    // ★숨 고른 뒤 다시 재기
            noiseBuf.Clear();

            ShowProgress("조금 시끄러웠어요 — 조용히 한 번만 더 잴게요");
            noticeT = retryPauseSec;   // ★이 보호가 없으면 다음 프레임 카운트다운이 안내를 덮는다
            return;
        }

        micNoise = Mathf.Round(avg);

        // --- 다음 단계: 기침 재기 ---
        phase = Phase.Cough;
        timer = 0f;

        // ★연습용 "관대 문턱": 아직 내 기침 세기를 모르니 배경 소음 바로 위면 일단 들어보고
        //   "기침답게 짧게 터졌는가"(소리 모양)만 본다.
        //   작은 기침이 고정 문턱(65)에 막히지 않게 하는 장치
        mic.SetCoughThresholds(
            Mathf.Max(micNoise + 8f, 18f),    // onset
            Mathf.Max(micNoise + 4f, 14f),    // end
            Mathf.Max(micNoise + 12f, 22f)    // peakMin
        );

        ShowGuide("이제 콜록! 하고\n시원하게 기침해 보세요");
        ShowProgress("콜록! — " + practiceReps + "번, 천천히 본인 속도로");
        noticeT = 1.5f;   // ★"천천히 본인 속도로"가 다음 프레임에 덮이지 않게

        UpdateDots();
    }

    // ===== ③ 콜록! 2번 =====
    void UpdateCough(float dt, MicScanner.CoughEvent cough, MicScanner.RejectEvent reject)
    {
        if (cough != null)
        {
            coughPeaks.Add(cough.peak);
            UpdateDots();

            if (coughPeaks.Count >= practiceReps)
            {
                Finish();
                return;
            }

            ShowProgress("잘했어요! 콜록! " + (practiceReps - coughPeaks.Count) + "번만 더!");
            noticeT = 1.5f;
            return;
        }

        if (reject != null)
        {
            ShowProgress(RejectMessage(reject.reason));
            noticeT = 1.5f;
            return;
        }

        if (noticeT <= 0f)
        {
            ShowProgress("콜록! — " + (practiceReps - coughPeaks.Count) + "번 남았어요");
        }
    }

    // ===== ④ 완료 =====
    void Finish()
    {
        myCoughMax = 0f;

        for (int i = 0; i < coughPeaks.Count; i++)
        {
            myCoughMax = Mathf.Max(myCoughMax, coughPeaks[i]);
        }

        ready = true;

        Apply();
        Save();

        phase = Phase.Done;
        timer = 0f;

        ShowGuide("참 잘했어요! 👏");
        ShowProgress("나의 기침 세기 " + Mathf.RoundToInt(myCoughMax)
                     + " · 배경 소음 " + Mathf.RoundToInt(micNoise));
    }

    void UpdateDone(float dt)
    {
        timer += dt;

        if (timer >= doneSec)
        {
            phase = Phase.Idle;

            if (onFinished != null)
            {
                onFinished();
            }
        }
    }

    // ============================================================
    //  ★잰 값으로 판정 문턱을 개인 값으로 (웹 applyCoughCalib과 같은 공식)
    // ============================================================
    public void Apply()
    {
        if (mic == null) return;
        if (!(ready && myCoughMax > 0f)) return;

        // 인정선 = 내 기침 세기의 50%
        // 단 배경 소음 +12보다는 위로(부스럭 소리가 안 세지게), 최소 20은 넘게
        float peakMin = Mathf.Max(micNoise + 12f, myCoughMax * thrRatio, 20f);

        // ★인정선이 내 기침 세기에 너무 붙었다면(기침이 배경 소음보다 살짝만 큰 환경)
        //   내 기침이 항상 통과하게 그 아래로 내린다 — 실패 없음 원칙
        if (peakMin > myCoughMax - 5f)
        {
            peakMin = Mathf.Max(myCoughMax - 5f, 20f);
        }

        // 폭발 시작 문턱 — 인정선보다 조금 아래(작게 시작한 기침도 지켜보게)
        float onset = Mathf.Max(micNoise + 8f, peakMin - 10f, 18f);

        // 폭발 끝 문턱 — 시작보다 아래(들어올 때·나갈 때 이중 문턱 = 떨림 방지)
        float end = Mathf.Max(micNoise + 4f, onset - 10f, 14f);

        mic.SetCoughThresholds(Mathf.Round(onset), Mathf.Round(end), Mathf.Round(peakMin));

        Debug.Log("[Calib] 내 기준 적용 — 시작 " + Mathf.Round(onset)
                  + " / 끝 " + Mathf.Round(end)
                  + " / 인정선 " + Mathf.Round(peakMin));
    }

    // ============================================================
    //  저장·불러오기
    // ============================================================
    void Save()
    {
        PlayerPrefs.SetFloat(KEY_NOISE, micNoise);
        PlayerPrefs.SetFloat(KEY_MAX, myCoughMax);
        PlayerPrefs.SetInt(KEY_READY, ready ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ============================================================
    //  도우미
    // ============================================================
    void UpdateDots()
    {
        if (progressText == null) return;

        // 동그라미는 guideText 아래 진행 표시에 붙인다
        string dots = "";

        for (int i = 0; i < practiceReps; i++)
        {
            dots += (i < coughPeaks.Count) ? "● " : "○ ";
        }

        // 진행 문구와 동그라미를 함께 보여 준다
        // (문구는 UpdateCough가 계속 갱신하므로 여기서는 동그라미만 기억)
        dotsCache = dots;
    }

    private string dotsCache = "";

    void ShowGuide(string msg)
    {
        if (guideText != null) guideText.text = msg;
    }

    void ShowProgress(string msg)
    {
        if (progressText != null)
        {
            progressText.text = (dotsCache == "") ? msg : (dotsCache + "\n" + msg);
        }
    }

    string RejectMessage(string reason)
    {
        if (reason == "long") return "말소리 같았어요 — 짧게 콜록! 해보세요";
        if (reason == "weak") return "소리가 조금 작았어요 — 조금만 더 시원하게!";
        if (reason == "rest") return "좋아요, 잠깐 쉬었다가 한 번 더 콜록!";

        return "짧은 소리였어요 — 콜록! 하고 해보세요";
    }
}