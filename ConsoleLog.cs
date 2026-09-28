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

        /// <summary>
        /// 지금 열려 있는 서버. 이미 닫혔거나 <paramref name="since"/> 이전 기록이면 null.
        /// 로그는 CS2를 껐다 켜도 남아 있어서, 시각을 안 보면 죽은 주소를 집어 든다.
        /// </summary>
        public static ServerInfo CurrentServer(DateTime since)
        {
            string last = null;
            foreach (string line in ReadLines())
            {
                if (ServerIdLine.IsMatch(line) || ShutdownLine.IsMatch(line)) last = line;
            }

            Match opened = last == null ? Match.Empty : ServerIdLine.Match(last);
            if (!opened.Success) return null;

            DateTime at = ParseTime(last);
            if (at < since) return null;

            return new ServerInfo { Address = opened.Groups[1].Value, OpenedAt = at };
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
