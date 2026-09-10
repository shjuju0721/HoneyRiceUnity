using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

// ============================================================
//  프로필 저장소 — 넷플릭스처럼 쓰는 사람을 골라 시작한다
//
//  ★비밀번호를 두지 않는 이유:
//   개인 기기(집 태블릿·노트북)에 깔아 쓰는 앱이라 로그인이 어색하고,
//   어르신이 앱을 켤 때마다 비밀번호를 치는 건 큰 장벽이다.
//   기획서에도 "별명만 저장, 개인정보 미수집"으로 못 박아 두었다.
//
//  저장 위치: Application.persistentDataPath/profiles.json
// ============================================================

// ===== 프로필 하나 =====
[Serializable]
public class Profile
{
    public string name;        // 별명 (기록을 거르는 열쇠)
    public int avatar;         // 캐릭터 번호 (0~7 = 스테이지 1~8)
    public string createdAt;   // 만든 날
}

[Serializable]
public class ProfileBook
{
    public List<Profile> profiles = new List<Profile>();
}

public static class ProfileStore
{
    private const string FILE_NAME = "profiles.json";

    // ★최대 4명. 화면에 한 줄로 놓기 좋은 수.
    public const int MAX_PROFILES = 4;

    // ★캐릭터 이름 (스테이지 1~8 순서)
    //   avatar 번호가 곧 이 배열의 자리다.
    public static readonly string[] AVATAR_NAMES = {
        "토끼",        // 0
        "씨름 선수",   // 1
        "펭귄",        // 2
        "개구리",      // 3
        "파리",        // 4
        "포도 젤리",   // 5
        "푸딩 젤리",   // 6
        "토코"         // 7
    };

    private static ProfileBook book = null;

    // ============================================================
    //  읽기·쓰기
    // ============================================================
    static string FilePath()
    {
        return Path.Combine(Application.persistentDataPath, FILE_NAME);
    }

    public static void Load()
    {
        if (book != null) return;

        book = new ProfileBook();

        try
        {
            string path = FilePath();

            if (!File.Exists(path)) return;

            string json = File.ReadAllText(path);

            if (string.IsNullOrEmpty(json)) return;

            ProfileBook loaded = JsonConvert.DeserializeObject<ProfileBook>(json);

            if (loaded != null && loaded.profiles != null)
            {
                book = loaded;
            }
        }
        catch (Exception e)
        {
            // ★파일이 깨져도 앱은 켜져야 한다
            Debug.LogWarning("[Profile] 불러오기 실패 — 빈 목록으로 시작합니다: " + e.Message);
            book = new ProfileBook();
        }
    }

    static void Write()
    {
        try
        {
            string json = JsonConvert.SerializeObject(book, Formatting.Indented);
            File.WriteAllText(FilePath(), json);
        }
        catch (Exception e)
        {
            Debug.LogError("[Profile] 저장 실패: " + e.Message);
        }
    }

    // ============================================================
    //  바깥에서 쓰는 것들
    // ============================================================

    public static List<Profile> All()
    {
        Load();
        return book.profiles;
    }

    public static int Count()
    {
        return All().Count;
    }

    public static bool IsFull()
    {
        return Count() >= MAX_PROFILES;
    }

    // 이름이 이미 있는가 (같은 이름이 둘이면 기록이 섞인다)
    public static bool Exists(string name)
    {
        List<Profile> list = All();

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].name == name) return true;
        }

        return false;
    }

    // ★새 프로필 만들기
    //   돌려주는 값: 성공했으면 true, 못 만들었으면 false
    public static bool Add(string name, int avatar, out string why)
    {
        why = "";

        name = (name ?? "").Trim();

        if (name.Length == 0)
        {
            why = "이름을 적어 주세요";
            return false;
        }

        if (name.Length > 8)
        {
            why = "이름은 8글자까지예요";
            return false;
        }

        if (IsFull())
        {
            why = "자리가 다 찼어요 (최대 " + MAX_PROFILES + "명)";
            return false;
        }

        if (Exists(name))
        {
            why = "같은 이름이 이미 있어요";
            return false;
        }

        Load();

        Profile p = new Profile();
        p.name = name;
        p.avatar = Mathf.Clamp(avatar, 0, AVATAR_NAMES.Length - 1);
        p.createdAt = DateTime.Now.ToString("yyyy-MM-dd");

        book.profiles.Add(p);
        Write();

        Debug.Log("[Profile] 새 프로필 — " + name + " (" + AVATAR_NAMES[p.avatar] + ")");

        return true;
    }

    // ★프로필 지우기
    //   ⚠그 사람의 기록도 같이 사라진다. 부르기 전에 반드시 확인을 받을 것.
    public static void Remove(string name)
    {
        Load();

        for (int i = book.profiles.Count - 1; i >= 0; i--)
        {
            if (book.profiles[i].name == name)
            {
                book.profiles.RemoveAt(i);
            }
        }

        Write();

        // 지운 사람이 지금 쓰는 사람이었으면 비운다
        if (RecordStore.Nickname == name)
        {
            RecordStore.Nickname = "";
        }

        Debug.Log("[Profile] 프로필 삭제 — " + name);
    }

    // ★이 사람으로 시작한다
    public static void Select(string name)
    {
        RecordStore.Nickname = name;

        Debug.Log("[Profile] 선택 — " + name);
    }

    // 지금 고른 사람
    public static Profile Current()
    {
        string nick = RecordStore.Nickname;

        if (string.IsNullOrEmpty(nick)) return null;

        List<Profile> list = All();

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].name == nick) return list[i];
        }

        return null;
    }

    // 캐릭터 번호 찾기 (없으면 0)
    public static int AvatarOf(string name)
    {
        List<Profile> list = All();

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].name == name) return list[i].avatar;
        }

        return 0;
    }

    // ============================================================
    //  지우기 (테스트용)
    // ============================================================
    public static void ClearAll()
    {
        book = new ProfileBook();
        Write();

        RecordStore.Nickname = "";

        Debug.Log("[Profile] 프로필을 모두 지웠습니다.");
    }

    public static string WhereIsFile()
    {
        return FilePath();
    }
}