using UnityEngine;  // 유니티 기능(GameObject, Vector3 등)을 쓰려면 필요. Python의 import와 같음
using Mediapipe.Unity.Sample.FaceLandmarkDetection;  // MoonClimbFaceRunner를 쓰기 위해
using TMPro;  // TextMeshPro 기능을 쓰려면 필요

// 클래스 이름은 반드시 파일 이름(MoonClimbGame.cs)과 똑같아야 함
public class MoonClimbGame : MonoBehaviour
{
    // ===== Inspector에서 조절할 값들 =====
    public GameObject stepPrefab;       // 복제할 계단 원본(프리팹)
    public int totalSteps = 20;         // 계단 총 개수
    public float stepHeightGap = 0.8f;  // 계단 사이 세로 간격
    public float stepSideGap = 0.6f;    // 계단 사이 가로 간격 (지그재그 폭)
    public Transform player;            // 플레이어 오브젝트
    public TMP_Text progressText;       // 진행도를 표시할 텍스트
    public GameObject completePanel;    // 완료 시 띄울 패널
    public float playerOffsetY = 0.5f;   // 발판 위로 띄우는 높이

    [Header("토끼 그림")]
    public SpriteRenderer playerRenderer;   // 플레이어의 Sprite Renderer
    public Sprite rabbitIdle;               // 앉아 있는 토끼
    public Sprite rabbitJump;               // 뛰어오르는 토끼

    [Header("점프 연출")]
    public float jumpTime = 0.35f;          // 한 칸 뛰는 데 걸리는 시간(초)
    public float jumpHeight = 0.6f;         // 포물선이 얼마나 높이 솟는지

    [Header("얼굴 인식 설정")]
    public MoonClimbFaceRunner faceRunner;   // jawOpen 값을 읽어올 러너
    public float openThreshold = 0.35f;      // 이 값을 넘으면 "입 벌림"으로 판정
    public float closeThreshold = 0.15f;     // 이 값 아래로 내려가면 "입 다뭄"으로 판정
    public bool useFaceInput = true;         // 해제하면 스페이스바 모드 (테스트용)

    // ===== 내부 상태 =====
    private int currentStep = 0;        // 지금 몇 번째 칸에 있는지
    private bool isMouthOpen = false;   // 지금 입이 벌어진 상태인지 (디바운스용)
    private bool isJumping = false;         // 지금 뛰는 중인지 (중복 입력 막기)

       void Start()
    {
        CreateSteps();

        // ★토끼를 첫 계단 아래(출발 지점)에 세운다
        MovePlayerToStart();

        UpdateProgressText();
    }

    // ★===== 시작 위치로 토끼 보내기 =====
    void MovePlayerToStart()
    {
        if (player == null)
        {
            return;
        }

       // ★0번 계단(맨 아래) 위에 선다
        float posY = playerOffsetY;
        float posX = -stepSideGap;

        player.position = new Vector3(posX, posY, 0f);

        // 오른쪽 위로 뛸 거니까 오른쪽을 보게
        if (playerRenderer != null)
        {
            playerRenderer.flipX = false;
        }
    }

    // ===== 매 프레임마다 자동 실행 =====
    void Update()
    {
        // 스페이스바는 항상 작동 (테스트용)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ClimbOneStep();
        }

        // 얼굴 입력이 꺼져 있거나 러너가 연결 안 됐으면 여기서 끝
        if (!useFaceInput || faceRunner == null)
        {
            return;
        }

        float jawOpen = faceRunner.latestJawOpen;

