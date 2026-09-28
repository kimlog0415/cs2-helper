using System;
using System.Diagnostics;
using System.Management;
using System.Threading;

namespace CS2PracticeHost
{
    internal static class Cs2Process
    {
        private static int _checkedPid = -1;
        private static bool _checkedHasCondebug;

        public static Process Find()
        {
            Process[] found = Process.GetProcessesByName("cs2");
            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>
        /// 앱 밖에서 켠 CS2는 -condebug 가 없어 로그가 안 남는다. 그러면 주소를 읽을 수 없다.
        /// WMI 조회가 느려서 같은 프로세스는 한 번만 확인한다.
        /// </summary>
        public static bool HasCondebug(Process cs2)
        {
            if (cs2.Id == _checkedPid) return _checkedHasCondebug;

            _checkedPid = cs2.Id;
            _checkedHasCondebug = CommandLineOf(cs2.Id).IndexOf("-condebug", StringComparison.OrdinalIgnoreCase) >= 0;
            return _checkedHasCondebug;
        }

        private static string CommandLineOf(int pid)
        {
            try
            {
                using (var search = new ManagementObjectSearcher(
                    "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + pid))
                using (ManagementObjectCollection results = search.Get())
                {
                    foreach (ManagementBaseObject item in results)
                        using (item)
                            return (item["CommandLine"] as string) ?? "";
                }
            }
            catch (Exception)
            {
                // 조회가 막히면 있다고 보고 진행한다 (없다고 단정해 경고를 띄우는 편이 더 나쁘다)
                return "-condebug";
            }
            return "";
        }

        /// <summary>창을 닫아 보고, 안 닫히면 강제 종료한다.</summary>
        public static void CloseAll()
        {
            Process[] running = Process.GetProcessesByName("cs2");
            if (running.Length == 0) return;

            foreach (Process p in running)
                try { p.CloseMainWindow(); } catch (Exception) { }

            foreach (Process p in running)
            {
                try { if (!p.WaitForExit(15000)) p.Kill(); }
                catch (Exception) { }
            }

            Thread.Sleep(3000);   // Steam이 게임 종료를 알아챌 시간
        }
    }
}
