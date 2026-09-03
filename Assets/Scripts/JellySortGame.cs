using UnityEngine;
using TMPro;

// ============================================================
//  스테이지7 「젤리 분류」 — 7-5c 피라미드 쌓기
//
//  흐름:
//   위에서 젤리가 뚝 떨어짐 → 카운터에 착지(통통) → 판정 대기
//     → 맞으면 그쪽 접시로 슝 (날아가며 작아짐) → 다음 젤리
//     → 틀리면 폴짝폴짝하고 제자리 (다시 시도)
//
//  ★젤리는 "껍데기(빈 오브젝트) + 그림 자식" 구조로 만든다.
//    애니메이터가 건드리는 크기와 우리가 정하는 크기가 안 싸우게.
// ============================================================
public class JellySortGame : MonoBehaviour
{
    [Header("연결")]
    public TongueLRCalibration calibration;
    public TongueScanner scanner;

    [Header("젤리")]
    public GameObject grapePrefab;            // Jelly 2 (포도 = 왼쪽)
    public GameObject puddingPrefab;          // Jelly 4 (푸딩 = 오른쪽)
    public float waitScale = 3f;              // ★판정 대기 중인 젤리 크기 (크게)
    public float pileScale = 2f;              // 접시에 쌓일 때 크기

    [Header("자리")]
    public Transform dropPoint;               // 떨어지기 시작하는 곳 (화면 위)
    public Transform landPoint;               // 착지해서 판정 기다리는 곳
    public Transform plateLeft;               // 포도 접시
    public Transform plateRight;              // 푸딩 접시

    [Header("접시에 쌓기 (피라미드: 아래 3 + 위 2)")]
    public float pileGapX = 0.85f;            // 옆으로 벌어지는 간격
    public float pileGapY = 0.7f;             // 위로 올라가는 간격
    public float pileBaseY = 0.15f;           // 접시 바닥에서 띄우는 높이

    [Header("게임 규칙")]
    public int totalJelly = 10;
    public float nextDelay = 0.25f;           // 다음 젤리까지 쉬는 시간

    [Header("화면 표시 (없어도 동작함)")]
    public TMP_Text bigText;
    public TMP_Text countText;
    public TMP_Text noticeText;

    [Header("판정 설정")]
    public float dirHoldSec = 0.3f;           // ★같은 방향이 이만큼 이어져야 인정
    public float centerHoldSec = 0.15f;       // 가운데를 이만큼 봐야 잠금 해제

    [Header("표시 시간")]
    public float popSec = 0.9f;

    // ===== 결과 =====
    [Header("★결과")]
    public int sortedCount = 0;
    public int leftDone = 0;
    public int rightDone = 0;
    public int wrongCount = 0;

    // ★max_effort 용 — 게임 중 실제로 낸 최대치 (연습값을 베끼지 말 것)
    public float leftBest = 0f;
    public float rightBest = 0f;

    public System.Action onCleared;

    // ===== 내부 =====
    private bool running = false;
    private bool armed = false;
    private int holdDir = 0;
    private float holdTime = 0f;
    private float centerTime = 0f;

    private float popTime = 0f;
    private string popMsg = "";

    private Jelly current = null;
    private int spawned = 0;
    private float waitNext = 0f;

    private int[] order;
    private int leftPiled = 0;
    private int rightPiled = 0;

    void Update()
    {
        if (calibration == null) return;

        float dt = Time.deltaTime;

        if (popTime > 0f) popTime -= dt;

        if (running)
        {
            if (current == null)
            {
                waitNext -= dt;

                if (waitNext <= 0f && spawned < totalJelly)
                {
                    SpawnNext();
                }
            }

            UpdateJudge(dt);
        }

        UpdateTexts();
    }

    // ===== 바깥에서 부르는 것들 =====

    public void StartGame()
    {
        sortedCount = 0;
        leftDone = 0;
        rightDone = 0;
        wrongCount = 0;
        leftBest = 0f;
        rightBest = 0f;

        spawned = 0;
        leftPiled = 0;
        rightPiled = 0;
        current = null;
        waitNext = 0f;

        MakeOrder();
        ResetJudge();

        running = true;
    }

