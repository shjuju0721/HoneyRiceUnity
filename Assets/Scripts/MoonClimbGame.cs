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

    [Header("얼굴 인식 설정")]
    public MoonClimbFaceRunner faceRunner;   // jawOpen 값을 읽어올 러너
    public float openThreshold = 0.35f;      // 이 값을 넘으면 "입 벌림"으로 판정
    public float closeThreshold = 0.15f;     // 이 값 아래로 내려가면 "입 다뭄"으로 판정
    public bool useFaceInput = true;         // 해제하면 스페이스바 모드 (테스트용)

    // ===== 내부 상태 =====
    private int currentStep = 0;        // 지금 몇 번째 칸에 있는지
    private bool isMouthOpen = false;   // 지금 입이 벌어진 상태인지 (디바운스용)

    // ===== Play 누르면 딱 1번 실행 =====
    void Start()
    {
        CreateSteps();
        UpdateProgressText();  // "0 / 20"으로 초기화
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
        // --- 방어: 이미 꼭대기면 더 안 올라감 ---
        // 아직 칸을 안 올린 상태에서 검사하므로, 도착 후 추가 입력을 막는 역할
        if (currentStep >= totalSteps)
        {
            return;
        }

        currentStep = currentStep + 1;  // 칸 수 1 증가

        // --- 플레이어를 다음 칸 위치로 이동 ---
        // 계단을 만들 때 쓴 계산식과 똑같이 맞춰야 발판 위에 정확히 올라감
        float posY = (currentStep - 1) * stepHeightGap + 0.5f;  // +0.5f = 발판 위로 살짝 띄우기

        float posX;
        if ((currentStep - 1) % 2 == 0)   // 계단 만들 때와 동일한 짝수/홀수 판정
        {
            posX = -stepSideGap;
        }
        else
        {
            posX = stepSideGap;
        }

        player.position = new Vector3(posX, posY, 0f);

        Debug.Log("현재 " + currentStep + "칸 / 총 " + totalSteps + "칸");
        UpdateProgressText();

        // --- 완료 확인: 칸을 올린 뒤에 검사해야 "방금 도착"을 잡을 수 있음 ---
        if (currentStep >= totalSteps)
        {
            Debug.Log("달에 도착했습니다!");

            if (completePanel != null)
            {
                completePanel.SetActive(true);
            }
        }
    }

    // ===== 계단 20칸을 만드는 함수 =====
    void CreateSteps()
    {
        // i가 0부터 19까지 총 20번 반복 (Python의 for i in range(20) 과 같음)
        for (int i = 0; i < totalSteps; i++)
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