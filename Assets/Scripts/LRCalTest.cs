using UnityEngine;

// 스테이지10 시작 흐름 — 안내 패널 → 시작하기 → 게임
public class FeedingTest : MonoBehaviour
{
    public FeedingGame game;
    public GameObject introPanel;   // 안내 패널

    void Start()
    {
        // 시작하면 안내 패널부터 보여준다
        if (introPanel != null) introPanel.SetActive(true);
    }

    // 버튼이 부를 함수
    public void OnStartClicked()
    {
        if (introPanel != null) introPanel.SetActive(false);

        if (game != null) game.StartGame();
    }
}