using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CS2Helper
{
    internal sealed class ServerInfo
    {
        /// <summary>[A:1:1509803029:51595] 형태의 서버 주소.</summary>
        public string Address;
        public DateTime OpenedAt;

        /// <summary>콘솔에 직접 치는 형태.</summary>
        public string ConnectCommand { get { return "connect " + Address; } }

        /// <summary>누르면 CS2가 켜지면서 바로 접속되는 링크. 친구 쪽에 앱·콘솔 설정이 필요 없다.</summary>
        public string JoinLink { get { return "steam://rungameid/730//+connect " + Address; } }
    }

    /// <summary>-condebug 가 남기는 console.log 에서 서버 상태를 읽는다.</summary>
    internal static class ConsoleLog
    {
        private static readonly Regex ServerIdLine = new Regex(@"ServerSteamID=(\[A:[^\]]+\])");
        private static readonly Regex ShutdownLine = new Regex(
            @"Server shutting down: NETWORK_DISCONNECT_(DISCONNECT_BY_USER|REQUEST_HOSTSTATE_IDLE)");

        /// <summary>가장 마지막에 열린 서버의 접속 명령. 맵을 새로 열 때마다 주소가 바뀐다.</summary>
        public static ServerInfo LastServer()
        {
            string found = null;
            foreach (string line in ReadLines())
            {
                Match m = ServerIdLine.Match(line);
                if (m.Success) found = line;
            }
            if (found == null) return null;

            return new ServerInfo
            {
                Address = ServerIdLine.Match(found).Groups[1].Value,
                OpenedAt = ParseTime(found),
            };
        }

        /// <summary>지금 서버가 열려 있는지. 마지막 기록이 서버 시작인지 종료인지로 가른다.</summary>
        public static DateTime? OpenServerTime(DateTime since)
        {
            string last = null;
            foreach (string line in ReadLines())
            {
                if (ServerIdLine.IsMatch(line) || ShutdownLine.IsMatch(line)) last = line;
            }
            if (last == null || !ServerIdLine.IsMatch(last)) return null;

            DateTime at = ParseTime(last);
            return at < since ? (DateTime?)null : at;
        }

        /// <summary>로그 줄은 "MM/dd HH:mm:ss ..." 로 시작한다 (연도가 없어 올해로 읽는다).</summary>
        private static DateTime ParseTime(string line)
        {
            if (line.Length < 14) return DateTime.MinValue;

            string stamp = DateTime.Now.Year + "/" + line.Substring(0, 14);
            DateTime parsed;
            if (DateTime.TryParseExact(stamp, "yyyy/MM/dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                return parsed;

            return DateTime.MinValue;
        }

        /// <summary>CS2가 쓰는 중인 파일이라 공유 모드로 연다.</summary>
        private static IEnumerable<string> ReadLines()
        {
            string path = Cs2Paths.ConsoleLog;
            if (!File.Exists(path)) yield break;

            StreamReader reader;
            try
            {
                reader = new StreamReader(
                    new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                    Encoding.UTF8);
            }
            catch (IOException) { yield break; }

            using (reader)
            {
                string line;
                while ((line = reader.ReadLine()) != null) yield return line;
            }
        }
    }
}
