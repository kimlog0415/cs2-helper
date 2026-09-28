using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace CS2PracticeHost
{
    internal static class Program
    {
        public const string Title = "CS2 Practice Host";

        /// <summary>
        /// 본 창이 뜨기 전에 띄우는 창은 주인이 없으면 다른 창 뒤로 숨는다.
        /// 사용자 눈에는 더블클릭해도 아무 일이 없는 것처럼 보인다.
        /// </summary>
        private static Form _startupOwner;

        private static bool _reported;

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Application.Run 전에 터진 예외는 오류창도 없이 프로세스를 끝낸다.
            // 그러면 원인을 알 길이 없으므로 직접 잡아서 보여 준다.
            Application.ThreadException += (s, e) => ReportCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception);

            try { Start(); }
            catch (Exception ex) { ReportCrash(ex); }
        }

        private static void Start()
        {
            Settings settings = Settings.Load();
            Cs2Paths.Detect(settings.InstallDir);

            using (_startupOwner = NewTopmostOwner())
            {
                if (!Cs2Paths.Found && !AskForInstallDir())
                    return;
            }
            _startupOwner = null;

            settings.InstallDir = Cs2Paths.InstallDir;
            settings.Save();

            Application.Run(new MainForm(settings));
        }

        /// <summary>화면에 보이지 않지만 다른 창보다 앞에 서는 주인 창.</summary>
        private static Form NewTopmostOwner()
        {
            var owner = new Form
            {
                TopMost = true,
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                Size = new Size(1, 1),
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000),
            };
            owner.Show();
            return owner;
        }

        /// <summary>자동 탐지 실패 — 사용자가 CS2 폴더를 직접 고르게 한다.</summary>
        private static bool AskForInstallDir()
        {
            if (Cs2Paths.SteamExe == null)
            {
                Show("Steam을 찾지 못했어요.\n\nSteam이 설치돼 있는지 확인해 주세요.");
                return false;
            }

            Show("CS2 설치 폴더를 찾지 못했어요.\n\n" +
                 "다음 창에서 CS2가 설치된 폴더를 골라 주세요.\n" +
                 "(이름이 'Counter-Strike Global Offensive' 인 폴더예요)");

            while (true)
            {
                using (var picker = new FolderBrowserDialog())
                {
                    picker.Description = "CS2 설치 폴더를 선택하세요";
                    picker.ShowNewFolderButton = false;

                    if (picker.ShowDialog(_startupOwner) != DialogResult.OK) return false;
                    if (Cs2Paths.UseManual(picker.SelectedPath)) return true;
                }

                Show("그 폴더에서는 CS2를 찾지 못했어요.\n\n" +
                     "Steam 라이브러리에서 CS2를 우클릭 → 관리 → 로컬 파일 보기 로\n" +
                     "열리는 폴더를 골라 주세요.");
            }
        }

        private static void Show(string text)
        {
            MessageBox.Show(_startupOwner, text, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ReportCrash(Exception error)
        {
            if (error == null || _reported) return;
            _reported = true;

            var report = new StringBuilder();
            report.AppendLine(error.GetType().Name + ": " + error.Message);
            report.AppendLine();
            report.AppendLine(error.StackTrace);

            try { Clipboard.SetText(report.ToString()); }
            catch (Exception) { }

            using (Form owner = NewTopmostOwner())
            {
                MessageBox.Show(owner,
                    "문제가 생겨 앱을 열지 못했어요.\n\n" +
                    error.GetType().Name + "\n" + error.Message + "\n\n" +
                    "자세한 내용이 클립보드에 복사됐어요.\n만든 사람에게 붙여넣어 보내 주세요.",
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Environment.Exit(1);
        }
    }
}
