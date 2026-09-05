using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ============================================================
//  스테이지8 「점프! 숙여! 러너」 — 게임 본체
//
//  흐름:
//   시작 → 꼬마가 달림(배경 흐름)
//        → 장애물이 앞에 와서 멈춤 (배경·꼬마도 같이 멈춤)
//        → 안내 문구: "혀를 위로 올려 폴짝 뛰어요"
//        → 혀 판정을 0.3초 유지하면 접수
//           · 맞으면 → 점프 / 슬라이딩 동작 → 통과 → 다시 달림
//           · 틀리면 → 갸우뚱 + "앗! 반대예요~" → 다시 기다림
//        → 10개 다 통과하면 골인 + 완료 패널
//
//  ★실패 상태가 없다. 틀려도 계속 기다려 준다.
//    어르신이 "내가 못했다"고 느끼지 않게 하는 것이 이 게임의 원칙.
// ============================================================
public class RunnerGame : MonoBehaviour
{
    [Header("연결")]
    public TongueUpDownScanner scanner;        // 혀 위아래 판정
    public ObstacleSpawner spawner;            // 장애물 내보내기
    public Animator playerAnimator;            // 꼬마 애니메이터
    public Transform playerTransform;          // 꼬마 (갸우뚱 연출용)
    public BackgroundScroller[] backgrounds;   // 배경 층들 (하늘·언덕·땅 전부)

    [Header("UI")]
    public TMP_Text statusText;                // 안내 문구
    public TMP_Text countText;                 // "3 / 10" 진행 표시
    public GameObject completePanel;           // 완료 패널

    [Header("애니메이션 이름 (Animator와 똑같이)")]
    public string animRun = "Toko_Run_Anim";
    public string animJump = "Toko_Jump_Anim";
    public string animSlide = "Toko_Slide_Anim";

    [Header("게임 설정")]
    public int totalObstacles = 10;            // 장애물 개수
    public float dirHoldSec = 0.3f;            // ★같은 방향을 이만큼 유지해야 접수
                                               //   웹의 UD_DIR_SEC와 같음
    public float actionSec = 0.9f;             // 점프·슬라이딩 동작에 걸리는 시간
    public float tiltSec = 0.9f;               // 갸우뚱 시간
    public float hintSec = 2.4f;               // "앗! 반대예요~" 안내 시간
    public float goalWaitSec = 2.0f;           // 골인 후 완료 패널까지

    [Header("점프 높이")]
    public float jumpHeight = 2.5f;            // ★발판 위로 올라가는 높이
                                               //   발판에 발이 닿게 눈으로 맞출 것

    // ===== 게임 상태 =====
    private enum Phase
    {
        Idle,       // 아직 시작 안 함
        Running,    // 달리는 중 (다음 장애물이 다가옴)
        Waiting,    // 장애물 앞에서 답을 기다림
        Acting,     // 점프·슬라이딩 동작 중
        Goal        // 골인
    }

    private Phase phase = Phase.Idle;

    private List<Obstacle.Kind> order = new List<Obstacle.Kind>();  // 장애물 순서표
    private int index = 0;                     // 지금 몇 번째 장애물인가
    private Obstacle current = null;           // 지금 멈춰 있는 장애물

    // 방향 유지 재기
    private int lastDir = 0;
    private float dirTimer = 0f;

    // 연출 시계
    private float actTimer = 0f;
    private float tiltTimer = 0f;
    private float hintTimer = 0f;
    private float goalTimer = 0f;

    // 꼬마 원래 자리 (점프·갸우뚱 뒤 되돌리기용)
    private Vector3 playerHome;
    private bool homeSaved = false;

    // 기록
    public int upCount = 0;      // 위로 성공한 횟수
    public int downCount = 0;    // 아래로 성공한 횟수
    public int wrongCount = 0;   // 틀린 횟수

    // ============================================================
    //  시작
    // ============================================================
    void Start()
    {
        if (playerTransform != null && !homeSaved)
        {
            playerHome = playerTransform.position;
            homeSaved = true;
        }

        SetBackgroundRunning(false);
        PlayAnim(animRun);

        if (completePanel != null) completePanel.SetActive(false);
        if (statusText != null) statusText.text = "";
        if (countText != null) countText.text = "";
    }

