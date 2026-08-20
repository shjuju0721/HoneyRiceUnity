using UnityEngine;

// 하트가 채워질 때 위로 둥실 떠올랐다가 사라지는 연출
public class PopHeart : MonoBehaviour
{
    public float riseSpeed = 1.5f;    // 위로 올라가는 속도
    public float lifeTime = 1.2f;     // 몇 초 뒤에 사라질지
    public float sideDrift = 0.5f;    // 좌우로 흔들리는 폭
    public Sprite[] heartSprites;   // ★쓸 하트 그림들. 이 중에서 아무거나 하나 골라 씀

    private float timer = 0f;         // 태어난 뒤 흐른 시간
    private float driftSeed;          // 하트마다 다르게 흔들리도록
    private SpriteRenderer sr;        // 투명하게 만들 때 씀

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();

        // ★그림 후보가 있으면 그중 아무거나 하나 골라 쓰기
        if (heartSprites != null && heartSprites.Length > 0)
        {
            int index = Random.Range(0, heartSprites.Length);   // 0 ~ (개수-1) 중 하나
            sr.sprite = heartSprites[index];
        }

        // 0~100 사이 아무 숫자. 하트마다 흔들리는 타이밍이 달라짐
        driftSeed = Random.Range(0f, 100f);
    }

    void Update()
    {
        timer += Time.deltaTime;

        // --- 위로 이동 + 좌우로 살랑 ---
        float x = Mathf.Sin((timer + driftSeed) * 3f) * sideDrift * Time.deltaTime;
        float y = riseSpeed * Time.deltaTime;
        transform.position += new Vector3(x, y, 0f);

        // --- 시간이 갈수록 투명해지기 ---
        if (sr != null)
        {
            // 남은 수명 비율 (1 → 0)
            float alpha = 1f - (timer / lifeTime);

            Color c = sr.color;
            c.a = alpha;        // a = 투명도. 1이면 불투명, 0이면 완전 투명
            sr.color = c;
        }

        // --- 수명이 다하면 스스로 제거 ---
        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }
}