using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace TeampptAddin
{
    public class RemoteAssetCache
    {
        private static readonly HttpClient Http = new HttpClient();
        private readonly string _baseUrl;
        private readonly string _anonKey;

        public RemoteAssetCache(string baseUrl, string anonKey)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _anonKey = anonKey;
        }

        public Task<string> GetThumbAsync(string remoteThumb) => GetAsync(remoteThumb, "thumb");
        public Task<string> GetPptxAsync(string remoteFile) => GetAsync(remoteFile, "pptx");

        private async Task<string> GetAsync(string remotePath, string subdir)
        {
            var fileName = Path.GetFileName(remotePath);
            var localDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeampptAddin", "cache", subdir);
            Directory.CreateDirectory(localDir);
            var localPath = Path.Combine(localDir, fileName);
            if (File.Exists(localPath)) return localPath;

            var url = $"{_baseUrl}/storage/v1/object/public/{remotePath}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("apikey", _anonKey);
            var resp = await Http.SendAsync(req).ConfigureAwait(false);
            Logger.Log($"[RemoteCache] GET {remotePath}: HTTP {(int)resp.StatusCode}");
            resp.EnsureSuccessStatusCode();
            File.WriteAllBytes(localPath, await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
            return localPath;
        }
    }
}
