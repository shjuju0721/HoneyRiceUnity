using UnityEngine;
using TMPro;

// ============================================================
//  스테이지9 「민들레 홀씨 날리기」 — 게임 본체
//
//  임상 목적 = 수의적 기침 훈련.
//  기침은 기도로 잘못 들어간 것을 뱉어내는 "기도 방어의 마지막 방어선"이고,
//  연하장애가 있는 분은 이 반사가 약해 의도적 기침 연습이 실제 치료 목표다.
//
//  흐름:
//   민들레 한 송이 → 콜록! → 홀씨가 우수수 날아가고 한 단계 비어감
//   → 3번이면 빈 줄기 → "잠깐 쉬어요 🍵 3·2·1" → 새 민들레
//   → 3송이를 다 날리면 완성
//
//  ★기침 세기는 "단계"가 아니라 "홀씨 개수"로 보상한다(사용자 확정).
//    세게 할수록 홀씨가 우수수 → 세게 하고 싶어짐 = 임상 목표와 같은 방향.
//
//  ⚠임상 안전: 총 기침 10회 이내 · 송이 사이에 반드시 쉬는 안내.
//    한 송이 3번 × 3송이 = 9번이라 딱 맞다. 이 숫자를 늘리지 말 것.
// ============================================================
public class DandelionGame : MonoBehaviour
{
    [Header("연결")]
    public MicScanner mic;                 // 마이크 소리·기침 판정
    public SpriteRenderer dandelion;       // 민들레 그림(단계마다 바꿔 낌)
    public SwayInWind sway;                // 민들레 흔들림(기침 때 휘청이게)
    public GameObject seedPrefab;          // 날아가는 홀씨 프리팹
    public Transform seedSpawnPoint;       // 홀씨가 태어나는 자리(솜뭉치 한가운데)
    public CoughCalibration calibration;   // ★이 줄 추가

    [Header("민들레 그림 4장 (가득 → 빈 줄기)")]
    public Sprite stage3;                  // 가득
    public Sprite stage2;                  // 3분의 2
    public Sprite stage1;                  // 3분의 1
    public Sprite stage0;                  // 빈 줄기

    [Header("UI")]
    public TMP_Text statusText;            // 안내 문구
    public TMP_Text countText;             // "민들레 1 / 3"
    public GameObject completePanel;       // 완료 패널

    [Header("게임 설정")]
    public int totalFlowers = 3;           // ★민들레 송이 수
    public int blowsPerFlower = 3;         // ★한 송이를 비우는 데 필요한 기침 횟수
                                           //   ⚠3 × 3 = 9회. 임상 안전(10회 이내)이라 늘리지 말 것
    public float restSec = 3.0f;           // 송이 사이 쉬는 시간(초)
    public float blowSec = 1.0f;           // 콜록 뒤 연출 시간
    public float goalWaitSec = 2.5f;       // 완성 뒤 완료 패널까지
    public bool autoStart = true;          // 씬이 열리면 바로 시작

    // ============================================================
    //  ★[임시] 키보드로 기침 흉내내기 (테스트용)
    //  ⚠조용한 방에서 계속 기침하기 어려워 만든 임시 장치.
    //    실제 배포 전에 반드시 끌 것(체크 해제).
    // ============================================================
    [Header("★[임시] 키보드 테스트")]
    public bool useKeyboardTest = false;   // 켜면 숫자키로 기침 흉내

    [Header("홀씨 개수 (기침 세기별)")]
    public int seedsSoft = 6;              // 살살
    public int seedsMid = 12;              // 보통
    public int seedsHard = 22;             // 세게
    public float seedSpread = 0.35f;       // 태어나는 자리를 얼마나 흩을지

    [Header("세기 등급 기준 (웹과 동일)")]
    public float lv2T = 0.35f;             // 내 범위의 35% 이상 = 보통
    public float lv3T = 0.70f;             // 70% 이상 = 세게
    public float myCoughMax = 90f;         // ★나의 기침 세기(캘리브레이션이 채워 줌)
                                           //   아직 안 쟀으면 이 기본값을 씀

    [Header("문구")]
    [TextArea] public string guideMsg = "준비되면 콜록!\n시원하게 기침해 보세요";
    [TextArea] public string goalMsg = "민들레를\n모두 날렸어요!";

    // ===== 게임 상태 =====
    private enum Phase
    {
        Idle,      // 아직 시작 안 함
        Ready,     // 기침을 기다리는 중
        Blowing,   // 콜록 뒤 연출 중
        Resting,   // 송이 사이 쉬는 중
        Goal       // 완성
    }

    private Phase phase = Phase.Idle;

    private int flowerIdx = 0;             // 지금 몇 송이째인가(0부터)
    private int blowsDone = 0;             // 이 송이에 몇 번 불었나
    private float timer = 0f;              // 연출·쉬기 시계
    private float noticeT = 0f;            // 안내 문구를 지켜 주는 시간

