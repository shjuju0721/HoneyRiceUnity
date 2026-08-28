using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

// 스테이지 하나의 정보를 담는 묶음
// [System.Serializable]을 붙이면 Inspector에서 값을 입력할 수 있다
[System.Serializable]
public class StageInfo
{
    public string title;            // 예: "5. 미소 사진관"
    [TextArea(3, 5)]                // Inspector에서 여러 줄로 입력 가능
    public string description;      // 게임 설명
    public Sprite previewImage;     // 미리보기 스크린샷
    public string sceneName;        // 이동할 씬 이름
}

// 카드를 누르면 미리보기 팝업을 띄우는 스크립트
public class StagePreview : MonoBehaviour
{
    [Header("팝업 연결")]
    public GameObject dimmer;           // 화면 어둡게 하는 막
    public GameObject previewPanel;     // 팝업 패널

    [Header("팝업 안 내용")]
    public TMP_Text titleText;
    public TMP_Text descText;
    public Image previewImage;

    [Header("스테이지 정보 (카드 순서대로)")]
    public StageInfo[] stages;

    // 지금 열려 있는 스테이지 번호 (0부터 시작)
    private int currentIndex = -1;

    void Start()
    {
        // 시작할 때는 팝업이 닫혀 있어야 한다
        ClosePreview();
    }

    // ===== 카드를 눌렀을 때 =====
    // 버튼 OnClick에 연결하고, 카드 번호(0부터)를 적어준다
    public void OpenPreview(int index)
    {
        // 잘못된 번호가 들어오면 아무것도 하지 않는다
        if (index < 0 || index >= stages.Length)
        {
            Debug.LogWarning("스테이지 번호가 범위를 벗어났습니다: " + index);
            return;
        }

        currentIndex = index;

        StageInfo info = stages[index];

        // 팝업 안 내용을 채운다
        if (titleText != null)
        {
            titleText.text = info.title;
        }

        if (descText != null)
        {
            descText.text = info.description;
        }

        if (previewImage != null)
        {
            previewImage.sprite = info.previewImage;
        }

        // 팝업을 보이게 한다
        if (dimmer != null)
        {
            dimmer.SetActive(true);
        }

        if (previewPanel != null)
        {
            previewPanel.SetActive(true);
        }
    }

    // ===== 닫기 버튼 =====
    public void ClosePreview()
    {
        currentIndex = -1;

        if (dimmer != null)
        {
            dimmer.SetActive(false);
        }

        if (previewPanel != null)
        {
            previewPanel.SetActive(false);
        }
    }

    // ===== 시작하기 버튼 =====
    public void StartStage()
    {
        // 열린 팝업이 없으면 아무것도 하지 않는다
        if (currentIndex < 0)
        {
            return;
        }

        string sceneName = stages[currentIndex].sceneName;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("씬 이름이 비어 있습니다. Inspector를 확인하세요.");
            return;
        }

        Debug.Log(sceneName + " 으로 이동");
        SceneManager.LoadScene(sceneName);
    }
}