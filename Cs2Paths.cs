using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CS2PracticeHost
{
    /// <summary>Steam·CS2 설치 위치를 찾는다. 못 찾으면 사용자가 직접 고른다.</summary>
    internal static class Cs2Paths
    {
        private const string InstallFolderName = "Counter-Strike Global Offensive";

        public static string SteamExe { get; private set; }
        public static string InstallDir { get; private set; }

        public static string CsgoDir { get { return Path.Combine(InstallDir, @"game\csgo"); } }
        public static string CfgDir { get { return Path.Combine(CsgoDir, "cfg"); } }
        public static string MapsDir { get { return Path.Combine(CsgoDir, "maps"); } }
        public static string ConsoleLog { get { return Path.Combine(CsgoDir, "console.log"); } }
        public static string Cs2Exe { get { return Path.Combine(InstallDir, @"game\bin\win64\cs2.exe"); } }

        public static bool Found { get { return InstallDir != null && SteamExe != null; } }

        /// <summary>레지스트리 → 라이브러리 폴더 순으로 찾고, 저장해 둔 수동 경로도 받아들인다.</summary>
        public static void Detect(string savedInstallDir)
        {
            SteamExe = ReadSteamValue("SteamExe");
            string steamRoot = ReadSteamValue("SteamPath");

            if (SteamExe == null && steamRoot != null)
            {
                string guess = Path.Combine(steamRoot, "steam.exe");
                if (File.Exists(guess)) SteamExe = guess;
            }

            if (IsInstallDir(savedInstallDir)) { InstallDir = savedInstallDir; return; }

            foreach (string library in LibraryFolders(steamRoot))
            {
                string candidate = Path.Combine(library, @"steamapps\common", InstallFolderName);
                if (IsInstallDir(candidate)) { InstallDir = candidate; return; }
            }
        }

        /// <summary>사용자가 고른 폴더를 설치 폴더로 받아들인다. csgo 안쪽을 골라도 위로 거슬러 찾는다.</summary>
        public static bool UseManual(string picked)
        {
            if (string.IsNullOrEmpty(picked)) return false;

            for (var dir = new DirectoryInfo(picked); dir != null; dir = dir.Parent)
            {
                if (IsInstallDir(dir.FullName)) { InstallDir = dir.FullName; return true; }
            }
            return false;
        }

        private static bool IsInstallDir(string path)
        {
            return !string.IsNullOrEmpty(path)
                && Directory.Exists(Path.Combine(path, @"game\csgo\maps"));
        }

        private static string ReadSteamValue(string name)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                if (key == null) return null;
                string value = key.GetValue(name) as string;
                if (string.IsNullOrEmpty(value)) return null;
                return value.Replace('/', '\\');   // SteamPath는 슬래시 방향이 섞여 온다
            }
        }

        /// <summary>Steam 루트 + libraryfolders.vdf 가 가리키는 다른 드라이브의 라이브러리들.</summary>
        private static IEnumerable<string> LibraryFolders(string steamRoot)
        {
            if (string.IsNullOrEmpty(steamRoot)) yield break;
            yield return steamRoot;

            string vdf = Path.Combine(steamRoot, @"steamapps\libraryfolders.vdf");
            if (!File.Exists(vdf)) yield break;

            string text;
            try { text = File.ReadAllText(vdf); }
            catch (IOException) { yield break; }

            foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]*)\""))
            {
                yield return m.Groups[1].Value.Replace(@"\\", @"\");
            }
        }
    }
}
