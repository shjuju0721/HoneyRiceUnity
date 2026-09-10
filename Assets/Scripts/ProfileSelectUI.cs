using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// ============================================================
//  프로필 선택 화면 (넷플릭스 방식)
//
//  흐름:
//   [고르기]  등록이 없으면 가운데 [+] 하나
//             있으면 아이콘들이 나란히 → 누르면 그 사람으로 시작
//             [편집]을 누르면 아이콘마다 X가 뜬다
//   [만들기]  캐릭터 고르기 → 별명 적기 → 등록
//
//  ★우클릭·길게 누르기를 안 쓴 이유:
//    개인 태블릿에서 쓰는 앱이라 우클릭이 없고,
//    어르신께는 "편집 버튼을 누르면 X가 보인다"가 훨씬 알기 쉽다.
// ============================================================
public class ProfileSelectUI : MonoBehaviour
{
    // ===== 슬롯 한 칸 =====
    [System.Serializable]
    public class Slot
    {
        public GameObject root;        // 이 칸 전체 (켜고 끄기)
        public Button button;          // 누르면 그 사람으로 시작
        public Image icon;             // 캐릭터 그림
        public TMP_Text nameText;      // 별명
        public GameObject deleteBadge; // X 표시 (편집할 때만)
        public Button deleteButton;    // X 누르면 삭제
    }

    [Header("고르는 화면")]
    public GameObject selectPanel;
    public TMP_Text titleText;
    public Slot[] slots = new Slot[4];     // ★4칸 — 미리 만들어 두고 필요한 만큼만 켠다
    public GameObject addSlot;             // [+] 칸 (자리가 남을 때만 보임)
    public Button addSlotButton;
    public Button editButton;
    public TMP_Text editButtonText;

    [Header("만드는 화면")]
    public GameObject createPanel;
    public Button[] avatarButtons = new Button[8];   // 캐릭터 고르기 8개
    public Image[] avatarSelectMarks = new Image[8]; // 고른 표시 (테두리 등)
    public TMP_InputField nameInput;
    public TMP_Text messageText;           // "같은 이름이 이미 있어요" 등
    public Button createButton;
    public Button cancelButton;

    [Header("삭제 확인")]
    public GameObject confirmPanel;
    public TMP_Text confirmText;
    public Button confirmYes;
    public Button confirmNo;

    [Header("캐릭터 그림 (0~7 순서대로)")]
    public Sprite[] avatarSprites = new Sprite[8];

    [Header("커서를 올렸을 때")]
    public float hoverScale = 1.12f;       // 살짝 커지는 정도
    public float hoverSpeed = 12f;         // 커지는 빠르기

    [Header("다음 씬")]
    public string nextScene = "Stage00_Menu";

    [Header("문구")]
    [TextArea] public string askWho = "프로필을\n선택해주세요";
    [TextArea] public string askFirst = "처음이시죠?\n아래 + 를 눌러 등록해 주세요";

    // ===== 내부 =====
    private bool editMode = false;         // 편집(삭제) 중인가
    private int pickedAvatar = 0;          // 만들 때 고른 캐릭터
    private string toDelete = "";          // 지우려는 별명

    void Start()
    {
        // 버튼 연결 (인스펙터에서 안 해도 되게 코드로)
        if (addSlotButton != null) addSlotButton.onClick.AddListener(OpenCreate);
        if (editButton != null) editButton.onClick.AddListener(ToggleEdit);
        if (createButton != null) createButton.onClick.AddListener(DoCreate);
        if (cancelButton != null) cancelButton.onClick.AddListener(CloseCreate);
        if (confirmYes != null) confirmYes.onClick.AddListener(DoDelete);
        if (confirmNo != null) confirmNo.onClick.AddListener(CloseConfirm);

        for (int i = 0; i < slots.Length; i++)
        {
            int idx = i;   // ★그대로 i를 쓰면 모든 버튼이 마지막 값을 쓴다

            if (slots[i] != null)
            {
                if (slots[i].button != null)
                {
                    slots[i].button.onClick.AddListener(() => OnSlotClicked(idx));
                }

                if (slots[i].deleteButton != null)
                {
                    slots[i].deleteButton.onClick.AddListener(() => AskDelete(idx));
                }

                // 커서를 올리면 살짝 커지게
                AddHover(slots[i].root);
            }
        }

        for (int i = 0; i < avatarButtons.Length; i++)
        {
            int idx = i;

            if (avatarButtons[i] != null)
            {
                avatarButtons[i].onClick.AddListener(() => PickAvatar(idx));

                AddHover(avatarButtons[i].gameObject);   // ★추가

                // 캐릭터 그림 넣기
                Image img = avatarButtons[i].GetComponent<Image>();

                if (img != null && idx < avatarSprites.Length && avatarSprites[idx] != null)
                {
                    img.sprite = avatarSprites[idx];
                }
            }
        }

        AddHover(addSlot);

        if (createPanel != null) createPanel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);

