using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LittleGuy3000.Core;
using LittleGuy3000.Desktop.Services;

namespace LittleGuy3000.Desktop;

public sealed partial class MainWindow
{
    private sealed class DriveFixture : HttpMessageHandler
    {
        public List<string> Bodies = [];
        public string? CodeVerifier;
        public bool FailUpload;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (request.RequestUri!.Host == "oauth2.googleapis.com")
            {
                var pairs = body.Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
                if (pairs.TryGetValue("code_verifier", out var verifier)) CodeVerifier = verifier;
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"fixture-access\",\"refresh_token\":\"fixture-refresh\"}") };
            }
            if (request.RequestUri.Host != "www.googleapis.com" || request.RequestUri.AbsolutePath != "/upload/drive/v3/files" || request.Headers.Authorization?.Parameter != "fixture-access") throw new Exception("Unexpected Drive request");
            Bodies.Add(body);
            return new(FailUpload ? HttpStatusCode.Forbidden : HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"fixture-document\"}") };
        }
    }
    private async Task RunDriveCheckAsync(string directory)
    {
        var checks = new Dictionary<string, object>();
        try
        {
            string clientPath = Path.Combine(directory, "fixture-client.json");
            await File.WriteAllTextAsync(clientPath, "{\"installed\":{\"client_id\":\"fixture.apps.googleusercontent.com\",\"client_secret\":\"fixture-only\"}}");
            var handler = new DriveFixture(); using var http = new HttpClient(handler); var drive = new GoogleDriveExport(directory, http);
            var opened = new TaskCompletionSource<Uri>(); using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var connect = drive.ConnectAsync(clientPath, uri => opened.TrySetResult(uri), cancel.Token);
            var auth = await opened.Task;
            var query = auth.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
            checks["minimalDriveScope"] = query["scope"] == "https://www.googleapis.com/auth/drive.file";
            checks["systemBrowserAndPkce"] = auth.Host == "accounts.google.com" && query["code_challenge_method"] == "S256";
            using var callback = new HttpClient();
            using var rejected = await callback.GetAsync(query["redirect_uri"] + "?state=wrong&code=fake", cancel.Token);
            checks["wrongStateRejected"] = rejected.StatusCode == HttpStatusCode.BadRequest && !connect.IsCompleted;
            using var accepted = await callback.GetAsync(query["redirect_uri"] + "?state=" + query["state"] + "&code=fixture-code", cancel.Token);
            await connect;
            string actualChallenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(handler.CodeVerifier!))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            checks["pkceVerifierMatches"] = actualChallenge == query["code_challenge"];
            checks["refreshTokenProtected"] = drive.Connected && !Encoding.UTF8.GetString(await File.ReadAllBytesAsync(Path.Combine(directory, "google-drive-token.bin"))).Contains("fixture-refresh");
            var report = new ResearchReport("Test report", "A short summary", [new("Topic", "Verified sample finding", "Sample", "https://example.com/source")]);
            var doc = await drive.UploadAsync(report, false, clientPath, cancel.Token);
            var sheet = await drive.UploadAsync(report, true, clientPath, cancel.Token);
            checks["docConversionAndContent"] = doc.AbsolutePath == "/document/d/fixture-document/edit" && handler.Bodies[0].Contains("application/vnd.google-apps.document") && handler.Bodies[0].Contains("Verified sample finding");
            checks["sheetConversionAndSources"] = sheet.AbsolutePath == "/spreadsheets/d/fixture-document/edit" && handler.Bodies[1].Contains("text/csv") && handler.Bodies[1].Contains("https://example.com/source");
            handler.FailUpload = true;
            try { await drive.UploadAsync(report, false, clientPath, cancel.Token); checks["failedUploadCannotClaimSuccess"] = false; }
            catch (InvalidOperationException) { checks["failedUploadCannotClaimSuccess"] = true; }
            drive.Disconnect(); checks["disconnectClearsLocalToken"] = !drive.Connected;
        }
        catch (Exception ex) { checks["error"] = ex.ToString(); }
        await File.WriteAllTextAsync(Path.Combine(directory, "drive-report.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
        await QuitAsync();
    }
}
