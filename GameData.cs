using System.Collections.Generic;
using System.Linq;

namespace CS2PracticeHost
{
    internal sealed class GameMap
    {
        public string Id;
        public string Name;
        public GameMap(string id, string name) { Id = id; Name = name; }
    }

    internal sealed class GameMode
    {
        public string Name;
        public int Type;
        public int Mode;
        /// <summary>맵 로딩 때 CS2가 읽는 gamemode_&lt;Cfg&gt;_server.cfg. 탈환은 봇을 모드 규칙이 정해서 없다.</summary>
        public string Cfg;
        public GameMap[] Maps;

        public bool BotsConfigurable { get { return Cfg != null; } }
    }

    /// <summary>게임 '연습' 탭의 이름·순서를 그대로 따른다.</summary>
    internal static class GameData
    {
        private static readonly GameMap[] ClassicMaps =
        {
            new GameMap("de_cache", "무기창고"),
            new GameMap("de_anubis", "아누비스"),
            new GameMap("de_inferno", "인페르노"),
            new GameMap("de_mirage", "신기루"),
            new GameMap("de_dust2", "더스트2"),
            new GameMap("de_nuke", "뉴크"),
            new GameMap("de_ancient", "고대"),
            new GameMap("de_train", "열차"),
            new GameMap("de_vertigo", "버티고"),
            new GameMap("de_overpass", "오버패스"),
            new GameMap("de_boulder", "볼더"),
            new GameMap("de_fachwerk", "파흐베르크"),
            new GameMap("cs_shelter", "쉘터"),
            new GameMap("cs_office", "사무실"),
            new GameMap("cs_italy", "이탈리아"),
        };

        private static readonly GameMap[] RetakeMaps = ClassicMaps.Take(10).ToArray();

        private static readonly GameMap[] ArmsMaps =
        {
            new GameMap("ar_baggage", "수하물"),
            new GameMap("ar_shoots", "재배소(낮)"),
            new GameMap("ar_shoots_night", "재배소(밤)"),
            new GameMap("ar_pool_day", "풀 데이"),
        };

        public static readonly GameMode[] Modes =
        {
            new GameMode { Name = "캐주얼",      Type = 0, Mode = 0, Cfg = "casual",     Maps = ClassicMaps },
            new GameMode { Name = "데스매치",    Type = 1, Mode = 2, Cfg = "deathmatch", Maps = ClassicMaps },
            new GameMode { Name = "무기 레이스", Type = 1, Mode = 0, Cfg = "armsrace",   Maps = ArmsMaps },
            new GameMode { Name = "탈환",        Type = 0, Mode = 5, Cfg = null,         Maps = RetakeMaps },
        };

        /// <summary>competitive는 예전 버전이 만들어 둔 파일을 정리하려고 남겨 둔다.</summary>
        public static readonly string[] ServerCfgs = { "casual", "deathmatch", "armsrace", "competitive" };

        public static readonly KeyValuePair<string, int>[] BotLevels =
        {
            new KeyValuePair<string, int>("쉬움", 0),
            new KeyValuePair<string, int>("보통", 1),
            new KeyValuePair<string, int>("어려움", 2),
            new KeyValuePair<string, int>("전문가", 3),
        };

        public static readonly KeyValuePair<string, string>[] BotTeams =
        {
            new KeyValuePair<string, string>("양쪽에 섞기", "any"),
            new KeyValuePair<string, string>("테러리스트 팀만", "t"),
            new KeyValuePair<string, string>("대테러 팀만", "ct"),
        };

        public static IEnumerable<string> BotCounts
        {
            get
            {
                yield return "게임 기본값";
                for (int i = 0; i <= 10; i++) yield return i + " 명";
            }
        }
    }
}
