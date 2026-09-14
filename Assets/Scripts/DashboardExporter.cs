using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

// ============================================================
//  웹 대시보드 열기
//
//  "내 기록 보기" 버튼 → OpenDashboard() 를 부르면
//    ① StreamingAssets/dashboard 폴더를 쓰기 가능한 곳으로 복사하고
//    ② 그 옆에 기록을 담은 data.js 를 쓰고
//    ③ 기본 브라우저로 index.html 을 연다
//
//  ★왜 복사하나: StreamingAssets는 읽기 전용이라 data.js를 못 쓴다.
//  ★왜 data.js 인가: file:// 에서는 fetch()로 json을 못 읽는다(브라우저가 막음).
//    script 태그로 읽는 js 파일이면 막히지 않는다.
//
//  쓰는 법: 빈 오브젝트에 붙이고 버튼 OnClick에 OpenDashboard() 연결
// ============================================================
public class DashboardExporter : MonoBehaviour
{
    [Header("폴더 이름")]
    public string folderName = "dashboard";     // StreamingAssets 안의 폴더 이름

    [Header("브라우저가 안 열릴 때")]
    public GameObject fallbackPanel;            // "브라우저를 열 수 없어요" 안내 (없으면 비워둬도 됨)

    // 웹으로 넘기는 기록 한 줄 (RecordStore의 GameRecord에서 필요한 것만)
    class WebRecord
    {
        public string day;
        public string playedAt;
        public int stage;
        public int reps;
        public float maxEffort;
        public bool hasEffort;
        public string side;
    }

    // 웹으로 넘기는 전체 꾸러미
    class WebData
    {
        public string nickname;
        public string generatedAt;
        public int totalPlays;                       // 전체 판 수 (성장 단계에 씀)
        public int avatar;                           // 프로필 캐릭터 번호 (0~7)
        public List<WebRecord> records = new List<WebRecord>();
    }

    // ------------------------------------------------------------
    //  버튼이 부르는 함수
    // ------------------------------------------------------------
    public void OpenDashboard()
    {
        try
        {
            string outDir = Path.Combine(Application.persistentDataPath, folderName);

            // ① 웹 파일들 복사 (매번 덮어쓴다 — 웹을 고쳤을 때 반영되게)
            CopyWebFiles(outDir);

            // ② 기록을 data.js 로
            WriteData(outDir);

            // ③ 브라우저로 열기
            string indexPath = Path.Combine(outDir, "index.html");

            if (!File.Exists(indexPath))
            {
                Debug.LogError("[Dashboard] index.html이 없습니다: " + indexPath);
                ShowFallback();
                return;
            }

            string url = "file:///" + indexPath.Replace("\\", "/");

            Debug.Log("[Dashboard] 여는 중 — " + url);

            Application.OpenURL(url);
        }
        catch (Exception e)
        {
            Debug.LogError("[Dashboard] 열기 실패: " + e.Message);
            ShowFallback();
        }
    }

    void ShowFallback()
    {
        if (fallbackPanel != null) fallbackPanel.SetActive(true);
    }

    // ------------------------------------------------------------
    //  ① 웹 파일 복사
    // ------------------------------------------------------------
    void CopyWebFiles(string outDir)
    {
        string srcDir = Path.Combine(Application.streamingAssetsPath, folderName);

        if (!Directory.Exists(srcDir))
        {
            throw new Exception("StreamingAssets/" + folderName + " 폴더가 없습니다");
        }

        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        // 폴더 안의 파일 전부 복사 (하위 폴더 포함)
        string[] files = Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories);

        for (int i = 0; i < files.Length; i++)
        {
            // ⚠유니티가 만드는 .meta 파일은 빼고
            if (files[i].EndsWith(".meta")) continue;

            string rel = files[i].Substring(srcDir.Length).TrimStart('/', '\\');
            string dst = Path.Combine(outDir, rel);

            string dstFolder = Path.GetDirectoryName(dst);
            if (!Directory.Exists(dstFolder)) Directory.CreateDirectory(dstFolder);

            File.Copy(files[i], dst, true);
        }
    }

    // ------------------------------------------------------------
    //  ② 기록을 data.js 로 쓰기
    // ------------------------------------------------------------
    void WriteData(string outDir)
    {
        WebData data = new WebData();

        data.nickname = RecordStore.Nickname;
        data.generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        data.totalPlays = RecordStore.PlayCount();      // 전체 판 수 (스테이지7 좌우 중복 처리됨)
        data.avatar = ProfileStore.AvatarOf(RecordStore.Nickname);   // 헤더에 띄울 캐릭터

        List<GameRecord> all = RecordStore.All();

        for (int i = 0; i < all.Count; i++)
        {
            GameRecord r = all[i];

            WebRecord w = new WebRecord();
            w.day = r.day;
            w.playedAt = r.playedAt;
            w.stage = r.stage;
            w.reps = r.reps;
            w.maxEffort = r.maxEffort;
            w.hasEffort = r.hasEffort;
            w.side = r.side;

            data.records.Add(w);
        }

        string json = JsonConvert.SerializeObject(data, Formatting.Indented);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("// 이 파일은 게임이 자동으로 만듭니다. 직접 고치지 마세요.");
        sb.AppendLine("window.DASHBOARD_DATA = " + json + ";");

        // ★UTF-8 BOM 없이 써야 브라우저가 한글을 제대로 읽는다
        File.WriteAllText(Path.Combine(outDir, "data.js"), sb.ToString(), new UTF8Encoding(false));

        Debug.Log("[Dashboard] data.js 저장 — 기록 " + data.records.Count + "줄 / 전체 " + data.totalPlays
                  + "판 / " + data.nickname + " (캐릭터 " + data.avatar + ")");
    }

    // ------------------------------------------------------------
    //  확인용 — 폴더가 어디인지
    // ------------------------------------------------------------
    public void OpenFolder()
    {
        Application.OpenURL("file:///" + Application.persistentDataPath.Replace("\\", "/"));
    }
}