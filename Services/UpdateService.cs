using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace CheckHash.Services;

public class UpdateService
{
    // URL GitHub Repo
    private const string RepoUrl = "https://github.com/poli0981/CheckHash-Multiflatform";
    
    private UpdateManager? _manager;
    private readonly HttpClient _httpClient;

    public string CurrentVersion => _manager?.CurrentVersion?.ToString() ?? "0.0.0 (Debug)";

    public UpdateService()
    {
        _httpClient = new HttpClient();
        // GitHub API yêu cầu User-Agent
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CheckHash", "1.0"));

        try 
        {
            _manager = new UpdateManager(new GithubSource(RepoUrl, null, false));
        }
        catch 
        { 
            // Bỏ qua lỗi nếu chạy local debug chưa pack
        }
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool allowPrerelease)
    {
        if (_manager == null) return null;

        if (allowPrerelease)
        {
             _manager = new UpdateManager(new GithubSource(RepoUrl, null, true));
        }
        else
        {
             _manager = new UpdateManager(new GithubSource(RepoUrl, null, false));
        }

        return await _manager.CheckForUpdatesAsync();
    }

    public async Task DownloadAndInstallAsync(UpdateInfo info)
    {
        if (_manager == null) return;

        await _manager.DownloadUpdatesAsync(info);
        _manager.ApplyUpdatesAndRestart(info);
    }

    // MỚI: Lấy Release Notes từ GitHub API
    public async Task<string> GetReleaseNotesAsync(string version)
    {
        try
        {
            // Chuyển đổi URL repo thành URL API
            // Từ: https://github.com/user/repo
            // Thành: https://api.github.com/repos/user/repo/releases/tags/{version}
            
            var apiUrlBase = RepoUrl.Replace("https://github.com/", "https://api.github.com/repos/");
            
            // Thử với version gốc (ví dụ: 1.0.1)
            var url = $"{apiUrlBase}/releases/tags/{version}";
            
            // Nếu GitHub dùng tag có chữ 'v' (ví dụ v1.0.1), logic này có thể cần điều chỉnh
            // Tuy nhiên Velopack thường khuyến nghị tag trùng version SemVer.
            
            var response = await _httpClient.GetAsync(url);
            
            // Nếu 404, thử thêm 'v' vào trước (fallback common case)
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                url = $"{apiUrlBase}/releases/tags/v{version}";
                response = await _httpClient.GetAsync(url);
            }

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var node = JsonNode.Parse(json);
                return node?["body"]?.ToString() ?? "Không có nội dung chi tiết.";
            }
        }
        catch
        {
            // Ignore network errors
        }

        return "Không thể tải thông tin chi tiết từ GitHub.";
    }
}