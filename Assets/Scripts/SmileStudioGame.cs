using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

// 스테이지 5: 미소 사진관 (B단계 - 웹캠 캡처 추가)
public class SmileStudioGame : MonoBehaviour
{
    [Header("연결")]
    public MoonClimbFaceRunner faceRunner;
    public TMP_Text statusText;
    public Slider smileGauge;
    public GameObject completePanel;
    public RawImage previewImage;       // ★확인용. 마지막에 찍은 사진을 보여줌

    [Header("판정 기준 (실측값)")]
    public float detectThreshold = 0.46f;
    public float releaseThreshold = 0.35f;

    [Header("게임 설정")]
    public float holdTime = 3.5f;
    public float waitTime = 2.5f;
    public int targetPhotos = 5;
    public int photoSize = 320;         // ★사진 한 변의 픽셀 수

    // ===== 내부 상태 =====
    private int photoCount = 0;
    private float holdTimer = 0f;
    private float waitTimer = 0f;
    private bool isArmed = true;
    private bool isFinished = false;

    // ★찍은 사진들을 담아두는 목록
    private List<Texture2D> photos = new List<Texture2D>();

    void Update()
    {
        if (faceRunner == null || isFinished)
        {
            return;
        }

        float smile = faceRunner.latestSmile;

        // --- 촬영 직후 대기 중 ---
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;

            if (smile < releaseThreshold)
            {
                isArmed = true;
            }

            UpdateGauge(0f);
            UpdateStatusText();
            return;
        }

        // --- 재장전이 안 됐으면 ---
        if (!isArmed)
        {
            if (smile < releaseThreshold)
            {
                isArmed = true;
            }

            UpdateGauge(0f);
            UpdateStatusText();
            return;
        }

        // --- 미소 유지 시간 재기 ---
        if (smile >= detectThreshold)
        {
            holdTimer += Time.deltaTime;

            if (holdTimer >= holdTime)
            {
                TakePhoto();
            }
        }
        else
        {
            holdTimer = 0f;
        }

        UpdateGauge(holdTimer / holdTime);
        UpdateStatusText();
    }

    // ===== 찰칵 =====
    void TakePhoto()
    {
        photoCount = photoCount + 1;
        holdTimer = 0f;
        waitTimer = waitTime;
        isArmed = false;

        // ★웹캠 화면을 한 장 떠옴
        Texture2D shot = CaptureWebcam();

        if (shot != null)
        {
            photos.Add(shot);

            // 확인용으로 화면에 띄움
            if (previewImage != null)
            {
                previewImage.texture = shot;
            }
        }

        Debug.Log("찰칵! " + photoCount + " / " + targetPhotos);

        if (photoCount >= targetPhotos)
        {
            Finish();
        }
    }

    // ★===== 웹캠 화면을 정사각형으로 잘라 복사 =====
    Texture2D CaptureWebcam()
    {
        // MediaPipe가 쓰고 있는 웹캠 텍스처를 가져옴
        var source = ImageSourceProvider.ImageSource;

        if (source == null)
        {
            Debug.LogWarning("웹캠을 찾을 수 없습니다");
            return null;
        }

        Texture src = source.GetCurrentTexture();

        if (src == null)
        {
            Debug.LogWarning("웹캠 텍스처가 비어 있습니다");
            return null;
        }

        // --- 짧은 변 기준으로 정사각형 영역을 계산 ---
        // 가로가 더 길면 좌우를 잘라내고, 세로가 길면 위아래를 잘라냄
        float shortSide = Mathf.Min(src.width, src.height);
        float scaleX = shortSide / src.width;
        float scaleY = shortSide / src.height;

        // 가운데를 쓰도록 시작 위치를 잡음
        float offsetX = (1f - scaleX) / 2f;
        float offsetY = (1f - scaleY) / 2f;

        // ★거울 반전: 가로 배율을 음수로 주면 좌우가 뒤집힘
        //   화면에서 보던 모습 그대로 나오게 하려는 것
        Vector2 scale = new Vector2(-scaleX, scaleY);
        Vector2 offset = new Vector2(offsetX + scaleX, offsetY);

        // --- 임시 화면에 그려 넣기 ---
        RenderTexture rt = RenderTexture.GetTemporary(photoSize, photoSize, 0);
        Graphics.Blit(src, rt, scale, offset);

        // --- 그려진 것을 Texture2D로 읽어옴 ---
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(photoSize, photoSize, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, photoSize, photoSize), 0, 0);
        result.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    // ===== 완료 =====
    void Finish()
    {
        isFinished = true;

        UpdateGauge(0f);

        if (statusText != null)
        {
            statusText.text = "다섯 장 모두 찍었어요!";
        }

        if (completePanel != null)
        {
            completePanel.SetActive(true);
        }
    }

    // ★===== 씬을 떠날 때 사진 메모리 정리 =====
    void OnDestroy()
    {
        foreach (var photo in photos)
        {
            if (photo != null)
            {
                Destroy(photo);
            }
        }

        photos.Clear();
    }

    // ===== 게이지 갱신 =====
    void UpdateGauge(float ratio)
    {
        if (smileGauge == null)
        {
            return;
        }

        smileGauge.value = ratio;
    }

    // ===== 안내 문구 =====
    void UpdateStatusText()
    {
        if (statusText == null)
        {
            return;
        }

        string line = "사진 " + photoCount + " / " + targetPhotos + "\n";

        if (waitTimer > 0f)
        {
            line = line + "참 멋진 미소예요!";
        }
        else if (!isArmed)
        {
            line = line + "표정을 풀면 다음 사진을 준비할게요";
        }
        else if (holdTimer > 0f)
        {
            line = line + "좋아요~ 그대로요!";
        }
        else
        {
            line = line + "활짝 웃어 보세요";
        }

        statusText.text = line;
    }
}