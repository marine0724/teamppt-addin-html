using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>
    /// 자동 업데이트. 설계: docs/superpowers/specs/2026-06-24-auto-update.md
    ///
    /// 핵심 판단:
    /// - COM 재등록(RegAsm/UAC)은 업데이트마다 불필요. GUID·설치경로가 고정이면
    ///   레지스트리 codebase가 같은 경로를 계속 가리키므로 파일만 덮어쓰면 된다.
    /// - 실행 중 DLL은 잠기므로 애드인 자신은 못 바꾼다 → PPT 종료 후 updater.bat이 스왑.
    ///
    /// 동작: 시작 시 version.json(GitHub Pages)을 GET → 원격>로컬이면 zip(Releases)을
    /// staging 폴더에 받아 압축 해제하고 UpdateReady 이벤트를 올린다(논블로킹·실패 무해).
    /// </summary>
    public class UpdateService
    {
        // GitHub Pages에 올라가는 매니페스트. (repo: marine0724/teamppt-addin-html)
        // ⚠ Pages가 docs/를 발행하는지, 경로가 /version.json인지 /docs/version.json인지 확인 필요.
        private const string ManifestUrl = "https://marine0724.github.io/teamppt-addin-html/version.json";

        private static readonly HttpClient Http;
        static UpdateService()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        /// <summary>업데이트 준비 완료(staging에 압축 해제됨). 인자: (버전, 릴리스 노트).</summary>
        public event Action<string, string> UpdateReady;

        public static string AppDir =>
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static string StagingRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TeampptAddin", "update-staging");

        public static string MarkerPath => Path.Combine(StagingRoot, "pending-update.json");

        public static Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>예외를 삼키는 백그라운드 진입점. OnStartupComplete 등에서 호출.</summary>
        public void CheckInBackground()
        {
            Task.Run(async () =>
            {
                try { await CheckAndStageAsync().ConfigureAwait(false); }
                catch (Exception ex) { Logger.Log($"[Update] 확인 실패(무해): {ex.Message}"); }
            });
        }

        public async Task CheckAndStageAsync()
        {
            var json = await Http.GetStringAsync(ManifestUrl).ConfigureAwait(false);
            var manifest = JObject.Parse(json);
            var remoteStr = (string)manifest["version"];
            var zipUrl = (string)manifest["zipUrl"];
            var notes = (string)manifest["notes"] ?? "";

            if (string.IsNullOrEmpty(remoteStr) || string.IsNullOrEmpty(zipUrl))
            {
                Logger.Log("[Update] 매니페스트에 version/zipUrl 없음");
                return;
            }

            if (!Version.TryParse(remoteStr, out var remote))
            {
                Logger.Log($"[Update] 버전 파싱 실패: {remoteStr}");
                return;
            }

            var local = CurrentVersion;
            Logger.Log($"[Update] 로컬={local} 원격={remote}");
            if (remote <= local) return;

            var stageDir = Path.Combine(StagingRoot, remote.ToString());
            var readyFlag = Path.Combine(stageDir, ".ready");

            // 멱등: 이미 받아둔 버전이면 다운로드 스킵.
            if (!File.Exists(readyFlag))
            {
                Directory.CreateDirectory(StagingRoot);
                if (Directory.Exists(stageDir)) Directory.Delete(stageDir, true);
                Directory.CreateDirectory(stageDir);

                var zipPath = Path.Combine(StagingRoot, $"{remote}.zip");
                var bytes = await Http.GetByteArrayAsync(zipUrl).ConfigureAwait(false);
                File.WriteAllBytes(zipPath, bytes);
                Logger.Log($"[Update] zip 다운로드 완료: {bytes.Length} bytes");

                ZipFile.ExtractToDirectory(zipPath, stageDir);
                File.Delete(zipPath);
                File.WriteAllText(readyFlag, DateTime.UtcNow.ToString("o"));
                Logger.Log($"[Update] staging 준비 완료: {stageDir}");
            }

            WriteMarker(remote.ToString(), notes, stageDir);
            UpdateReady?.Invoke(remote.ToString(), notes);
        }

        private static void WriteMarker(string version, string notes, string stageDir)
        {
            var marker = new JObject
            {
                ["version"] = version,
                ["notes"] = notes,
                ["stageDir"] = stageDir,
                ["appDir"] = AppDir
            };
            File.WriteAllText(MarkerPath, marker.ToString());
        }

        /// <summary>
        /// updater.bat을 detached로 실행한다. updater가 PowerPoint 종료를 기다렸다가
        /// staging을 app 폴더에 덮어쓰고 재실행한다. 이 호출 후 PowerPoint를 종료해야 한다.
        /// </summary>
        public static bool LaunchUpdater()
        {
            try
            {
                var bat = Path.Combine(AppDir, "updater.bat");
                if (!File.Exists(bat))
                {
                    Logger.Log($"[Update] updater.bat 없음: {bat}");
                    return false;
                }

                if (!File.Exists(MarkerPath))
                {
                    Logger.Log("[Update] 마커 없음 — 적용할 업데이트 없음");
                    return false;
                }

                var marker = JObject.Parse(File.ReadAllText(MarkerPath));
                var stageDir = (string)marker["stageDir"];
                var appDir = (string)marker["appDir"] ?? AppDir;

                var psi = new ProcessStartInfo
                {
                    FileName = bat,
                    Arguments = $"\"{stageDir}\" \"{appDir}\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                };
                Process.Start(psi);
                Logger.Log("[Update] updater.bat 실행됨");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"[Update] updater 실행 실패: {ex}");
                return false;
            }
        }
    }
}
