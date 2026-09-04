using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  스테이지7 「젤리 분류」 — 시작 흐름 관리
//
//  흐름:
//   씬 시작 → 시작 패널
//     ├ [연습하고 시작] → 기준 재기 → 끝나면 게임 시작
//     └ [바로 시작]     → 저장된 기준으로 바로 게임
//                         (기준이 없으면 버튼이 잠겨 있음)
//
//  ★기준을 쟀는지는 코드가 이미 알고 있다(PlayerPrefs).
//    어르신이 스스로 판단하게 두지 말고 문구와 버튼으로 안내한다.
// ============================================================
public class JellyStageFlow : MonoBehaviour
{
    [Header("연결")]
    public TongueLRCalibration calibration;
    public JellySortGame game;

    [Header("시작 패널")]
    public GameObject startPanel;             // StartPanel
    public TMP_Text startText;                // StartText (패널 안 문구)
    public Button practiceButton;             // 연습하고 시작
    public Button quickStartButton;           // 바로 시작

    [Header("연습 안내 (게임 화면 위)")]
    public TMP_Text statusText;               // StatusText — 연습 문구가 여기 나온다
    public TMP_Text progressText;             // ●●○ 진행 표시 (없어도 됨)

    [Header("문구")]
    [TextArea]
    public string firstTimeMessage = "먼저 연습을 해볼까요?";
    [TextArea]
    public string readyMessage = "바로 시작 해볼까요?";

    [Header("시간")]
    public float afterPracticeSec = 1.5f;     // 연습 끝나고 게임까지 쉬는 시간

    // ===== 내부 =====
    private bool waitingToStart = false;      // 연습 끝나고 게임 기다리는 중
    private float startTimer = 0f;

    void Start()
    {
        // 캘리브레이션이 StatusText를 쓰도록 넘겨준다
        if (calibration != null)
        {
            calibration.guideText = statusText;
            calibration.progressText = progressText;

            calibration.onFinished = OnPracticeFinished;
        }

        ShowStartPanel();
    }

    void Update()
    {
        // 연습이 끝나고 잠깐 쉬었다가 게임 시작
        if (waitingToStart)
        {
            startTimer -= Time.deltaTime;

            if (startTimer <= 0f)
            {
                waitingToStart = false;
                BeginGame();
            }
        }
    }

    // ===== 시작 패널 보여주기 =====
    public void ShowStartPanel()
    {
        if (startPanel != null) startPanel.SetActive(true);

        bool hasBase = (calibration != null && calibration.ready);

        // ★문구는 상황에 맞게 바꾼다
        if (startText != null)
        {
            startText.text = hasBase ? readyMessage : firstTimeMessage;
        }

        // 기준이 없으면 "바로 시작"은 잠근다
        if (quickStartButton != null)
        {
            quickStartButton.interactable = hasBase;
        }

        if (statusText != null) statusText.text = "";
        if (progressText != null) progressText.text = "";
    }

    // ===== 버튼: 연습하고 시작 =====
    public void OnPracticeClicked()
    {
        if (startPanel != null) startPanel.SetActive(false);

        if (calibration != null) calibration.StartCalibration();
    }

    // ===== 버튼: 바로 시작 =====
    public void OnQuickStartClicked()
    {
        if (startPanel != null) startPanel.SetActive(false);

        BeginGame();
    }

    // ===== 연습이 끝났을 때 (캘리브레이션이 불러준다) =====
    void OnPracticeFinished()
    {
        waitingToStart = true;
        startTimer = afterPracticeSec;
    }

    // ===== 게임 시작 =====
    void BeginGame()
    {
        // 연습 안내는 지운다 (게임 안내가 대신 나온다)
        if (progressText != null) progressText.text = "";

        // 캘리브레이션이 StatusText를 계속 덮어쓰지 않게 연결을 끊는다
        if (calibration != null)
        {
            calibration.guideText = null;
            calibration.progressText = null;
        }

        if (statusText != null) statusText.text = "";

        if (game != null) game.StartGame();
    }
}