    // ★StagePreview의 시작 버튼이 이걸 부른다
    public void StartGame()
    {
        // --- 순서표 만들기 ---
        order = MakeOrder(totalObstacles);
        index = 0;
        current = null;

        upCount = 0;
        downCount = 0;
        wrongCount = 0;

        lastDir = 0;
        dirTimer = 0f;

        phase = Phase.Running;

        SetBackgroundRunning(true);
        PlayAnim(animRun);
        UpdateCountText();

        if (statusText != null) statusText.text = "";

        // 첫 장애물 내보내기
        SpawnNext();
    }

    // ============================================================
    //  장애물 순서 만들기
    //  ★점프 절반 + 숙이기 절반을 섞되, 같은 게 3번 연속 나오지 않게
    //    (웹 makeRunOrder와 같은 규칙)
    // ============================================================
    List<Obstacle.Kind> MakeOrder(int n)
    {
        List<Obstacle.Kind> pool = new List<Obstacle.Kind>();

        int half = n / 2;

        for (int i = 0; i < half; i++) pool.Add(Obstacle.Kind.Jump);
        for (int i = 0; i < n - half; i++) pool.Add(Obstacle.Kind.Duck);

        // 30번까지 섞어 보고, 3연속이 없으면 채택
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Shuffle(pool);

            if (!HasThreeInARow(pool))
            {
                break;
            }
        }

