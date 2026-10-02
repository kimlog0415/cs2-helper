using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CS2PracticeHost
{
    internal sealed class MainForm : Form
    {
        private readonly Settings _settings;

        private readonly Label _banner = new Label();
        private readonly Label _status = new Label();
        private readonly ComboBox _mode = new ComboBox();
        private readonly ComboBox _map = new ComboBox();
        private readonly ComboBox _bots = new ComboBox();
        private readonly ComboBox _level = new ComboBox();
        private readonly ComboBox _team = new ComboBox();
        private readonly Button _launch = new Button();
        private readonly Button _copyLink = new Button();
        private readonly Button _copyAddress = new Button();
        private readonly Timer _watch = new Timer { Interval = 2000 };

        private GameMap[] _maps = new GameMap[0];
        private bool _loading = true;

        /// <summary>▶로 서버를 여는 중. 이 동안은 진행 상황 문구를 유지하고 버튼을 잠근다.</summary>
        private bool _launching;
        private DateTime _launchedAt;

        /// <summary>이미 복사해 준 서버. 맵 안에서 F10으로 연 서버도 이걸로 알아챈다.</summary>
        private string _copiedAddress;

        public MainForm(Settings settings)
        {
            _settings = settings;
            BuildLayout();
            FillControls();
            RestoreSelection();
            _loading = false;

            CfgWriter.WriteBindCfg();
            SaveAll();

            _watch.Tick += (s, e) => RefreshStatus();
            Shown += (s, e) => { RefreshStatus(); _watch.Start(); };
        }

        // ---------- 화면 ----------

        private void BuildLayout()
        {
            Text = Program.Title;
            Font = new Font("맑은 고딕", 10);
            ClientSize = new Size(380, 612);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // exe에 박아 둔 자기 아이콘 (Valve 자산을 쓰지 않는다)
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }

            _banner.Location = new Point(20, 14);
            _banner.Size = new Size(340, 32);
            _banner.TextAlign = ContentAlignment.MiddleLeft;
            _banner.Padding = new Padding(8, 0, 0, 0);
            _banner.Font = new Font("맑은 고딕", 10, FontStyle.Bold);
            Controls.Add(_banner);

            int y = 62;
            AddRow("모드", _mode, ref y);
            AddRow("맵", _map, ref y);
            AddRow("봇 수", _bots, ref y);
            AddRow("봇 난이도", _level, ref y);
            AddRow("봇 팀", _team, ref y);

            _status.Location = new Point(20, y + 2);
            _status.Size = new Size(340, 192);
            _status.ForeColor = Color.DarkGreen;
            Controls.Add(_status);

            _launch.Text = "▶  CS2 켜고 서버 열기";
            _launch.Font = new Font("맑은 고딕", 11, FontStyle.Bold);
            _launch.Location = new Point(20, 462);
            _launch.Size = new Size(340, 46);
            _launch.Click += OnLaunchClick;
            Controls.Add(_launch);

            _copyLink.Text = "참가 링크 복사 (친구에게 보내기)";
            _copyLink.Location = new Point(20, 516);
            _copyLink.Size = new Size(340, 40);
            _copyLink.Click += OnCopyLinkClick;
            Controls.Add(_copyLink);

            _copyAddress.Text = "콘솔용 주소 복사";
            _copyAddress.Location = new Point(20, 564);
            _copyAddress.Size = new Size(340, 32);
            _copyAddress.Click += OnCopyAddressClick;
            Controls.Add(_copyAddress);
        }

        private void AddRow(string label, ComboBox box, ref int y)
        {
            var text = new Label { Text = label, Location = new Point(20, y + 4), AutoSize = true };
            box.DropDownStyle = ComboBoxStyle.DropDownList;
            box.Location = new Point(120, y);
            box.Width = 240;
            box.MaxDropDownItems = 16;
            Controls.Add(text);
            Controls.Add(box);
            y += 40;
        }

        private void FillControls()
        {
            foreach (GameMode m in GameData.Modes) _mode.Items.Add(m.Name);
            foreach (string c in GameData.BotCounts) _bots.Items.Add(c);
            foreach (var l in GameData.BotLevels) _level.Items.Add(l.Key);
            foreach (var t in GameData.BotTeams) _team.Items.Add(t.Key);

            _mode.SelectedIndexChanged += OnModeChanged;
            foreach (ComboBox box in new[] { _map, _bots, _level, _team })
                box.SelectedIndexChanged += (s, e) => SaveAll();
        }

        private void RestoreSelection()
        {
            _mode.SelectedIndex = Math.Max(0, IndexOfName(_mode, _settings.Mode));
            FillMaps(_settings.Map ?? "de_dust2");

            _bots.SelectedIndex = _settings.Bots >= 0 && _settings.Bots < _bots.Items.Count
                ? _settings.Bots : 0;
            _level.SelectedIndex = Math.Max(1, IndexOfName(_level, _settings.Level));
            _team.SelectedIndex = Math.Max(0, IndexOfName(_team, _settings.Team));
        }

        private static int IndexOfName(ComboBox box, string name)
        {
            return name == null ? -1 : box.Items.IndexOf(name);
        }

        private GameMode SelectedMode { get { return GameData.Modes[_mode.SelectedIndex]; } }

        private GameMap SelectedMap
        {
            get { return _map.SelectedIndex >= 0 ? _maps[_map.SelectedIndex] : null; }
        }

        /// <summary>설치된 맵만 보여준다. 모드를 바꿔도 같은 맵이 있으면 유지한다.</summary>
        private void FillMaps(string wantedId)
        {
            _maps = SelectedMode.Maps
                .Where(m => File.Exists(Path.Combine(Cs2Paths.MapsDir, m.Id + ".vpk")))
                .ToArray();

            _map.Items.Clear();
            foreach (GameMap m in _maps) _map.Items.Add(m.Name);

            if (_maps.Length == 0) return;   // 설치된 맵이 없으면 고를 것도 없다 (빈 목록에 SelectedIndex를 주면 예외)

            int index = Array.FindIndex(_maps, m => m.Id == wantedId);
            if (index < 0) index = Array.FindIndex(_maps, m => m.Id == "de_dust2");
            _map.SelectedIndex = Math.Max(0, index);
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            GameMap keep = SelectedMap;

            // 시작할 때도 불리므로 불러오는 중이라는 표시를 덮어쓰면 안 된다
            bool wasLoading = _loading;
            _loading = true;
            FillMaps(keep != null ? keep.Id : null);
            _loading = wasLoading;

            SaveAll();
        }

        // ---------- 저장 ----------

        private void SaveAll()
        {
            // 하나라도 아직 안 정해졌으면 저장하지 않는다 (되살리는 도중에 불릴 수 있다)
            if (_loading) return;
            foreach (ComboBox box in new[] { _mode, _map, _bots, _level, _team })
                if (box.SelectedIndex < 0) return;

            GameMode mode = SelectedMode;
            GameMap map = SelectedMap;

            // 탈환은 모드 규칙이 봇을 정해서 설정이 먹지 않는다
            foreach (ComboBox box in new[] { _bots, _level, _team })
                box.Enabled = mode.BotsConfigurable;

            CfgWriter.WriteMatchCfgs(mode, map.Id);
            CfgWriter.WriteBotCfgs(
                _bots.SelectedIndex,
                GameData.BotLevels[_level.SelectedIndex].Value,
                GameData.BotTeams[_team.SelectedIndex].Value);

            _settings.Mode = mode.Name;
            _settings.Map = map.Id;
            _settings.Bots = _bots.SelectedIndex;
            _settings.Level = _level.Text;
            _settings.Team = _team.Text;
            _settings.Save();

            if (_launching) return;   // 서버 여는 중에는 진행 상황 문구를 유지한다

            string note = mode.BotsConfigurable
                ? OneSidedBotNote()
                : "\n※ 탈환은 봇을 게임이 정해요 (수비 테러리스트 봇)";

            _status.Text =
                "저장됐어요!  (" + mode.Name + " / " + map.Name + ")" + note + "\n\n" +
                "▶ 버튼 : CS2 켜고 서버 열기 (항상 이걸로 켜세요)\n" +
                "F10 : (맵 안에서) 맵·모드 바꾸기 — 친구들 그대로\n\n" +
                "※ 봇 설정은 맵을 새로 열 때 반영돼요\n" +
                "※ 방장이 메뉴로 나가면 서버가 닫혀요";
        }

        /// <summary>
        /// 봇을 한쪽 팀으로 몰면 게임이 팀 인원 차이를 2명으로 제한해 「사람 수 + 2」에서 멈춘다.
        /// 고를 수는 있는데 그만큼 안 나오는 상태라, 이유를 알려주지 않으면 고장으로 보인다.
        /// </summary>
        private string OneSidedBotNote()
        {
            bool oneSided = _team.SelectedIndex >= 0
                && GameData.BotTeams[_team.SelectedIndex].Value != "any";

            if (!oneSided || _bots.SelectedIndex <= 1) return "";

            return "\n※ 한 팀으로 몰면 봇은 「사람 수 + 2」까지만 나와요\n" +
                   "   (다 채우려면 봇 팀을 「양쪽에 섞기」로)";
        }

        // ---------- 상태 표시줄 ----------

        /// <summary>상태를 읽어 표시줄을 맞추고, 새 서버가 보이면 링크를 복사한다.</summary>
        private void RefreshStatus()
        {
            Cs2Status status = Cs2Watcher.Read();

            _launch.Text = status.State == Cs2State.Off
                ? "▶  CS2 켜고 서버 열기"
                : "▶  CS2 다시 켜고 서버 열기";

            switch (status.State)
            {
                case Cs2State.Off:
                    SetBanner(Color.Gainsboro, Color.DimGray, "○  CS2 꺼져 있음 · 아래 ▶ 버튼으로 시작");
                    break;
                case Cs2State.NoLog:
                    SetBanner(Color.FromArgb(255, 243, 205), Color.DarkGoldenrod,
                        "●  CS2 실행 중 · 주소를 읽으려면 ▶로 다시 켜기");
                    break;
                case Cs2State.NoServer:
                    SetBanner(Color.FromArgb(209, 231, 248), Color.SteelBlue,
                        "●  CS2 실행 중 · 서버 없음 (메인 화면)");
                    break;
                case Cs2State.ServerOpen:
                    SetBanner(Color.FromArgb(212, 237, 218), Color.DarkGreen,
                        "●  서버 열림 (" + status.Server.OpenedAt.ToString("HH:mm") + ") · 맵 안에서 F10");
                    NoticeNewServer(status.Server);
                    return;
            }

            if (_launching && (DateTime.Now - _launchedAt).TotalMinutes > 4)
            {
                _launching = false;
                _launch.Enabled = true;
                _status.Text = "서버를 찾지 못했어요.\nCS2가 켜졌는지 확인하고 다시 눌러주세요.";
            }
        }

        /// <summary>처음 보는 서버면 링크를 복사해 알린다. ▶로 열었든 맵 안에서 F10으로 열었든 같다.</summary>
        private void NoticeNewServer(ServerInfo server)
        {
            _launching = false;
            _launch.Enabled = true;

            if (server.Address == _copiedAddress) return;
            _copiedAddress = server.Address;

            CopyToClipboard(server.JoinLink);
            SystemSounds.Asterisk.Play();
            _status.Text =
                "서버가 열렸어요! 참가 링크가 복사됐어요.\n" +
                "카톡/디스코드에 Ctrl+V 로 보내세요.\n" +
                "친구가 누르면 안내 페이지를 거쳐 바로 들어와요.\n\n" +
                server.JoinLink;
        }

        private void SetBanner(Color back, Color fore, string text)
        {
            _banner.BackColor = back;
            _banner.ForeColor = fore;
            _banner.Text = text;
        }

        // ---------- CS2 켜고 서버 열기 ----------

        private async void OnLaunchClick(object sender, EventArgs e)
        {
            if (Cs2Paths.SteamExe == null || !File.Exists(Cs2Paths.SteamExe))
            {
                Tell("Steam을 찾지 못했어요.");
                return;
            }

            Cs2Status before = Cs2Watcher.Read();
            if (before.State != Cs2State.Off)
            {
                // 버튼에 "다시 켜고"라고 적혀 있으니 그대로 한다.
                // 서버가 열려 있을 때만 묻는다 — 친구들이 들어와 있으면 말없이 끊으면 안 된다.
                if (before.State == Cs2State.ServerOpen)
                {
                    DialogResult answer = MessageBox.Show(
                        "지금 서버가 열려 있어요.\n\n" +
                        "새로 켜면 들어와 있는 친구들이 전부 끊겨요.\n" +
                        "맵·모드만 바꿀 거라면 [아니요]를 누르고 맵 안에서 F10을 쓰세요.\n\n" +
                        "그래도 CS2를 껐다가 새로 켤까요?",
                        Program.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (answer != DialogResult.Yes) return;
                }

                _launch.Enabled = false;
                Cursor = Cursors.WaitCursor;
                await Task.Run(() => Cs2Process.CloseAll());
                Cursor = Cursors.Default;
                _launch.Enabled = true;
            }

            GameMode mode = SelectedMode;
            GameMap map = SelectedMap;
            if (map == null)
            {
                Tell("고를 수 있는 맵이 없어요.\nCS2 설치 폴더를 제대로 찾았는지 확인해 주세요.");
                return;
            }

            CfgWriter.WriteBindCfg();
            Process.Start(Cs2Paths.SteamExe, LaunchArguments(mode, map));

            _launchedAt = DateTime.Now;
            _launching = true;
            _launch.Enabled = false;
            _status.Text =
                "CS2 켜는 중...  (" + mode.Name + " / " + map.Name + ")\n\n" +
                "서버가 열리면 친구에게 보낼 참가 링크가\n자동으로 복사돼요. (1분 정도 걸려요)";
        }

        /// <summary>-condebug·바인드까지 인자로 붙여 사용자가 Steam 설정을 건드릴 일이 없게 한다.</summary>
        private static string LaunchArguments(GameMode mode, GameMap map)
        {
            return "-applaunch 730 -condebug" +
                   " +exec " + CfgWriter.BindCfg +
                   " +game_type " + mode.Type +
                   " +game_mode " + mode.Mode +
                   " +map " + map.Id;
        }

        // ---------- 친구에게 보내기 ----------

        private void OnCopyLinkClick(object sender, EventArgs e)
        {
            ServerInfo server = FindServer();
            if (server == null) return;

            CopyToClipboard(server.JoinLink);
            Tell("참가 링크가 복사됐어요!\n카톡/디스코드에 Ctrl+V 로 붙여넣으세요.\n\n" +
                 server.JoinLink + "\n\n" +
                 "친구가 링크를 누르면 안내 페이지가 열리고,\n" +
                 "거기서 [게임 접속하기]를 누르면 바로 들어와요.\n" +
                 "친구 쪽에 설치할 것도 콘솔 설정도 없어요.\n\n" +
                 SharingWarning);
        }

        private void OnCopyAddressClick(object sender, EventArgs e)
        {
            ServerInfo server = FindServer();
            if (server == null) return;

            CopyToClipboard(server.ConnectCommand);
            Tell("콘솔용 주소가 복사됐어요.\n\n" +
                 server.ConnectCommand + "\n\n" +
                 "친구가 게임 콘솔(~ 키)에 붙여넣는 방식이에요.\n" +
                 "보통은 [참가 링크 복사] 쪽이 더 편해요.\n\n" +
                 SharingWarning);
        }

        private const string SharingWarning =
            "※ 주소를 아는 사람은 누구나 들어올 수 있어요.\n" +
            "   공개된 곳에 올리지 말고 친구에게만 보내세요.";

        /// <summary>지금 살아 있는 서버만 돌려준다. 죽은 주소를 친구에게 보내면 안 된다.</summary>
        private ServerInfo FindServer()
        {
            Cs2Status status = Cs2Watcher.Read();
            switch (status.State)
            {
                case Cs2State.Off:
                    Tell("CS2가 꺼져 있어요.\n▶ 버튼으로 서버를 열어주세요.");
                    return null;

                case Cs2State.NoLog:
                    Tell("이 CS2는 앱으로 켠 게 아니라 주소를 읽을 수 없어요.\n\n" +
                         "▶ 버튼으로 다시 켜주세요.");
                    return null;

                // F10은 맵을 바꾸는 키라 서버가 돌고 있어야 먹는다. 여기선 ▶ 말고 길이 없다.
                case Cs2State.NoServer:
                    Tell("열려 있는 서버가 없어요.\n\n▶ 버튼으로 서버를 열어주세요.");
                    return null;

                default:
                    return status.Server;
            }
        }

        private static void CopyToClipboard(string text)
        {
            try { Clipboard.SetText(text); }
            catch (Exception) { /* 다른 프로그램이 클립보드를 잡고 있을 수 있다 */ }
        }

        private void Tell(string text)
        {
            MessageBox.Show(text, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
