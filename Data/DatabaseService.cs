using Jarvis.Models;
using Microsoft.Data.Sqlite;
using System.IO;

namespace Jarvis.Data;

public sealed class DatabaseService
{
    private readonly string _dbPath;
    private string ConnectionString => $"Data Source={_dbPath}";

    public DatabaseService()
    {
        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Jarvis");
        Directory.CreateDirectory(appData);
        _dbPath = Path.Combine(appData, "jarvis.db");
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        string sql = """
        CREATE TABLE IF NOT EXISTS ConversationHistory (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Role TEXT NOT NULL,
            Content TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS CommandHistory (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Source TEXT NOT NULL,
            CommandText TEXT NOT NULL,
            ResultText TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Reminders (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Text TEXT NOT NULL,
            DueAt TEXT NOT NULL,
            IsCompleted INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS UserPreferences (
            Key TEXT PRIMARY KEY,
            Value TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS ApiSecrets (
            Name TEXT PRIMARY KEY,
            Value TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS AssistantMemory (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Key TEXT NOT NULL,
            Value TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS UserProfile (
            Id INTEGER PRIMARY KEY CHECK (Id = 1),
            DisplayName TEXT NOT NULL,
            AddressForm TEXT NOT NULL,
            IsOnboardingComplete INTEGER NOT NULL DEFAULT 0,
            UpdatedAt TEXT NOT NULL
        );
        """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<string> GetApiSecretAsync(string name)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Value
            FROM ApiSecrets
            WHERE Name = $name
            """;
        command.Parameters.AddWithValue("$name", name);

        return (await command.ExecuteScalarAsync())?.ToString() ?? "";
    }

    public async Task SaveApiSecretAsync(string name, string value)
    {
        await ExecuteAsync(
            """
            INSERT INTO ApiSecrets(Name, Value, UpdatedAt)
            VALUES ($name, $value, $updatedAt)
            ON CONFLICT(Name) DO UPDATE SET
                Value = excluded.Value,
                UpdatedAt = excluded.UpdatedAt
            """,
            ("$name", name),
            ("$value", value),
            ("$updatedAt", DateTime.Now.ToString("O")));
    }

    public async Task AddConversationMessageAsync(string role, string content)
    {
        await ExecuteAsync(
            "INSERT INTO ConversationHistory(Role, Content, CreatedAt) VALUES ($role, $content, $createdAt)",
            ("$role", role), ("$content", content), ("$createdAt", DateTime.Now.ToString("O")));
    }

    public async Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(int count)
    {
        var messages = new List<ChatMessage>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Role, Content, CreatedAt
            FROM ConversationHistory
            ORDER BY Id DESC
            LIMIT $count
            """;
        command.Parameters.AddWithValue("$count", count);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(new ChatMessage
            {
                Id = reader.GetInt64(0),
                Role = reader.GetString(1),
                Content = reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3))
            });
        }

        messages.Reverse();
        return messages;
    }

    public async Task AddCommandHistoryAsync(string source, string commandText, string resultText)
    {
        await ExecuteAsync(
            "INSERT INTO CommandHistory(Source, CommandText, ResultText, CreatedAt) VALUES ($source, $commandText, $resultText, $createdAt)",
            ("$source", source), ("$commandText", commandText), ("$resultText", resultText), ("$createdAt", DateTime.Now.ToString("O")));
    }

    public async Task AddMemoryAsync(string key, string value)
    {
        await ExecuteAsync(
            "INSERT INTO AssistantMemory(Key, Value, CreatedAt) VALUES ($key, $value, $createdAt)",
            ("$key", key), ("$value", value), ("$createdAt", DateTime.Now.ToString("O")));
    }

    public async Task<IReadOnlyList<string>> SearchMemoryAsync(string term, int count = 5)
    {
        var results = new List<string>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Value
            FROM AssistantMemory
            WHERE Value LIKE $term OR Key LIKE $term
            ORDER BY Id DESC
            LIMIT $count
            """;
        command.Parameters.AddWithValue("$term", $"%{term}%");
        command.Parameters.AddWithValue("$count", count);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(reader.GetString(0));

        return results;
    }

    public async Task<long> AddReminderAsync(string text, DateTime dueAt)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Reminders(Text, DueAt, IsCompleted, CreatedAt)
            VALUES ($text, $dueAt, 0, $createdAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$dueAt", dueAt.ToString("O"));
        command.Parameters.AddWithValue("$createdAt", DateTime.Now.ToString("O"));
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    public async Task<IReadOnlyList<Reminder>> GetActiveRemindersAsync()
    {
        var reminders = new List<Reminder>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Text, DueAt, IsCompleted, CreatedAt
            FROM Reminders
            WHERE IsCompleted = 0
            ORDER BY DueAt
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            reminders.Add(new Reminder
            {
                Id = reader.GetInt64(0),
                Text = reader.GetString(1),
                DueAt = DateTime.Parse(reader.GetString(2)),
                IsCompleted = reader.GetInt32(3) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(4))
            });
        }

        return reminders;
    }

    public async Task CompleteReminderAsync(long id)
    {
        await ExecuteAsync("UPDATE Reminders SET IsCompleted = 1 WHERE Id = $id", ("$id", id));
    }

    public async Task<UserProfile?> GetUserProfileAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DisplayName, AddressForm, IsOnboardingComplete
            FROM UserProfile
            WHERE Id = 1
            """;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new UserProfile
        {
            DisplayName = reader.GetString(0),
            AddressForm = reader.GetString(1),
            IsOnboardingComplete = reader.GetInt32(2) == 1
        };
    }

    public async Task SaveUserProfileAsync(UserProfile profile)
    {
        await ExecuteAsync(
            """
            INSERT INTO UserProfile(Id, DisplayName, AddressForm, IsOnboardingComplete, UpdatedAt)
            VALUES (1, $displayName, $addressForm, $isComplete, $updatedAt)
            ON CONFLICT(Id) DO UPDATE SET
                DisplayName = excluded.DisplayName,
                AddressForm = excluded.AddressForm,
                IsOnboardingComplete = excluded.IsOnboardingComplete,
                UpdatedAt = excluded.UpdatedAt
            """,
            ("$displayName", profile.DisplayName),
            ("$addressForm", profile.AddressForm),
            ("$isComplete", profile.IsOnboardingComplete ? 1 : 0),
            ("$updatedAt", DateTime.Now.ToString("O")));
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }
}
