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
}