        // --- 히스테리시스 디바운스 ---
        // 닫힌 상태에서 openThreshold를 넘으면 → 벌린 것으로 인정, 한 칸 오르기
        if (!isMouthOpen && jawOpen > openThreshold)
        {
            isMouthOpen = true;
            ClimbOneStep();
        }
        // 벌어진 상태에서 closeThreshold 아래로 내려가면 → 다문 것으로 인정
        // 칸은 안 올리고 상태만 되돌림 (다음 벌리기를 받을 준비)
        else if (isMouthOpen && jawOpen < closeThreshold)
        {
            isMouthOpen = false;
        }
    }

        // ===== 한 칸 올라가는 함수 =====
    void ClimbOneStep()
    {
        // 이미 꼭대기면 더 안 올라감
        if (currentStep >= totalSteps)
        {
            return;
        }

        // ★뛰는 중에 또 입력이 들어오면 무시
        if (isJumping)
        {
            return;
        }

        currentStep = currentStep + 1;

         // ★0번에서 출발하므로, 1칸 오르면 1번 계단으로 간다
        float posY = currentStep * stepHeightGap + playerOffsetY;

        float posX;
        if (currentStep % 2 == 0)
        {
            posX = -stepSideGap;
        }
        else
        {
            posX = stepSideGap;
        }

        Vector3 goal = new Vector3(posX, posY, 0f);

        // ★순간이동 대신 포물선으로 뛰어오르기
        StartCoroutine(JumpTo(goal));

        Debug.Log("현재 " + currentStep + "칸 / 총 " + totalSteps + "칸");
        UpdateProgressText();

        // 완료 확인
        if (currentStep >= totalSteps)
        {
            Debug.Log("달에 도착했습니다!");

            if (completePanel != null)
            {
                completePanel.SetActive(true);
            }
        }
    }

    // ★===== 포물선을 그리며 다음 칸으로 뛰기 =====
    // IEnumerator = 여러 프레임에 걸쳐 조금씩 실행되는 함수 (코루틴)
    System.Collections.IEnumerator JumpTo(Vector3 goal)
    {
        isJumping = true;

        // ★가는 방향을 바라보게 좌우 반전
        // goal.x가 지금 위치보다 오른쪽이면 오른쪽을 본다
        if (playerRenderer != null)
        {
            if (goal.x > player.position.x)
            {
                playerRenderer.flipX = false;   // 원래 방향 (오른쪽)
            }
            else
            {
                playerRenderer.flipX = true;    // 뒤집기 (왼쪽)
            }
        }

        // 뛰는 그림으로 바꾸기
        if (playerRenderer != null && rabbitJump != null)
        {
            playerRenderer.sprite = rabbitJump;
        }

        Vector3 start = player.position;
        float timer = 0f;

        // jumpTime 동안 조금씩 이동
        while (timer < jumpTime)
        {
            timer += Time.deltaTime;

            // t는 0에서 1까지 (0=출발, 1=도착)
            float t = timer / jumpTime;

            if (t > 1f)
            {
                t = 1f;
            }

            // 출발점에서 도착점까지 직선으로 이동
            Vector3 now = Vector3.Lerp(start, goal, t);

            // 거기에 포물선 높이를 더한다.
            // Sin(t * 180도)는 t=0.5일 때 가장 크고, 양 끝에서 0이 된다.
            float arc = Mathf.Sin(t * Mathf.PI) * jumpHeight;
            now.y = now.y + arc;

            player.position = now;

            yield return null;   // 다음 프레임까지 기다린다
        }

        // 정확한 위치로 마무리
        player.position = goal;

        // 앉은 그림으로 되돌리기
        if (playerRenderer != null && rabbitIdle != null)
        {
            playerRenderer.sprite = rabbitIdle;
        }

        isJumping = false;
    }

    // ===== 계단 20칸을 만드는 함수 =====
    void CreateSteps()
    {
        // ★출발 발판까지 포함해서 totalSteps + 1칸을 만든다
        for (int i = 0; i <= totalSteps; i++)
        {
            // 세로 위치: 위로 갈수록 높아짐
            float posY = i * stepHeightGap;

            // 가로 위치: 지그재그로 놓기 위해 짝수/홀수를 나눔
            float posX;
            if (i % 2 == 0)
            {
                posX = -stepSideGap;  // 짝수 번째는 왼쪽
            }
            else
            {
                posX = stepSideGap;   // 홀수 번째는 오른쪽
            }

            Vector3 spawnPosition = new Vector3(posX, posY, 0f);

            // Instantiate(원본, 위치, 회전) = 프리팹을 복제해서 씬에 생성
            // Quaternion.identity = "회전 없음"
            GameObject newStep = Instantiate(stepPrefab, spawnPosition, Quaternion.identity);

            newStep.name = "Step_" + (i + 1);
        }

        Debug.Log("계단 " + totalSteps + "칸 생성 완료");
    }

    // ===== 화면의 진행도 텍스트를 갱신 =====
    void UpdateProgressText()
    {
        if (progressText == null)
        {
            return;
        }

        progressText.text = currentStep + " / " + totalSteps;

        if (currentStep >= totalSteps)
        {
            progressText.text = "달 도착!";
        }
    }
}