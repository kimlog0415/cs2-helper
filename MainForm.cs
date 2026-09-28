using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CS2Helper
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
        private readonly Timer _watchServer = new Timer { Interval = 2000 };
        private readonly Timer _watchProcess = new Timer { Interval = 3000 };

        private GameMap[] _maps = new GameMap[0];
        private DateTime _launchedAt;
        private bool _loading = true;

        public MainForm(Settings settings)
        {
            _settings = settings;
            BuildLayout();
            FillControls();
            RestoreSelection();
            _loading = false;

            CfgWriter.WriteBindCfg();
            SaveAll();

            _watchServer.Tick += OnServerTick;
            _watchProcess.Tick += (s, e) => UpdateBanner();
            Shown += (s, e) => { UpdateBanner(); _watchProcess.Start(); };
        }

        // ---------- 화면 ----------

        private void BuildLayout()
        {
            Text = Program.Title;
            Font = new Font("맑은 고딕", 10);
            ClientSize = new Size(380, 588);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // 발베 아이콘을 재배포하지 않고 사용자 PC의 cs2.exe에서 꺼내 쓴다
            try { Icon = Icon.ExtractAssociatedIcon(Cs2Paths.Cs2Exe); }
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
            _status.Size = new Size(340, 168);
            _status.ForeColor = Color.DarkGreen;
            Controls.Add(_status);

            _launch.Text = "▶  CS2 켜고 서버 열기";
            _launch.Font = new Font("맑은 고딕", 11, FontStyle.Bold);
            _launch.Location = new Point(20, 438);
            _launch.Size = new Size(340, 46);
            _launch.Click += OnLaunchClick;
            Controls.Add(_launch);

            _copyLink.Text = "참가 링크 복사 (친구에게 보내기)";
            _copyLink.Location = new Point(20, 492);
            _copyLink.Size = new Size(340, 40);
            _copyLink.Click += OnCopyLinkClick;
            Controls.Add(_copyLink);

            _copyAddress.Text = "콘솔용 주소 복사";
            _copyAddress.Location = new Point(20, 540);
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

            if (_watchServer.Enabled) return;   // 서버 여는 중에는 진행 상황 문구를 유지한다

            string note = mode.BotsConfigurable
                ? ""
                : "\n※ 탈환은 봇을 게임이 정해요 (수비 테러리스트 봇)";

            // F10을 먼저 둔다 — 친구들이 들어온 뒤로는 이쪽이 기본이고, F9는 주소가 바뀌어 다 끊긴다
            _status.Text =
                "저장됐어요!  (" + mode.Name + " / " + map.Name + ")" + note + "\n\n" +
                "▶ 버튼 : CS2 켜고 서버 열기 (항상 이걸로 켜세요)\n" +
                "F10 : (맵 안에서) 맵·모드 바꾸기 — 친구들 그대로\n" +
                "F9  : (맵 안에서) 서버 새로 열기 — 친구들 끊김\n\n" +
                "※ 방장이 메뉴로 나가면 서버가 닫혀요";
        }

        // ---------- 상태 표시줄 ----------

        private void UpdateBanner()
        {
            Process cs2 = Cs2Process.Find();
            if (cs2 == null)
            {
                SetBanner(Color.Gainsboro, Color.DimGray, "○  CS2 꺼져 있음 · 아래 ▶ 버튼으로 시작");
                _launch.Text = "▶  CS2 켜고 서버 열기";
                return;
            }

            _launch.Text = "▶  CS2 다시 켜고 서버 열기";

            if (!Cs2Process.HasCondebug(cs2))
            {
                SetBanner(Color.FromArgb(255, 243, 205), Color.DarkGoldenrod,
                    "●  CS2 실행 중 · 주소를 읽으려면 ▶로 다시 켜기");
                return;
            }

            ServerInfo opened = CurrentServer(cs2);
            if (opened != null)
                SetBanner(Color.FromArgb(212, 237, 218), Color.DarkGreen,
                    "●  서버 열림 (" + opened.OpenedAt.ToString("HH:mm") + ") · 맵 안에서 F9 / F10");
            else
                SetBanner(Color.FromArgb(209, 231, 248), Color.SteelBlue,
                    "●  CS2 실행 중 · 서버 없음 (메인 화면)");
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

            if (Cs2Process.Find() != null)
            {
                DialogResult answer = MessageBox.Show(
                    "CS2가 이미 켜져 있어요.\n\n" +
                    "게임 안이라면 [아니요]를 누르고 게임에서 F9를 누르세요.\n" +
                    "메인 화면이라면 [예]를 누르면 CS2를 껐다가 새로 켜서 서버를 열어요.",
                    Program.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (answer != DialogResult.Yes) return;

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
            _launch.Enabled = false;
            _watchServer.Start();
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

        private void OnServerTick(object sender, EventArgs e)
        {
            ServerInfo server = ConsoleLog.CurrentServer(_launchedAt.AddSeconds(-5));
            if (server != null)
            {
                _watchServer.Stop();
                _launch.Enabled = true;
                CopyToClipboard(server.JoinLink);
                SystemSounds.Asterisk.Play();
                _status.Text =
                    "서버가 열렸어요! 참가 링크가 복사됐어요.\n" +
                    "카톡/디스코드에 Ctrl+V 로 보내세요.\n" +
                    "친구가 누르면 CS2가 켜지면서 바로 들어와요.\n\n" +
                    server.JoinLink;
            }
            else if ((DateTime.Now - _launchedAt).TotalMinutes > 4)
            {
                _watchServer.Stop();
                _launch.Enabled = true;
                _status.Text = "서버를 찾지 못했어요.\nCS2가 켜졌는지 확인하고 다시 눌러주세요.";
            }
        }

        // ---------- 친구에게 보내기 ----------

        private void OnCopyLinkClick(object sender, EventArgs e)
        {
            ServerInfo server = FindServer();
            if (server == null) return;

            CopyToClipboard(server.JoinLink);
            Tell("참가 링크가 복사됐어요!\n카톡/디스코드에 Ctrl+V 로 붙여넣으세요.\n\n" +
                 server.JoinLink + "\n\n" +
                 "친구가 이 링크를 누르면 CS2가 켜지면서\n바로 서버로 들어와요. (콘솔 설정 필요 없음)\n\n" +
                 "글자로만 보이고 눌리지 않으면,\n친구에게 복사해서 실행창(Win+R)에 붙여넣으라고 하세요.\n\n" +
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
            Process cs2 = Cs2Process.Find();
            if (cs2 == null)
            {
                Tell("CS2가 꺼져 있어요.\n▶ 버튼으로 서버를 열어주세요.");
                return null;
            }

            if (!Cs2Process.HasCondebug(cs2))
            {
                Tell("이 CS2는 앱으로 켠 게 아니라 주소를 읽을 수 없어요.\n\n" +
                     "▶ 버튼으로 다시 켜주세요.");
                return null;
            }

            ServerInfo server = CurrentServer(cs2);
            if (server == null)
                Tell("열려 있는 서버가 없어요.\n\n" +
                     "▶ 버튼으로 서버를 열거나,\n맵 안에서 F9를 눌러주세요.");

            return server;
        }

        private static ServerInfo CurrentServer(Process cs2)
        {
            try { return ConsoleLog.CurrentServer(cs2.StartTime.AddSeconds(-5)); }
            catch (Exception) { return null; }
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