        Refresh();
    }

    // ============================================================
    //  화면 다시 그리기
    // ============================================================
    void Refresh()
    {
        var list = ProfileStore.All();

        // --- 슬롯 채우기 ---
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].root == null) continue;

            bool use = i < list.Count;

            slots[i].root.SetActive(use);

            if (!use) continue;

            Profile p = list[i];

            if (slots[i].nameText != null) slots[i].nameText.text = p.name;

            if (slots[i].icon != null
                && p.avatar >= 0 && p.avatar < avatarSprites.Length
                && avatarSprites[p.avatar] != null)
            {
                slots[i].icon.sprite = avatarSprites[p.avatar];
            }

            // X 표시는 편집 중일 때만
            if (slots[i].deleteBadge != null)
            {
                slots[i].deleteBadge.SetActive(editMode);
            }
        }

        // --- [+] 칸은 자리가 남을 때만 ---
        if (addSlot != null)
        {
            addSlot.SetActive(!ProfileStore.IsFull() && !editMode);
        }

        // --- 편집 버튼은 등록이 있을 때만 ---
        if (editButton != null)
        {
            editButton.gameObject.SetActive(list.Count > 0);
        }

        if (editButtonText != null)
        {
            editButtonText.text = editMode ? "다 했어요" : "편집";
        }

        // --- 제목 ---
        if (titleText != null)
        {
            titleText.text = (list.Count == 0) ? askFirst : askWho;
        }
    }

    // ============================================================
    //  고르기
    // ============================================================
    void OnSlotClicked(int idx)
    {
        // 편집 중에는 고르지 않는다 (실수로 시작하는 것 방지)
        if (editMode) return;

        var list = ProfileStore.All();

        if (idx < 0 || idx >= list.Count) return;

        ProfileStore.Select(list[idx].name);

        // 다음 화면으로
        if (!string.IsNullOrEmpty(nextScene))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
        }
    }

    void ToggleEdit()
    {
        editMode = !editMode;
        Refresh();
    }

    // ============================================================
    //  지우기
    // ============================================================
    void AskDelete(int idx)
    {
        var list = ProfileStore.All();

        if (idx < 0 || idx >= list.Count) return;

        toDelete = list[idx].name;

        if (confirmText != null)
        {
            // ★기록도 사라진다는 것을 반드시 알린다
            confirmText.text = toDelete + " 님을 지울까요?\n\n지금까지의 기록도 함께 사라져요";
        }

        if (confirmPanel != null) confirmPanel.SetActive(true);
    }

    void DoDelete()
    {
        if (toDelete != "")
        {
            ProfileStore.Remove(toDelete);
            toDelete = "";
        }

        CloseConfirm();

        // 다 지웠으면 편집 모드도 끈다
        if (ProfileStore.Count() == 0) editMode = false;

        Refresh();
    }

    void CloseConfirm()
    {
        toDelete = "";

        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // ============================================================
    //  만들기
    // ============================================================
    void OpenCreate()
    {
        pickedAvatar = 0;

        if (nameInput != null) nameInput.text = "";
        if (messageText != null) messageText.text = "";

        UpdateAvatarMarks();

        if (createPanel != null) createPanel.SetActive(true);
    }

    void CloseCreate()
    {
        if (createPanel != null) createPanel.SetActive(false);
    }

    void PickAvatar(int idx)
    {
        pickedAvatar = idx;
        UpdateAvatarMarks();
    }

    void UpdateAvatarMarks()
    {
        // Mark 이미지가 있으면 그것도 쓴다 (없으면 건너뜀)
        for (int i = 0; i < avatarSelectMarks.Length; i++)
        {
            if (avatarSelectMarks[i] != null)
            {
                avatarSelectMarks[i].gameObject.SetActive(i == pickedAvatar);
            }
        }

        // ★고른 캐릭터만 크게. 투명하게 하지 않는다 — 흐리면 잘 안 보인다
        for (int i = 0; i < avatarButtons.Length; i++)
        {
            if (avatarButtons[i] == null) continue;

            HoverGrow h = avatarButtons[i].GetComponent<HoverGrow>();

            if (h != null)
            {
                h.SetPicked(i == pickedAvatar);
            }
        }
    }

    void DoCreate()
    {
        string name = (nameInput != null) ? nameInput.text : "";
        string why;

        bool ok = ProfileStore.Add(name, pickedAvatar, out why);

        if (!ok)
        {
            // ★왜 안 됐는지 알려 준다 (그냥 안 되면 답답하다)
            if (messageText != null) messageText.text = why;
            return;
        }

        CloseCreate();
        Refresh();
    }

    // ============================================================
    //  커서를 올리면 살짝 커지게
    // ============================================================
    void AddHover(GameObject go)
    {
        if (go == null) return;

        HoverGrow h = go.GetComponent<HoverGrow>();

        if (h == null) h = go.AddComponent<HoverGrow>();

        h.scaleTo = hoverScale;
        h.speed = hoverSpeed;
    }
}

// ============================================================
//  커서를 올리면 살짝 커졌다가, 떼면 되돌아온다
//  ★터치 기기에는 커서가 없으므로 아무 일도 일어나지 않는다(문제없음)
// ============================================================
public class HoverGrow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float scaleTo = 1.12f;
    public float speed = 12f;
    public float pickedScale = 1.15f;   // ★고른 것은 계속 커져 있다

    private Vector3 baseScale = Vector3.one;
    private bool over = false;
    private bool picked = false;
    private bool saved = false;

    void OnEnable()
    {
        if (!saved)
        {
            baseScale = transform.localScale;
            saved = true;
        }

        over = false;
    }

    public void SetPicked(bool on)
    {
        picked = on;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        over = true;
    }

    public void OnPointerExit(PointerEventData e)
    {
        over = false;
    }

    void Update()
    {
        float mul = 1f;

        if (picked) mul = pickedScale;
        if (over) mul = scaleTo;          // 커서를 올리면 그게 우선

        Vector3 target = baseScale * mul;

        float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * speed);

        transform.localScale = Vector3.Lerp(transform.localScale, target, k);
    }
}