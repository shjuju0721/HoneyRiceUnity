using UnityEngine;

// 대상을 가로(X)로만 따라가는 카메라
public class CameraFollowX : MonoBehaviour
{
    public Transform target;          // 따라갈 대상 (펭귄)
    public float offsetX = -2f;       // 펭귄을 화면 어디쯤에 둘지 (음수 = 왼쪽에 치우침)
    public float smoothSpeed = 3f;    // 따라가는 부드러움. 클수록 빨리 따라붙음
    public float minX = 0f;           // 카메라가 이보다 왼쪽으로는 안 감          
    public float maxX = 17f;          // ★카메라가 이보다 오른쪽으로도 안 감

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // 목표 X = 펭귄 위치에서 offset만큼 밀어놓은 곳
        float wantX = target.position.x - offsetX;

        // 시작 지점보다 왼쪽으로는 안 가게 막기
        if (wantX < minX)
        {
            wantX = minX;
        }

        // ★코스 끝을 넘어가지 않게 막기
        if (wantX > maxX)
        {
            wantX = maxX;
        }

        // 지금 위치에서 목표까지 조금씩 다가감 (뚝뚝 끊기지 않게)
        float newX = Mathf.Lerp(transform.position.x, wantX, smoothSpeed * Time.deltaTime);

        transform.position = new Vector3(newX, transform.position.y, transform.position.z);
    }
}