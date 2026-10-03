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

            // 아래 폴더 고르기 창부터 문구가 나가므로, 창을 띄우기 전에 언어를 정해 둔다
            Strings.Init(settings.Lang);

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
                Show(Strings.SteamMissing);
                return false;
            }

            Show(Strings.InstallDirMissing);

            while (true)
            {
                using (var picker = new FolderBrowserDialog())
                {
                    picker.Description = Strings.PickInstallDir;
                    picker.ShowNewFolderButton = false;

                    if (picker.ShowDialog(_startupOwner) != DialogResult.OK) return false;
                    if (Cs2Paths.UseManual(picker.SelectedPath)) return true;
                }

                Show(Strings.WrongInstallDir);
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
                    Strings.Crash(error.GetType().Name, error.Message),
                    Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Environment.Exit(1);
        }
    }
}