    public void StopGame()
    {
        running = false;
        ResetJudge();
    }

    void ResetJudge()
    {
        armed = false;      // ★시작도 잠금 상태 — 가운데를 한 번 보여야 첫 판정
        holdDir = 0;
        holdTime = 0f;
        centerTime = 0f;
        popTime = 0f;
    }

    // ===== 젤리 순서 정하기 =====
    // 포도 5 + 푸딩 5를 섞되, ★같은 것이 3연속으로 나오지 않게 한다.
    void MakeOrder()
    {
        order = new int[totalJelly];

        int half = totalJelly / 2;

        for (int i = 0; i < totalJelly; i++)
        {
            order[i] = (i < half) ? +1 : -1;
        }

        for (int attempt = 0; attempt < 60; attempt++)
        {
            // Fisher-Yates 섞기
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }

            if (!HasThreeInARow()) break;
        }
    }

    bool HasThreeInARow()
    {
        for (int i = 2; i < order.Length; i++)
        {
            if (order[i] == order[i - 1] && order[i] == order[i - 2]) return true;
        }

        return false;
    }

    // ===== 젤리 하나 떨어뜨리기 =====
    void SpawnNext()
    {
        int dir = order[spawned];

        GameObject prefab = (dir > 0) ? grapePrefab : puddingPrefab;

        if (prefab == null || dropPoint == null || landPoint == null) return;

        // ★껍데기를 만들고 그 안에 젤리 그림을 넣는다.
        //   껍데기 = 위치·크기 담당(우리 코드) / 자식 = 그림·애니메이션 담당
        GameObject root = new GameObject(dir > 0 ? "Grape" : "Pudding");

        GameObject art = Instantiate(prefab, root.transform);
        art.transform.localPosition = Vector3.zero;
        art.transform.localScale = Vector3.one;

        Jelly j = root.AddComponent<Jelly>();

        j.correctDir = dir;
        j.onArrived = OnJellyArrived;
        j.DropFrom(dropPoint.position, landPoint.position, waitScale);

        current = j;
        spawned++;
    }

    // ===== ★판정 + 디바운스 =====
    void UpdateJudge(float dt)
    {
        int dir = calibration.Direction();       // +1 왼쪽 / 0 가운데 / −1 오른쪽
        float v = calibration.CurrentValue();

        // ★"진짜 가운데"인지 확인
        //   Direction()이 0을 주는 경우는 두 가지:
        //    ⓐ 값이 판정선 안쪽 = 진짜 가운데
        //    ⓑ 못 재는 상태(입 다뭄·혀 안 보임)
        //   ⓑ를 가운데로 치면 입 벌림이 흔들릴 때마다 잠금이 풀려 중복 인정된다.
        bool trulyCenter = calibration.CanMeasure()
                        && Mathf.Abs(v) < Mathf.Min(calibration.thrLeft, calibration.thrRight);

        if (!armed)
        {
            if (trulyCenter)
            {
                centerTime += dt;

                if (centerTime >= centerHoldSec)
                {
                    armed = true;
                    centerTime = 0f;
                    holdDir = 0;
                    holdTime = 0f;
                }
            }
            else
            {
                centerTime = 0f;
            }

            return;
        }

        if (dir == 0)
        {
            holdDir = 0;
            holdTime = 0f;
            return;
        }

        if (dir != holdDir)
        {
            holdDir = dir;
            holdTime = 0f;
        }

        holdTime += dt;

        // ★게임 중 실제로 낸 최대치 (연습값 복사 금지 — 체크리스트 5-6)
        if (dir > 0 && v > leftBest) leftBest = v;
        if (dir < 0 && -v > rightBest) rightBest = -v;

        if (holdTime >= dirHoldSec)
        {
            Accept(dir);
        }
    }

    // 방향 1회 인정
    void Accept(int dir)
    {
        // 잠근다 — 가운데로 돌아와야 다음 판정
        armed = false;
        centerTime = 0f;
        holdDir = 0;
        holdTime = 0f;

        if (dir > 0) leftDone++;
        else rightDone++;

        // 판정 대기 중인 젤리가 없으면 운동 횟수만 세고 끝
        if (current == null || !current.IsWaiting()) return;

        if (dir == current.correctDir)
        {
            current.FlyTo(PileSpot(dir), pileScale);

            sortedCount++;
            popMsg = "잘했어요!";
            popTime = popSec;

            current = null;
            waitNext = nextDelay;
        }
        else
        {
            current.Hop();

            wrongCount++;
            popMsg = "다시 해볼까요?";
            popTime = popSec;
        }
    }

    // ===== 접시 위 쌓을 자리 (피라미드: 아래 3 + 위 2) =====
    Vector3 PileSpot(int dir)
    {
        Transform plate = (dir > 0) ? plateLeft : plateRight;

        if (plate == null) return landPoint.position;

        int n = (dir > 0) ? leftPiled : rightPiled;

        if (dir > 0) leftPiled++;
        else rightPiled++;

        float ox, oy;

        if (n < 3)
        {
            // 아래 줄 3개: 왼쪽 · 가운데 · 오른쪽
            ox = (n - 1) * pileGapX;
            oy = pileBaseY;
        }
        else
        {
            // 위 줄 2개: 아래 줄 사이 틈에 얹기
            ox = (n - 3 == 0 ? -0.5f : 0.5f) * pileGapX;
            oy = pileBaseY + pileGapY;
        }

        return plate.position + new Vector3(ox, oy, 0f);
    }

    // 젤리가 접시에 도착했을 때
    void OnJellyArrived(Jelly j)
    {
        if (sortedCount >= totalJelly)
        {
            running = false;

            popMsg = "와~ 젤리를 모두 나눴어요!";
            popTime = 3f;

            if (onCleared != null) onCleared();
        }
    }

    // ===== 화면 문구 =====
    void UpdateTexts()
    {
        if (bigText != null)
        {
            bigText.text = (popTime > 0f) ? popMsg : "";
        }

        if (countText != null)
        {
            countText.text = "남은 젤리 " + Mathf.Max(0, totalJelly - sortedCount) + "개";
        }

        if (noticeText != null)
        {
            noticeText.text = NoticeMessage();
        }
    }

    string NoticeMessage()
    {
        if (!running) return "";

        if (!calibration.ready) return "먼저 기준을 재요";

        if (!calibration.CanMeasure()) return calibration.NoticeText();

        if (!armed) return "혀를 가운데로 돌아와 주세요";

        // ★§17.3 교훈: 안내 문구가 곧 인식률
        return "혀를 앞으로 빼지 말고\n혀끝을 입꼬리에 콕! 대보세요";
    }

    // ===== ★진단 =====
    void OnGUI()
    {
        if (scanner == null || !scanner.showDebug) return;

        GUIStyle st = new GUIStyle(GUI.skin.label);
        st.fontSize = 16;
        st.normal.textColor = Color.cyan;

        string cur = "없음";

        if (current != null) cur = (current.correctDir > 0 ? "포도(왼쪽)" : "푸딩(오른쪽)")
                                 + " / " + current.state;

        string info =
            "[젤리게임] " + (running ? "진행중" : "멈춤") +
            "   " + sortedCount + " / " + totalJelly + "\n" +
            "지금 젤리 " + cur + "\n" +
            "잠금 " + (armed ? "열림" : "잠김(가운데로)") +
            "   이어진 " + (holdDir == 1 ? "왼쪽" : holdDir == -1 ? "오른쪽" : "-") +
            " " + holdTime.ToString("F2") + "초\n" +
            "왼쪽 " + leftDone + "회 (최대 " + leftBest.ToString("F3") + ")\n" +
            "오른쪽 " + rightDone + "회 (최대 " + rightBest.ToString("F3") + ")\n" +
            "틀림 " + wrongCount + "회";

        GUI.Label(new Rect(500f, Screen.height - 170f, 460f, 160f), info, st);
    }
}