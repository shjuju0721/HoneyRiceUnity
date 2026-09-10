using UnityEngine;
using TMPro;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 얼굴을 못 찾을 때만 안내를 띄운다.
// ★그물망(랜드마크 점)을 끄는 대신 이걸 쓴다 —
//   그물망은 항상 떠 있어 거슬리지만, 이건 문제가 있을 때만 뜬다.
public class FaceLostNotice : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;
    public TMP_Text noticeText;          // 안내를 띄울 텍스트

    [Header("설정")]
    public float graceSec = 1.0f;        // ★이만큼 계속 안 보여야 안내를 띄운다
                                         //   깜빡임에 매번 반응하면 글자가 번쩍여 거슬린다
    [TextArea]
    public string message = "얼굴이 화면에 보이게\n앉아 주세요";

    private float lostT = 0f;

    void Update()
    {
        if (noticeText == null || faceRunner == null) return;

        bool visible = faceRunner.latestHasLip;

        if (visible)
        {
            lostT = 0f;

            // 우리가 띄운 안내일 때만 지운다 (다른 문구를 지우지 않게)
            if (noticeText.text == message) noticeText.text = "";
        }
        else
        {
            lostT += Time.deltaTime;

            if (lostT >= graceSec) noticeText.text = message;
        }
    }
}