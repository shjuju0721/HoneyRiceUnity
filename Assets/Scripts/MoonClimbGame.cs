using UnityEngine;  // 유니티 기능(GameObject, Vector3 등)을 쓰려면 필요. Python의 import와 같음

// 클래스 이름은 반드시 파일 이름(MoonClimbGame.cs)과 똑같아야 함
// MonoBehaviour를 상속하면 유니티가 이 스크립트를 관리해줌 (Start, Update를 자동 호출)
public class MoonClimbGame : MonoBehaviour
{
    // ===== Inspector에서 조절할 값들 =====
    // public을 붙이면 유니티 Inspector 창에 칸이 생겨서 코드 수정 없이 값을 바꿀 수 있음

    public GameObject stepPrefab;   // 복제할 계단 원본(프리팹). 나중에 드래그해서 연결할 것
    public int totalSteps = 20;     // 계단 총 개수
    public float stepHeightGap = 0.8f;  // 계단 사이 세로 간격 (소수라서 f 필수)
    public float stepSideGap = 0.6f;    // 계단 사이 가로 간격 (지그재그 폭)

    // ===== Play 누르면 딱 1번 실행되는 함수 =====
    void Start()
    {
        CreateSteps();  // 아래에 직접 만든 함수를 호출. 계단 만들기 시작
    }

    // ===== 계단 20칸을 만드는 함수 =====
    // void = 돌려주는 값 없음 / 함수 이름은 대문자로 시작하는 게 C# 관습
    void CreateSteps()
    {
        // i가 0부터 19까지 총 20번 반복 (Python의 for i in range(20) 과 같음)
        for (int i = 0; i < totalSteps; i++)
        {
            // --- 이번 계단이 놓일 위치 계산 ---

            // 세로 위치: 위로 갈수록 높아짐. 0번째는 0, 1번째는 0.8, 2번째는 1.6...
            float posY = i * stepHeightGap;

            // 가로 위치: 지그재그로 놓기 위해 짝수/홀수를 나눔
            // i % 2 는 i를 2로 나눈 나머지 (Python과 동일)
            // 나머지가 0이면 짝수 → 왼쪽, 1이면 홀수 → 오른쪽
            float posX;
            if (i % 2 == 0)
            {
                posX = -stepSideGap;  // 짝수 번째는 왼쪽으로
            }
            else
            {
                posX = stepSideGap;   // 홀수 번째는 오른쪽으로
            }

            // Vector3 = 3D 좌표를 담는 상자 (x, y, z)
            // 2D 게임이라 z는 0으로 둠
            Vector3 spawnPosition = new Vector3(posX, posY, 0f);

            // --- 실제로 계단 하나를 복제해서 씬에 배치 ---
            // Instantiate(원본, 위치, 회전) = 프리팹을 복제해서 씬에 생성하는 유니티 함수
            // Quaternion.identity = "회전 없음"이라는 뜻 (0도)
            GameObject newStep = Instantiate(stepPrefab, spawnPosition, Quaternion.identity);

            // 복제된 계단에 이름 붙이기. Hierarchy에서 알아보기 쉽게
            // i + 1 을 하는 이유: 사람은 1번부터 세는 게 자연스러워서
            newStep.name = "Step_" + (i + 1);
        }

        // Debug.Log = Python의 print(). 유니티 Console 창에 출력됨
        Debug.Log("계단 " + totalSteps + "칸 생성 완료");
    }
}