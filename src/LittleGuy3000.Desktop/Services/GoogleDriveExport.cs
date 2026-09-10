using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LittleGuy3000.Core;

namespace LittleGuy3000.Desktop.Services;

internal sealed class GoogleDriveExport(string directory, HttpClient? http = null)
{
    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly HttpClient _http = http ?? SharedHttp;
    private string TokenFile => Path.Combine(directory, "google-drive-token.bin");
    public bool Connected => File.Exists(TokenFile);
    private sealed record Grant(string RefreshToken, string ClientId);
    private static (string Id, string Secret) Client(string path)
    {
        if (!File.Exists(path)) throw new InvalidOperationException("Choose your Google Desktop OAuth client JSON in Settings > Research and ChatGPT first. See GOOGLE_DRIVE_SETUP.md for the one-time setup.");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("installed", out var installed)) throw new InvalidDataException("Choose a Desktop app OAuth client JSON, not a web client or service account.");
        string id = installed.GetProperty("client_id").GetString()!;
        if (!id.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal)) throw new InvalidDataException("The Google OAuth client ID is invalid.");
        return (id, installed.TryGetProperty("client_secret", out var secret) ? secret.GetString() ?? "" : "");
    }
    private static string B64(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public async Task ConnectAsync(string clientPath, Action<Uri> openBrowser, CancellationToken token)
    {
        var client = Client(clientPath);
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        int port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        string redirect = $"http://127.0.0.1:{port}/", state = B64(RandomNumberGenerator.GetBytes(32)), verifier = B64(RandomNumberGenerator.GetBytes(32));
        using var listener = new HttpListener(); listener.Prefixes.Add(redirect); listener.Start();
        var values = new Dictionary<string, string> { ["client_id"] = client.Id, ["redirect_uri"] = redirect, ["response_type"] = "code", ["scope"] = "https://www.googleapis.com/auth/drive.file", ["state"] = state, ["code_challenge"] = B64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))), ["code_challenge_method"] = "S256", ["access_type"] = "offline", ["prompt"] = "consent" };
        openBrowser(new Uri("https://accounts.google.com/o/oauth2/v2/auth?" + string.Join('&', values.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)))));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(5));
        HttpListenerContext callback;
        while (true)
        {
            callback = await listener.GetContextAsync().WaitAsync(timeout.Token);
            if (callback.Request.QueryString["state"] == state) break;
            callback.Response.StatusCode = 400; callback.Response.Close();
        }
        string? code = callback.Request.QueryString["code"];
        byte[] message = Encoding.UTF8.GetBytes("You can return to Little Guy 3000. This sign-in window can be closed.");
        callback.Response.ContentType = "text/plain; charset=utf-8";
        await callback.Response.OutputStream.WriteAsync(message, timeout.Token); callback.Response.Close();
        if (string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Google sign-in wasn't completed. Nothing was connected.");
        using var grant = await TokenAsync(new() { ["client_id"] = client.Id, ["client_secret"] = client.Secret, ["code"] = code, ["redirect_uri"] = redirect, ["code_verifier"] = verifier, ["grant_type"] = "authorization_code" }, timeout.Token);
        if (!grant.RootElement.TryGetProperty("refresh_token", out var refresh)) throw new InvalidOperationException("Google didn't provide offline access. Connect again and approve the requested access.");
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(new Grant(refresh.GetString()!, client.Id));
        try
        {
            byte[] protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(TokenFile + ".tmp", protectedBytes, token); File.Move(TokenFile + ".tmp", TokenFile, true);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    private async Task<JsonDocument> TokenAsync(Dictionary<string, string> values, CancellationToken token)
    {
        using var body = new FormUrlEncodedContent(values);
        using var response = await _http.PostAsync("https://oauth2.googleapis.com/token", body, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Google authorization failed. Reconnect Google Drive in Settings.");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
    }
    private async Task<string> AccessAsync(string clientPath, CancellationToken token)
    {
        if (!Connected) throw new InvalidOperationException("Connect Google Drive in Settings before saving research.");
        var client = Client(clientPath);
        byte[] plain = ProtectedData.Unprotect(await File.ReadAllBytesAsync(TokenFile, token), null, DataProtectionScope.CurrentUser);
        Grant saved;
        try { saved = JsonSerializer.Deserialize<Grant>(plain)!; }
        finally { CryptographicOperations.ZeroMemory(plain); }
        if (saved.ClientId != client.Id) throw new InvalidOperationException("The Google client changed. Connect Google Drive again.");
        using var access = await TokenAsync(new() { ["client_id"] = client.Id, ["client_secret"] = client.Secret, ["refresh_token"] = saved.RefreshToken, ["grant_type"] = "refresh_token" }, token);
        return access.RootElement.GetProperty("access_token").GetString()!;
    }
    public async Task<Uri> UploadAsync(ResearchReport report, bool sheet, string clientPath, CancellationToken token)
    {
        report.Validate(); string access = await AccessAsync(clientPath, token);
        using var multipart = new MultipartContent("related");
        multipart.Add(new StringContent(JsonSerializer.Serialize(new { name = report.Title, mimeType = sheet ? "application/vnd.google-apps.spreadsheet" : "application/vnd.google-apps.document" }), Encoding.UTF8, "application/json"));
        multipart.Add(new StringContent(sheet ? report.ToCsv() : report.ToHtml(), Encoding.UTF8, sheet ? "text/csv" : "text/html"));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id") { Content = multipart };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        using var response = await _http.SendAsync(request, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Google Drive couldn't save the report (HTTP {(int)response.StatusCode}). Your report remains in Little Guy; reconnect or try Save again.");
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        string id = result.RootElement.GetProperty("id").GetString()!;
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-zA-Z0-9_-]+$")) throw new InvalidDataException("Google returned an invalid document ID.");
        return new Uri($"https://docs.google.com/{(sheet ? "spreadsheets" : "document")}/d/{id}/edit");
    }
    public void Disconnect() { if (File.Exists(TokenFile)) File.Delete(TokenFile); }
}
