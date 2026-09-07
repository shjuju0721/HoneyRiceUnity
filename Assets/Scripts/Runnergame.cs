using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ============================================================
//  스테이지8 「점프! 숙여! 러너」 — 게임 본체
//
//  흐름:
//   시작 → 꼬마가 달림(배경 흐름)
//        → 장애물이 앞에 와서 멈춤 (배경·꼬마도 같이 멈춤)
//        → 안내 문구: "혀를 윗입술까지 올려 보세요"
//        → 혀 판정을 0.3초 유지하면 접수
//           · 맞으면 → 점프 / 슬라이딩 동작 → 통과 → 다시 달림
//           · 틀리면 → 갸우뚱 + "앗! 반대예요~" → 다시 기다림
//        → 10개 다 통과하면 ★집이 나타나고 토코가 폴짝폴짝 → 완료 패널
//
//  ★실패 상태가 없다. 틀려도 계속 기다려 준다.
//    어르신이 "내가 못했다"고 느끼지 않게 하는 것이 이 게임의 원칙.
//
//  ★골인은 "결승선 통과"가 아니라 "집에 도착"이다.
//    경쟁이 아니라 여정의 끝. 이 게임의 성격에 맞춘 선택.
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
    public float tiltSec = 0.9f;               // 갸우뚱 시간
    public float hintSec = 2.4f;               // "앗! 반대예요~" 안내 시간
    public bool autoStart = true;              // ★씬이 열리면 바로 시작
                                               //   (프리뷰에서 "시작하기"를 눌러 들어오므로)

    // ============================================================
    //  ★골인 연출 — 집에 도착하기
    // ============================================================
    [Header("★골인 연출 (집 도착)")]
    public GameObject housePrefab;             // ★Goal_House 프리팹
    public float houseSpawnX = 14f;            // 집이 나타나는 자리 (화면 오른쪽 밖)
    public float houseStopX = 1.5f;            // ★집이 멈추는 자리 (토코 앞)
    public float houseY = 0f;                  // 집의 높이
    public float houseScale = 1f;              // 집 크기

    public int cheerCount = 3;                 // ★토코가 폴짝 뛰는 횟수
    public float cheerUpSec = 0.28f;           // 한 번 뛰는 데 걸리는 시간
    public float cheerHeight = 1.2f;           // 뛰는 높이
    public float cheerRestSec = 0.12f;         // 뛰고 나서 쉬는 시간
    public float afterCheerSec = 1.2f;         // 다 뛰고 완료 패널까지

    [TextArea]
    public string goalMessage = "집에 다 왔어요!";

    // ============================================================
    //  ★점프 3구간 (웹 버전과 같은 방식)
    //
    //  ① 뛰어오르기 : 땅 → 발판 높이까지 올라감
    //  ② 발판 위 달리기 : 높이를 유지한 채 달리기 애니메이션
    //     → 이 동안 발판이 밑으로 흘러가서 "건너는" 것처럼 보인다
    //  ③ 뛰어내리기 : 발판 → 땅으로 내려옴
    // ============================================================
    [Header("★점프 3구간")]
    public float platformTopY = 4.64f;         // ★발판 윗면 높이. 꼬마 발이 여기에 닿는다
    public float riseSec = 0.38f;              // ① 뛰어오르는 시간
    public float onTopSec = 0.30f;             // ② 발판 위를 달리는 시간
    public float fallSec = 0.34f;              // ③ 뛰어내리는 시간

    [Header("슬라이딩")]
    public float slideSec = 0.9f;              // 엎드려 미끄러지는 시간

    // ===== 게임 상태 =====
    private enum Phase
    {
        Idle,        // 아직 시작 안 함
        Running,     // 달리는 중 (다음 장애물이 다가옴)
        Waiting,     // 장애물 앞에서 답을 기다림
        Jumping,     // ★점프 3구간 진행 중
        Sliding,     // 슬라이딩 중
        HouseComing, // ★집이 다가오는 중
        Cheering,    // ★토코가 폴짝폴짝
        Finishing    // ★기뻐하고 나서 완료 패널까지 기다림
    }

    private Phase phase = Phase.Idle;

    // 점프 3구간 중 어디인가
    private enum JumpStep { Rise, OnTop, Fall }
    private JumpStep jumpStep = JumpStep.Rise;
    private float jumpTimer = 0f;

    private List<Obstacle.Kind> order = new List<Obstacle.Kind>();  // 장애물 순서표
    private int index = 0;                     // 지금 몇 번째 장애물인가
    private Obstacle current = null;           // 지금 멈춰 있는 장애물

    // 방향 유지 재기
    private int lastDir = 0;
    private float dirTimer = 0f;

    // 연출 시계
    private float slideTimer = 0f;
    private float tiltTimer = 0f;
    private float hintTimer = 0f;
    private float finishTimer = 0f;

    // 골인 연출
    private GameObject houseObj = null;        // 지금 나와 있는 집
    private int cheerLeft = 0;                 // 몇 번 더 뛸까
    private float cheerTimer = 0f;
    private bool cheerResting = false;         // 뛰는 중인가 쉬는 중인가

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

        // ★씬이 열리면 바로 게임 시작
        if (autoStart)
        {
            StartGame();
        }
    }

    // ★프리뷰의 시작 버튼이나 다시하기 버튼이 이걸 부른다
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
        hintTimer = 0f;
        tiltTimer = 0f;

        // 남아 있는 집이 있으면 치운다 (다시하기 대비)
        if (houseObj != null)
        {
            Destroy(houseObj);
            houseObj = null;
        }

        // 꼬마를 땅으로 되돌린다
        if (playerTransform != null && homeSaved)
        {
            playerTransform.position = playerHome;
            playerTransform.rotation = Quaternion.identity;
        }

        phase = Phase.Running;

        SetBackgroundRunning(true);
        PlayAnim(animRun);
        UpdateCountText();

        if (statusText != null) statusText.text = "";
        if (completePanel != null) completePanel.SetActive(false);

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
            case Phase.Waiting:
                UpdateWaiting();
                break;

            case Phase.Jumping:
                UpdateJumping();
                break;

            case Phase.Sliding:
                UpdateSliding();
                break;

            case Phase.HouseComing:
                UpdateHouseComing();
                break;

            case Phase.Cheering:
                UpdateCheering();
                break;

            case Phase.Finishing:
                UpdateFinishing();
                break;
        }
    }

    // ===== 장애물 앞에서 답을 기다림 =====
    void UpdateWaiting()
    {
        if (current == null)
        {
            return;
        }

        // ============================================================
        //  ★얼굴을 아예 못 찾을 때만 따로 안내한다.
        //    입을 벌렸는지는 안내하지 않는다 —
        //    혀 동작을 하면 입은 저절로 벌어지므로,
        //    "입을 벌리세요"는 오히려 무슨 동작을 하라는 건지 헷갈리게 만든다.
        // ============================================================
        if (scanner == null || !scanner.hasFace)
        {
            lastDir = 0;
            dirTimer = 0f;

            ShowStatus("얼굴이 화면에 보이게\n앉아 주세요");
            return;
        }

        // --- 카메라가 너무 멀 때만 그 안내를 우선 ---
        if (scanner.NoticeText() != "")
        {
            lastDir = 0;
            dirTimer = 0f;

            ShowStatus(scanner.NoticeText());
            return;
        }

        // --- 안내 문구 (틀렸을 때는 그 안내가 잠깐 우선) ---
        if (hintTimer <= 0f)
        {
            ShowStatus(current.GuideText());
        }

        // --- 아직 입을 안 벌렸으면 판정만 쉰다 (문구는 그대로) ---
        if (!scanner.CanMeasure())
        {
            lastDir = 0;
            dirTimer = 0f;
            return;
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
            hintTimer = 0f;

            if (playerTransform != null)
            {
                playerTransform.rotation = Quaternion.identity;
            }

            string passMsg = current.PassText();

            current.Pass();
            current = null;

            SetBackgroundRunning(true);

            if (dir > 0)
            {
                // ★점프 시작 — 3구간 중 첫 번째부터
                upCount++;

                phase = Phase.Jumping;
                jumpStep = JumpStep.Rise;
                jumpTimer = riseSec;

                PlayAnim(animJump);
            }
            else
            {
                // 슬라이딩
                downCount++;

                phase = Phase.Sliding;
                slideTimer = slideSec;

                PlayAnim(animSlide);
            }

            ShowStatus(passMsg);
        }
        else
        {
            // ★오답 — 실패가 아니다. 갸우뚱하고 다시 기다린다
            wrongCount++;

            tiltTimer = tiltSec;
            hintTimer = hintSec;

            ShowStatus("앗! 반대예요~\n" + current.GuideText());
        }
    }

    // ============================================================
    //  ★점프 3구간
    // ============================================================
    void UpdateJumping()
    {
        jumpTimer -= Time.deltaTime;

        // 땅에서 발판 윗면까지의 높이 차이
        float climb = platformTopY - playerHome.y;

        switch (jumpStep)
        {
            // --- ① 뛰어오르기 : 땅 → 발판 ---
            case JumpStep.Rise:
                {
                    float t = 1f - Mathf.Clamp01(jumpTimer / riseSec);   // 0 → 1

                    // 처음엔 빠르고 끝에서 느려지게 (자연스러운 도약)
                    float ease = Mathf.Sin(t * Mathf.PI * 0.5f);

                    SetPlayerHeight(climb * ease);

                    if (jumpTimer <= 0f)
                    {
                        // ★발판 위에 올라섰다 → 달리기로 바꾼다
                        jumpStep = JumpStep.OnTop;
                        jumpTimer = onTopSec;

                        SetPlayerHeight(climb);
                        PlayAnim(animRun);
                    }
                }
                break;

            // --- ② 발판 위 달리기 : 높이 유지 ---
            case JumpStep.OnTop:
                {
                    SetPlayerHeight(climb);   // ★그대로 유지

                    if (jumpTimer <= 0f)
                    {
                        jumpStep = JumpStep.Fall;
                        jumpTimer = fallSec;

                        PlayAnim(animJump);
                    }
                }
                break;

            // --- ③ 뛰어내리기 : 발판 → 땅 ---
            case JumpStep.Fall:
                {
                    float t = 1f - Mathf.Clamp01(jumpTimer / fallSec);   // 0 → 1

                    // 처음엔 느리고 끝에서 빠르게 (중력에 끌리듯)
                    float ease = 1f - Mathf.Cos(t * Mathf.PI * 0.5f);

                    SetPlayerHeight(climb * (1f - ease));

                    if (jumpTimer <= 0f)
                    {
                        SetPlayerHeight(0f);
                        FinishAction();
                    }
                }
                break;
        }
    }

    // ===== 슬라이딩 중 =====
    void UpdateSliding()
    {
        slideTimer -= Time.deltaTime;

        // ★땅에 붙여 둔다 (직전 점프 높이가 남지 않게)
        SetPlayerHeight(0f);

        if (slideTimer <= 0f)
        {
            FinishAction();
        }
    }

    // ===== 동작이 끝났을 때 (점프·슬라이딩 공통) =====
    void FinishAction()
    {
        SetPlayerHeight(0f);
        PlayAnim(animRun);
        ShowStatus("");

        index++;
        UpdateCountText();

        if (index >= order.Count)
        {
            // ★다 통과했다 → 집이 다가온다
            StartHouseComing();
        }
        else
        {
            phase = Phase.Running;
            SpawnNext();
        }
    }

    // ============================================================
    //  ★골인 연출 ① — 집이 다가옴
    // ============================================================
    void StartHouseComing()
    {
        phase = Phase.HouseComing;

        // 배경은 계속 흐른다 (아직 달리는 중)
        SetBackgroundRunning(true);
        PlayAnim(animRun);

        if (housePrefab == null)
        {
            // 집 프리팹이 없으면 그냥 기뻐하기로 넘어간다
            Debug.LogWarning("[RunnerGame] House Prefab이 없어 집 연출을 건너뜁니다.");
            StartCheering();
            return;
        }

        houseObj = Instantiate(housePrefab,
                               new Vector3(houseSpawnX, houseY, 0f),
                               Quaternion.identity, transform);

        houseObj.transform.localScale = Vector3.one * houseScale;
    }

    void UpdateHouseComing()
    {
        if (houseObj == null)
        {
            StartCheering();
            return;
        }

        // 배경(땅)과 같은 속도로 흘러온다
        float speed = (spawner != null) ? spawner.speed : 3f;

        houseObj.transform.position += Vector3.left * (speed * Time.deltaTime);

        // 멈춤 자리에 도착하면
        if (houseObj.transform.position.x <= houseStopX)
        {
            Vector3 p = houseObj.transform.position;
            p.x = houseStopX;
            houseObj.transform.position = p;

            StartCheering();
        }
    }

    // ============================================================
    //  ★골인 연출 ② — 토코가 폴짝폴짝
    // ============================================================
    void StartCheering()
    {
        phase = Phase.Cheering;

        // ★배경을 멈춘다 (집 앞에 도착했으니)
        SetBackgroundRunning(false);

        cheerLeft = cheerCount;
        cheerTimer = cheerUpSec;
        cheerResting = false;

        PlayAnim(animJump);
        ShowStatus(goalMessage);
    }

    void UpdateCheering()
    {
        cheerTimer -= Time.deltaTime;

        if (!cheerResting)
        {
            // --- 뛰는 중 : 올라갔다 내려오는 포물선 ---
            float t = 1f - Mathf.Clamp01(cheerTimer / cheerUpSec);   // 0 → 1
            float h = Mathf.Sin(t * Mathf.PI) * cheerHeight;

            SetPlayerHeight(h);

            if (cheerTimer <= 0f)
            {
                SetPlayerHeight(0f);

                cheerLeft--;

                if (cheerLeft <= 0)
                {
                    // 다 뛰었다
                    PlayAnim(animRun);

                    phase = Phase.Finishing;
                    finishTimer = afterCheerSec;
                    return;
                }

                // 잠깐 쉬었다가 또 뛴다
                cheerResting = true;
                cheerTimer = cheerRestSec;
            }
        }
        else
        {
            // --- 쉬는 중 ---
            SetPlayerHeight(0f);

            if (cheerTimer <= 0f)
            {
                cheerResting = false;
                cheerTimer = cheerUpSec;

                PlayAnim(animJump);
            }
        }
    }

    // ============================================================
    //  ★골인 연출 ③ — 완료 패널
    // ============================================================
    void UpdateFinishing()
    {
        finishTimer -= Time.deltaTime;

        if (finishTimer <= 0f)
        {
            phase = Phase.Idle;

            if (completePanel != null)
            {
                completePanel.SetActive(true);
            }
        }
    }

    // ===== 꼬마를 원래 자리에서 h만큼 위로 =====
    void SetPlayerHeight(float h)
    {
        if (playerTransform == null || !homeSaved) return;

        playerTransform.position = playerHome + Vector3.up * h;
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