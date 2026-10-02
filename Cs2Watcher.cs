using System;
using System.Diagnostics;

namespace CS2PracticeHost
{
    internal enum Cs2State
    {
        /// <summary>CS2가 꺼져 있다.</summary>
        Off,

        /// <summary>앱이 켠 CS2가 아니라 로그가 없다. 서버가 열려 있어도 주소를 읽을 수 없다.</summary>
        NoLog,

        /// <summary>CS2는 돌지만 열린 서버가 없다 (메인 화면이거나 방장이 나갔다).</summary>
        NoServer,

        ServerOpen,
    }

    internal sealed class Cs2Status
    {
        public Cs2State State;

        /// <summary><see cref="Cs2State.ServerOpen"/> 일 때만 채워진다.</summary>
        public ServerInfo Server;
    }

    /// <summary>
    /// 지금 CS2가 어떤 상태인지 한곳에서 판단한다.
    /// 상태 표시줄과 주소 복사가 같은 순서로 따져야 해서, 그 순서를 두 군데 두지 않는다.
    /// </summary>
    internal static class Cs2Watcher
    {
        public static Cs2Status Read()
        {
            Process cs2 = Cs2Process.Find();
            if (cs2 == null) return new Cs2Status { State = Cs2State.Off };
            if (!Cs2Process.HasCondebug(cs2)) return new Cs2Status { State = Cs2State.NoLog };

            ServerInfo server = ServerSince(cs2);
            return server == null
                ? new Cs2Status { State = Cs2State.NoServer }
                : new Cs2Status { State = Cs2State.ServerOpen, Server = server };
        }

        /// <summary>이번 CS2 세션에서 열린 서버만 인정한다. 로그는 껐다 켜도 남아 죽은 주소를 집게 된다.</summary>
        private static ServerInfo ServerSince(Process cs2)
        {
            try { return ConsoleLog.CurrentServer(cs2.StartTime.AddSeconds(-5)); }
            catch (Exception) { return null; }
        }
    }
}
