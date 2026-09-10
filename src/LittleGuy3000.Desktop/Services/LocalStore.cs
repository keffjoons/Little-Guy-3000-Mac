using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using LittleGuy3000.Core;

namespace LittleGuy3000.Desktop.Services;

internal sealed class UserSettings
{
    public string CodexExecutable { get; set; } = "";
    public bool ScreenEnabled { get; set; } = false;
    public bool ShowInScreenCapture { get; set; } = false;
    public bool VoiceEnabled { get; set; } = false;
    public string? MicrophoneName { get; set; }
    public bool VoiceCommandsEnabled { get; set; } = true;
    public bool AutoSpeak { get; set; } = false;
    public int SpeechRate { get; set; }
    public int SpeechPitch { get; set; }
    public string GoogleOAuthClientPath { get; set; } = "";
    public string NarrationVoice { get; set; } = "af_heart";
    public bool HistoryEnabled { get; set; } = true;
    public bool ReducedMotion { get; set; }
    public bool ShowCompanion { get; set; } = true;
    public bool ShiftHotkey { get; set; }
    public string ExcludedApps { get; set; } = "1Password\nKeePass\nKeePassXC\nBitwarden";
    public string? Model { get; set; }
    public string DetailEffort { get; set; } = "medium";
    public string ReplyTone { get; set; } = "Warm, natural and concise. Match the conversation's language. Avoid jargon and excessive emojis.";
    public IEnumerable<string> Exclusions => ExcludedApps.Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

internal sealed class LocalStore : IDisposable
{
    public string DirectoryPath { get; }
    private readonly SqliteConnection _database;
    public UserSettings Settings { get; }
    public LocalStore(string? directory = null)
    {
        DirectoryPath = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Brand.Identifier);
        Directory.CreateDirectory(DirectoryPath);
        string file = Path.Combine(DirectoryPath, "settings.json");
        try { Settings = File.Exists(file) ? JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(file)) ?? new() : new(); }
        catch (JsonException) { Settings = new(); }
        _database = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(DirectoryPath, "history.db"), Pooling = false }.ToString());
        _database.Open();
        using var command = _database.CreateCommand();
        command.CommandText = "PRAGMA secure_delete=ON; PRAGMA journal_mode=DELETE; CREATE TABLE IF NOT EXISTS Messages (Id INTEGER PRIMARY KEY, Created TEXT NOT NULL, Content BLOB NOT NULL, Pinned INTEGER NOT NULL DEFAULT 0);"; command.ExecuteNonQuery();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Messages') WHERE name='Pinned'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0) { command.CommandText = "ALTER TABLE Messages ADD COLUMN Pinned INTEGER NOT NULL DEFAULT 0"; command.ExecuteNonQuery(); }
    }
    public void SaveSettings()
    {
        var path = Path.Combine(DirectoryPath, "settings.json"); var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, path, true);
    }
    public void SaveExchange(string question, string answer, bool pin = false)
    {
        if (!Settings.HistoryEnabled && !pin) return;
        byte[] text = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { question, answer }));
        try
        {
            byte[] protectedText = ProtectedData.Protect(text, null, DataProtectionScope.CurrentUser);
            using var command = _database.CreateCommand(); command.CommandText = "INSERT INTO Messages(Created,Content,Pinned) VALUES($date,$content,$pin)";
            command.Parameters.AddWithValue("$date", DateTimeOffset.UtcNow.ToString("O")); command.Parameters.AddWithValue("$content", protectedText); command.Parameters.AddWithValue("$pin",pin?1:0); command.ExecuteNonQuery();
        }
        finally { CryptographicOperations.ZeroMemory(text); }
    }
    public IReadOnlyList<string> ReadRecent()
    {
        var result = new List<string>(); using var command = _database.CreateCommand(); command.CommandText = "SELECT Content FROM Messages ORDER BY Id DESC LIMIT 25";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            try
            {
                byte[] plain = ProtectedData.Unprotect((byte[])reader[0], null, DataProtectionScope.CurrentUser);
                try { using var json = JsonDocument.Parse(plain); result.Add(json.RootElement.GetProperty("question").GetString() + "\n\n" + json.RootElement.GetProperty("answer").GetString()); }
                finally { CryptographicOperations.ZeroMemory(plain); }
            }
            catch (CryptographicException) { result.Add("This entry cannot be decrypted by the current Windows account."); }
        }
        return result;
    }
    public void DeleteHistory() { using var command = _database.CreateCommand(); command.CommandText = "DELETE FROM Messages; VACUUM;"; command.ExecuteNonQuery(); }
    public IReadOnlyList<HistoryEntry> SearchHistory(string query, bool pinnedOnly)
    {
        var results=new List<HistoryEntry>(); using var command=_database.CreateCommand();
        command.CommandText="SELECT Id,Created,Content,Pinned FROM Messages WHERE ($p=0 OR Pinned=1) ORDER BY Id DESC LIMIT 500";
        command.Parameters.AddWithValue("$p",pinnedOnly?1:0); using var reader=command.ExecuteReader();
        while(reader.Read())
        {
            try
            {
                byte[] plain=ProtectedData.Unprotect((byte[])reader[2],null,DataProtectionScope.CurrentUser);
                try
                {
                    using var json=JsonDocument.Parse(plain);
                    string question=json.RootElement.GetProperty("question").GetString()??"", answer=json.RootElement.GetProperty("answer").GetString()??"";
                    if(string.IsNullOrWhiteSpace(query) || question.Contains(query,StringComparison.OrdinalIgnoreCase) || answer.Contains(query,StringComparison.OrdinalIgnoreCase))
                        results.Add(new(reader.GetInt64(0),reader.GetString(1),question,answer,reader.GetInt32(3)!=0));
                }
                finally { CryptographicOperations.ZeroMemory(plain); }
            }
            catch(CryptographicException) { }
        }
        return results;
    }
    public void SetPinned(long id,bool pinned)
    {
        using var command=_database.CreateCommand(); command.CommandText="UPDATE Messages SET Pinned=$pin WHERE Id=$id";
        command.Parameters.AddWithValue("$pin",pinned?1:0);command.Parameters.AddWithValue("$id",id);command.ExecuteNonQuery();
    }
    public void PinExchange(string question,string answer)
    {
        var existing=SearchHistory("",false).FirstOrDefault(e=>e.Question==question&&e.Answer==answer);
        if(existing is not null) SetPinned(existing.Id,true); else SaveExchange(question,answer,true);
    }
    public void Dispose() => _database.Dispose();
}
internal sealed record HistoryEntry(long Id,string Created,string Question,string Answer,bool Pinned);
