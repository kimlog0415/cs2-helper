using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace CS2Helper
{
    [DataContract]
    internal sealed class Settings
    {
        [DataMember(Name = "mode")] public string Mode { get; set; }
        [DataMember(Name = "map")] public string Map { get; set; }
        [DataMember(Name = "bots")] public int Bots { get; set; }
        [DataMember(Name = "level")] public string Level { get; set; }
        [DataMember(Name = "team")] public string Team { get; set; }

        /// <summary>자동 탐지가 실패해 사용자가 직접 고른 CS2 설치 폴더.</summary>
        [DataMember(Name = "installDir")] public string InstallDir { get; set; }

        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CS2Helper");
                return Path.Combine(dir, "settings.json");
            }
        }

        public static Settings Load()
        {
            try
            {
                using (var stream = File.OpenRead(FilePath))
                    return (Settings)Serializer.ReadObject(stream) ?? new Settings();
            }
            catch (Exception)
            {
                return new Settings();   // 없거나 깨졌으면 기본값으로 시작
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                using (var stream = File.Create(FilePath))
                    Serializer.WriteObject(stream, this);
            }
            catch (Exception)
            {
                // 설정 저장 실패로 앱이 멈출 이유는 없다
            }
        }

        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(typeof(Settings));
    }
}