    // 기록
    public int coughSoft = 0;
    public int coughMid = 0;
    public int coughHard = 0;
    public float peakCough = 0f;           // 이 판에서 가장 센 기침

    // ============================================================
    //  시작
    // ============================================================
    void Start()
    {
        if (completePanel != null) completePanel.SetActive(false);
        if (statusText != null) statusText.text = "";
        if (countText != null) countText.text = "";

        if (autoStart)
        {
            // ★기준을 이미 쟀으면 바로 시작, 아니면 연습부터
            if (calibration != null && !calibration.HasBaseline())
            {
                calibration.onFinished = StartGame;   // 연습이 끝나면 게임 시작
                calibration.StartCalibration();
            }
            else
            {
                if (calibration != null) myCoughMax = calibration.myCoughMax;
                StartGame();
            }
        }
    }

    // ★프리뷰의 시작 버튼이나 다시하기 버튼이 부른다
    public void StartGame()
    {

        // ★연습에서 잰 내 기침 세기를 반드시 가져온다
        //   ⚠이게 빠져서 연습 직후 첫 판만 기본값 90으로 등급이 계산되던 버그
        if (calibration != null && calibration.HasBaseline())
        {
            myCoughMax = calibration.myCoughMax;
        }

        flowerIdx = 0;
        blowsDone = 0;

        coughSoft = 0;
        coughMid = 0;
        coughHard = 0;
        peakCough = 0f;

        noticeT = 0f;

        if (mic != null) mic.ResetMic();

        ShowFlower();
        UpdateCountText();

        phase = Phase.Ready;
        ShowStatus(guideMsg);

        if (completePanel != null) completePanel.SetActive(false);
    }

    // ============================================================
    //  매 프레임
    // ============================================================
    void Update()
    {
        if (noticeT > 0f) noticeT -= Time.deltaTime;

        // --- 마이크 판정 결과는 매 프레임 가져와 비운다 ---
        //   ★안 가져가면 낡은 결과가 남아 엉뚱한 때 쓰인다
        MicScanner.CoughEvent cough = (mic != null) ? mic.TakeCough() : null;
        MicScanner.RejectEvent reject = (mic != null) ? mic.TakeReject() : null;

        switch (phase)
        {
            case Phase.Ready:
                UpdateReady(cough, reject);
                break;

            case Phase.Blowing:
                UpdateBlowing();
                break;

            case Phase.Resting:
                UpdateResting();
                break;

            case Phase.Goal:
                UpdateGoal();
                break;
        }
    }

    // ===== 기침을 기다리는 중 =====
    void UpdateReady(MicScanner.CoughEvent cough, MicScanner.RejectEvent reject)
    {
        // --- 마이크가 준비 안 됐으면 그 안내가 우선 ---
        if (!useKeyboardTest && (mic == null || !mic.micReady))
        {
            string why = (mic != null && mic.micDenied) ? "마이크가 막혀 있어요"
                       : (mic != null && mic.micFailed) ? "마이크를 찾을 수 없어요"
                       : "마이크 준비 중…";

            ShowStatus(why);
            return;
        }

        // --- ★[임시] 키보드 테스트 ---
        //   1 = 살살 / 2 = 보통 / 3 = 세게
        if (useKeyboardTest)
        {
            int fakeLv = 0;

            if (Input.GetKeyDown(KeyCode.Alpha1)) fakeLv = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) fakeLv = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) fakeLv = 3;

