using System;
using System.Windows.Forms;

namespace CS2Helper
{
    internal static class Program
    {
        public const string Title = "CS2 방장 도우미";

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Settings settings = Settings.Load();
            Cs2Paths.Detect(settings.InstallDir);

            if (!Cs2Paths.Found && !AskForInstallDir())
                return;

            settings.InstallDir = Cs2Paths.InstallDir;
            settings.Save();

            Application.Run(new MainForm(settings));
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

                    if (picker.ShowDialog() != DialogResult.OK) return false;
                    if (Cs2Paths.UseManual(picker.SelectedPath)) return true;
                }

                Show("그 폴더에서는 CS2를 찾지 못했어요.\n\n" +
                     "Steam 라이브러리에서 CS2를 우클릭 → 관리 → 로컬 파일 보기 로\n" +
                     "열리는 폴더를 골라 주세요.");
            }
        }

        private static void Show(string text)
        {
            MessageBox.Show(text, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
