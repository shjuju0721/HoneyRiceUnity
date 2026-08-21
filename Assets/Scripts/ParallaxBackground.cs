using UnityEngine;

// 카메라보다 느리게 따라와서 "멀리 있는 배경" 느낌을 내는 스크립트
public class ParallaxBackground : MonoBehaviour
{
    public Transform cam;             // 기준이 되는 카메라
    public float parallaxFactor = 0.7f;   // 0 = 완전 고정, 1 = 카메라와 똑같이 움직임

    private float startCamX;          // 시작할 때 카메라 X
    private float startMyX;           // 시작할 때 내 X

    void Start()
    {
        if (cam == null)
        {
            return;
        }

        startCamX = cam.position.x;
        startMyX = transform.position.x;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            return;
        }

        // 카메라가 시작점에서 얼마나 움직였나
        float camMoved = cam.position.x - startCamX;

        // 그 일부만큼만 따라 움직임
        float myX = startMyX + (camMoved * parallaxFactor);

        transform.position = new Vector3(myX, transform.position.y, transform.position.z);
    }
}