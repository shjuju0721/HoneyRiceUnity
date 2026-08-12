using UnityEngine;
using UnityEngine.SceneManagement;   // 씬 전환 기능을 쓰려면 필요

// 버튼에 연결해서 씬을 이동시키는 스크립트
public class SceneLoader : MonoBehaviour
{
    // ===== 씬 이름을 받아서 이동 =====
    // public이라 유니티 버튼의 OnClick에서 직접 호출 가능
    public void LoadScene(string sceneName)
    {
        // Build Settings에 등록된 씬 이름으로 이동
        SceneManager.LoadScene(sceneName);
    }
}