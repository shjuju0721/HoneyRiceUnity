using UnityEngine;

// ============================================================
//  민들레가 바람에 살랑살랑 흔들리게 하는 스크립트
//
//  ★그림은 한 장이면 된다. 기울기만 부드럽게 바꾼다.
//  ⚠Pivot이 Bottom(줄기 밑동)이어야 자연스럽다.
//    가운데가 축이면 뿌리째 좌우로 흔들려 어색하다.
// ============================================================
public class SwayInWind : MonoBehaviour
{
    [Header("흔들림")]
    public float angle = 4f;      // 좌우로 몇 도까지 기울일지
    public float speed = 1.2f;    // 얼마나 빠르게 흔들릴지
    public float offset = 0f;     // 여러 송이가 있을 때 서로 다르게 흔들리도록

    [Header("바람 세기 (기침할 때 잠깐 키우기용)")]
    public float gust = 0f;       // ★게임 스크립트가 여기에 값을 넣으면 확 휘청인다
    public float gustFade = 3f;   // 그 휘청임이 사그라드는 속도

    void Update()
    {
        // 기침으로 생긴 돌풍은 서서히 사그라든다
        if (gust > 0f)
        {
            gust = Mathf.Max(0f, gust - gustFade * Time.deltaTime);
        }

        // sin 곡선 = 좌우로 부드럽게 왕복
        float a = Mathf.Sin((Time.time + offset) * speed) * (angle + gust);

        transform.rotation = Quaternion.Euler(0f, 0f, a);
    }

    // ★기침했을 때 부르면 잠깐 크게 휘청인다
    public void Gust(float amount)
    {
        gust = amount;
    }
}