            if (fakeLv > 0)
            {
                if (fakeLv >= 3) coughHard++;
                else if (fakeLv == 2) coughMid++;
                else coughSoft++;

                Blow(fakeLv);
                return;
            }
        }

        // --- 기침이 인정됐다면! ---
        if (cough != null)
        {
            int lv = CoughLevel(cough.peak);

            if (lv >= 3) coughHard++;
            else if (lv == 2) coughMid++;
            else coughSoft++;

            peakCough = Mathf.Max(peakCough, cough.peak);

            Blow(lv);
            return;
        }

        // --- 소리는 났는데 기침으로 안 셌다면 이유를 부드럽게 ---
        if (reject != null)
        {
            ShowStatus(RejectMessage(reject.reason));
            noticeT = 1.6f;
            return;
        }

        // --- 평소에는 안내 문구 ---
        if (noticeT <= 0f)
        {
            ShowStatus(guideMsg);
        }
    }

    // ===== 콜록! 한 단계 날리기 =====
    void Blow(int level)
    {
        blowsDone++;

        // --- 홀씨 뿌리기 ---
        int n = (level >= 3) ? seedsHard : (level == 2) ? seedsMid : seedsSoft;

        SpawnSeeds(n, level);

        // --- 민들레가 휘청 ---
        if (sway != null)
        {
            sway.Gust(6f + level * 4f);
        }

        // --- 그림 한 단계 비우기 ---
        ShowFlower();

        // --- 칭찬 문구(세기별) ---
        string msg = (level >= 3) ? "세게! 우수수~"
                   : (level == 2) ? "좋아요!"
                                  : "잘했어요!";

        ShowStatus(msg);
        noticeT = blowSec;

        phase = Phase.Blowing;
        timer = blowSec;
    }

    // ===== 콜록 뒤 연출 중 =====
    void UpdateBlowing()
    {
        timer -= Time.deltaTime;

        if (timer > 0f) return;

        if (blowsDone >= blowsPerFlower)
        {
            // 이 송이는 다 날렸다
            flowerIdx++;
            UpdateCountText();

            if (flowerIdx >= totalFlowers)
            {
                // 다 끝났다!
                phase = Phase.Goal;
                timer = goalWaitSec;
                ShowStatus(goalMsg);
            }
            else
            {
                // ★쉬는 시간 — 임상 안전(과한 기침은 어지러움·피로를 부른다)
                phase = Phase.Resting;
                timer = restSec;
            }
        }
        else
        {
            phase = Phase.Ready;
            ShowStatus(guideMsg);
        }
    }

    // ===== 송이 사이 쉬는 중 =====
    void UpdateResting()
    {
        timer -= Time.deltaTime;

        int left = Mathf.Max(1, Mathf.CeilToInt(timer));

        ShowStatus("참 잘했어요!\n잠깐 쉬어요 " + left);

        if (timer <= 0f)
        {
            // 새 민들레
            blowsDone = 0;
            ShowFlower();

            phase = Phase.Ready;
            ShowStatus(guideMsg);
        }
    }

    // ===== 완성 =====
    void UpdateGoal()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            phase = Phase.Idle;

            // ★판 기록 저장 — 기침 횟수와 이 판 최고 세기
            RecordStore.Save(9, coughSoft + coughMid + coughHard, peakCough);

            if (completePanel != null)
            {
                completePanel.SetActive(true);
            }
        }
    }

    // ============================================================
    //  홀씨 뿌리기
    // ============================================================
    void SpawnSeeds(int count, int level)
    {
        if (seedPrefab == null || seedSpawnPoint == null) return;

        for (int i = 0; i < count; i++)
        {
            // 솜뭉치 근처에서 조금씩 흩어져 태어난다
            Vector3 p = seedSpawnPoint.position
                      + new Vector3(Random.Range(-seedSpread, seedSpread),
                                    Random.Range(-seedSpread, seedSpread), 0f);

            GameObject go = Instantiate(seedPrefab, p, Quaternion.identity, transform);

            DandelionSeed s = go.GetComponent<DandelionSeed>();

            if (s == null) s = go.AddComponent<DandelionSeed>();

            s.Launch(level);
        }
    }

    // ============================================================
    //  민들레 그림 바꾸기 (남은 단계에 맞게)
    // ============================================================
    void ShowFlower()
    {
        if (dandelion == null) return;

        int left = blowsPerFlower - blowsDone;   // 몇 단계 남았나

        Sprite s;

        if (left >= 3) s = stage3;
        else if (left == 2) s = stage2;
        else if (left == 1) s = stage1;
        else s = stage0;

        if (s != null) dandelion.sprite = s;
    }

    // ============================================================
    //  기침 세기 등급 (웹 coughLevel과 같은 계산)
    //  내 범위 = 인정선 ~ 나의 기침 세기. 그 안 위치로 1/2/3
    // ============================================================
    int CoughLevel(float peak)
    {
        float floor = (mic != null) ? mic.coughPeakMin : 65f;
        float range = Mathf.Max(1f, myCoughMax - floor);
        float t = Mathf.Clamp01((peak - floor) / range);

        if (t >= lv3T) return 3;
        if (t >= lv2T) return 2;
        return 1;
    }

    // ============================================================
    //  거절 이유 문구 (웹과 같은 말투 — 나무라지 않는다)
    // ============================================================
    string RejectMessage(string reason)
    {
        if (reason == "long") return "말소리 같았어요\n짧게 콜록! 해보세요";
        if (reason == "weak") return "소리가 조금 작았어요\n조금만 더 시원하게!";
        if (reason == "rest") return "좋아요, 잠깐 쉬었다가\n한 번 더 콜록!";

        return "짧은 소리였어요\n콜록! 하고 해보세요";
    }

    // ============================================================
    //  도우미
    // ============================================================
    void ShowStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    void UpdateCountText()
    {
        if (countText != null)
        {
            countText.text = "민들레 " + Mathf.Min(flowerIdx + 1, totalFlowers) + " / " + totalFlowers;
        }
    }
}