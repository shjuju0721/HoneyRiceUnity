using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.FaceLandmarker;

// MediaPipe 결과에서 원하는 블렌드셰이프 점수를 꺼내주는 도구 모음
// static 클래스 = 인스턴스를 만들지 않고 바로 함수를 부를 수 있는 유틸리티
public static class FaceBlendshapeReader
{
    // ===== 이름으로 블렌드셰이프 점수 찾기 =====
    // 예: GetScore(result, "jawOpen")  →  입 벌린 정도 0~1
    // 찾지 못하면 0을 돌려줌 (얼굴이 안 잡혔을 때 등)
    public static float GetScore(FaceLandmarkerResult result, string blendshapeName)
    {
        // 1) 블렌드셰이프 자체가 없으면 0
        //    (옵션이 꺼져 있거나 얼굴이 화면에 없을 때 null이 됨)
        if (result.faceBlendshapes == null || result.faceBlendshapes.Count == 0)
        {
            return 0f;
        }

        // 2) 첫 번째 얼굴의 52개 목록을 가져옴
        //    Num Faces = 1로 설정했으므로 [0]만 보면 됨
        var categories = result.faceBlendshapes[0].categories;

        if (categories == null)
        {
            return 0f;
        }

        // 3) 52개를 하나씩 훑으면서 이름이 일치하는 것을 찾음
        //    foreach = Python의 for x in list 와 같음
        foreach (var category in categories)
        {
            if (category.categoryName == blendshapeName)
            {
                return category.score;  // 찾았으면 점수 반환하고 함수 종료
            }
        }

        // 4) 끝까지 못 찾았으면 0
        return 0f;
    }

    // ===== 좌우 쌍으로 된 블렌드셰이프의 평균 =====
    // 예: GetAverageScore(result, "mouthPressLeft", "mouthPressRight")
    // 웹 버전에서 (mouthPressLeft + mouthPressRight) / 2 로 쓰던 방식
    public static float GetAverageScore(FaceLandmarkerResult result, string nameA, string nameB)
    {
        float a = GetScore(result, nameA);
        float b = GetScore(result, nameB);
        return (a + b) / 2f;
    }

        // ===== 볼 너비 비율 계산 =====
    // 웹/데스크톱판에서 검증된 공식 그대로:
    //   (61-135 + 291-364 + 137-366 거리의 평균) ÷ 얼굴 세로(10-152)
    // 얼굴 세로로 나누는 이유 = 카메라에 가까이 가도 값이 안 변하게 (비율로 만듦)
    public static float GetCheekWidth(FaceLandmarkerResult result)
    {
        // 랜드마크가 없으면 0 (얼굴이 안 잡혔을 때)
        if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
        {
            return 0f;
        }

        var landmarks = result.faceLandmarks[0].landmarks;

        if (landmarks == null || landmarks.Count < 400)
        {
            return 0f;
        }

        // --- 가로 거리 세 쌍의 평균 ---
        float w1 = Distance(landmarks, 61, 135);
        float w2 = Distance(landmarks, 291, 364);
        float w3 = Distance(landmarks, 137, 366);
        float widthAvg = (w1 + w2 + w3) / 3f;

        // --- 얼굴 세로 (이마 10번 ~ 턱끝 152번) ---
        float faceHeight = Distance(landmarks, 10, 152);

        // 0으로 나누기 방지
        if (faceHeight < 0.0001f)
        {
            return 0f;
        }

        return widthAvg / faceHeight;
    }

    // ===== 두 랜드마크 사이의 거리 =====
    // 2D 화면상의 거리만 봄 (z는 정확도가 낮아서 뺌 — 웹 버전과 동일)
    private static float Distance(
        IReadOnlyList<Mediapipe.Tasks.Components.Containers.NormalizedLandmark> pts,
        int indexA, int indexB)
    {
        var a = pts[indexA];
        var b = pts[indexB];

        float dx = a.x - b.x;
        float dy = a.y - b.y;

        // Mathf.Sqrt = 제곱근. 피타고라스 정리로 직선 거리를 구함
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

        // ===== 입술 안쪽 둘레 20개 점 번호 =====
    // 데스크톱 → 웹 → 유니티, 세 번째 이식. 순서대로 이으면 "입 안" 다각형이 됨
    // ★순서를 바꾸면 다각형이 꼬입니다
    public static readonly int[] INNER_LIP = {
        78, 95, 88, 178, 87, 14, 317, 402, 318, 324,
        308, 415, 310, 311, 312, 13, 82, 81, 80, 191
    };

    // ===== 입술 안쪽 20개 점의 좌표를 뽑아 담기 =====
    // ★콜백 스레드 안에서 불러야 함. 좌표만 복사해서 넘김(목록 자체를 밖으로 넘기면 크래시)
    // dest: 미리 만들어둔 20칸짜리 배열. 여기에 값을 채워 넣음
    // 돌려주는 값: 성공했으면 true
    public static bool FillInnerLip(FaceLandmarkerResult result, Vector2[] dest)
    {
        if (dest == null || dest.Length < INNER_LIP.Length)
        {
            return false;
        }

        if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
        {
            return false;
        }

        var landmarks = result.faceLandmarks[0].landmarks;

        if (landmarks == null || landmarks.Count < 478)
        {
            return false;
        }

        for (int i = 0; i < INNER_LIP.Length; i++)
        {
            var p = landmarks[INNER_LIP[i]];
            dest[i] = new Vector2(p.x, p.y);   // 0~1 정규화 좌표
        }

        return true;
    }
}