        return pool;
    }

    void Shuffle(List<Obstacle.Kind> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Obstacle.Kind tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    bool HasThreeInARow(List<Obstacle.Kind> list)
    {
        for (int i = 2; i < list.Count; i++)
        {
            if (list[i] == list[i - 1] && list[i] == list[i - 2])
            {
                return true;
            }
        }

        return false;
    }

    // ============================================================
    //  매 프레임
    // ============================================================
    void Update()
    {
        // --- 안내 문구 시계 ---
        if (hintTimer > 0f)
        {
            hintTimer -= Time.deltaTime;
        }

        // --- 갸우뚱 연출 ---
        if (tiltTimer > 0f)
        {
            tiltTimer -= Time.deltaTime;

            if (playerTransform != null)
            {
                // 좌우로 갸웃갸웃
                float ang = Mathf.Sin(tiltTimer * 18f) * 12f;
                playerTransform.rotation = Quaternion.Euler(0f, 0f, ang);
            }

            if (tiltTimer <= 0f && playerTransform != null)
            {
                playerTransform.rotation = Quaternion.identity;
            }
        }

        switch (phase)
        {
            case Phase.Running:
                UpdateRunning();
                break;

            case Phase.Waiting:
                UpdateWaiting();
                break;

            case Phase.Acting:
                UpdateActing();
                break;

            case Phase.Goal:
                UpdateGoal();
                break;
        }
    }

    // ===== 달리는 중 =====
    void UpdateRunning()
    {
        // 장애물이 도착하면 spawner가 알려 준다 (OnObstacleArrived)
        // 여기서는 특별히 할 일이 없다
    }

    // ===== 장애물 앞에서 답을 기다림 =====
    void UpdateWaiting()
    {
        if (current == null)
        {
            return;
        }

        // --- 판정할 수 없는 상태면 안내만 ---
        if (scanner == null || !scanner.CanMeasure())
        {
            lastDir = 0;
            dirTimer = 0f;

            ShowStatus(scanner != null && scanner.NoticeText() != ""
                       ? scanner.NoticeText()
                       : "얼굴이 화면에 보이게 앉아 주세요");
            return;
        }

        // --- 안내 문구 (틀렸을 때는 그 안내가 우선) ---
        if (hintTimer <= 0f)
        {
            ShowStatus(current.GuideText());
        }

        // --- 방향을 얼마나 유지했나 ---
        int dir = scanner.Direction();

        if (dir == 0)
        {
            lastDir = 0;
            dirTimer = 0f;
            return;
        }

        if (dir == lastDir)
        {
            dirTimer += Time.deltaTime;
        }
        else
        {
            lastDir = dir;
            dirTimer = 0f;
        }

        // --- 충분히 유지했으면 접수 ---
        if (dirTimer >= dirHoldSec)
        {
            lastDir = 0;
            dirTimer = 0f;

            Judge(dir);
        }
    }

    // ===== 판정 =====
    void Judge(int dir)
    {
        if (current == null) return;

        if (dir == current.CorrectDirection())
        {
            // ★정답 — 갸우뚱 중이어도 즉시 통과시킨다
            tiltTimer = 0f;

            if (playerTransform != null)
            {
                playerTransform.rotation = Quaternion.identity;
            }

            if (dir > 0)
            {
                upCount++;
                PlayAnim(animJump);
                ShowStatus("폴짝!");
            }
            else
            {
                downCount++;
                PlayAnim(animSlide);
                ShowStatus("슝~!");
            }

            current.Pass();
            current = null;

            phase = Phase.Acting;
            actTimer = actionSec;

            SetBackgroundRunning(true);
        }
        else
        {
            // ★오답 — 실패가 아니다. 갸우뚱하고 다시 기다린다
            wrongCount++;

            tiltTimer = tiltSec;
            hintTimer = hintSec;

            ShowStatus("앗! 반대예요~  " + current.GuideText());
        }
    }

    // ===== 점프·슬라이딩 동작 중 =====
    void UpdateActing()
    {
        actTimer -= Time.deltaTime;

        // 점프면 포물선으로 올라갔다 내려온다
        if (playerTransform != null && playerAnimator != null)
        {
            float t = 1f - Mathf.Clamp01(actTimer / actionSec);   // 0 → 1

            // 지금 점프 중인지 확인 (up으로 통과했을 때만 띄운다)
            bool isJumping = playerAnimator.GetCurrentAnimatorStateInfo(0)
                                           .IsName(animJump);

            if (isJumping)
            {
                // sin 곡선 = 올라갔다 내려오는 포물선
                float h = Mathf.Sin(t * Mathf.PI) * jumpHeight;
                playerTransform.position = playerHome + Vector3.up * h;
            }
            else
            {
                // ★슬라이딩 중에는 땅에 붙여 둔다
                //   (직전 점프 높이가 남아 공중에서 미끄러지는 것 방지)
                playerTransform.position = playerHome;
            }
        }

        if (actTimer <= 0f)
        {
            // 원래 자리·달리기로 되돌리기
            if (playerTransform != null)
            {
                playerTransform.position = playerHome;
            }

            PlayAnim(animRun);
            ShowStatus("");

            index++;
            UpdateCountText();

            if (index >= order.Count)
            {
                // 다 통과했다 → 골인
                phase = Phase.Goal;
                goalTimer = goalWaitSec;
                ShowStatus("골인이에요! 🎉");
            }
            else
            {
                phase = Phase.Running;
                SpawnNext();
            }
        }
    }

    // ===== 골인 =====
    void UpdateGoal()
    {
        goalTimer -= Time.deltaTime;

        if (goalTimer <= 0f)
        {
            phase = Phase.Idle;

            SetBackgroundRunning(false);

            if (completePanel != null)
            {
                completePanel.SetActive(true);
            }
        }
    }

    // ============================================================
    //  장애물 내보내기
    // ============================================================
    void SpawnNext()
    {
        if (spawner == null || index >= order.Count)
        {
            return;
        }

        Obstacle ob = spawner.Spawn(order[index]);

        if (ob != null)
        {
            // 도착하면 알려 달라고 부탁 (spawner 것을 덮어쓴다)
            ob.onArrived = OnObstacleArrived;
        }
    }

    // ===== 장애물이 꼬마 앞에 도착했을 때 =====
    void OnObstacleArrived(Obstacle ob)
    {
        current = ob;
        phase = Phase.Waiting;

        // ★배경도 같이 멈춘다 (장애물만 서 있으면 뒤로 밀리는 것처럼 보임)
        SetBackgroundRunning(false);

        lastDir = 0;
        dirTimer = 0f;

        ShowStatus(ob.GuideText());
    }

    // ============================================================
    //  도우미
    // ============================================================

    void SetBackgroundRunning(bool run)
    {
        if (backgrounds == null) return;

        for (int i = 0; i < backgrounds.Length; i++)
        {
            if (backgrounds[i] != null)
            {
                backgrounds[i].SetRunning(run);
            }
        }
    }

    void PlayAnim(string stateName)
    {
        if (playerAnimator == null || string.IsNullOrEmpty(stateName)) return;

        playerAnimator.Play(stateName, 0, 0f);
    }

    void ShowStatus(string msg)
    {
        if (statusText != null)
        {
            statusText.text = msg;
        }
    }

    void UpdateCountText()
    {
        if (countText != null)
        {
            countText.text = index + " / " + order.Count;
        }
    }
}