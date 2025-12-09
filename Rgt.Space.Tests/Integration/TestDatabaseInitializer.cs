using Npgsql;
using Rgt.Space.Core.Constants;

namespace Rgt.Space.Tests.Integration;

public static class TestDatabaseInitializer
{
    private static readonly string[] RequiredFiles =
    {
        "READMEs/SQL/PostgreSQL/Migrations/00-extensions.sql",
        "READMEs/SQL/PostgreSQL/Migrations/01-portal-schema.sql",
        "READMEs/SQL/PostgreSQL/Migrations/03-portal-routing-schema.sql"
    };

    public static async Task InitializeAsync(string connectionString)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        foreach (var filePath in RequiredFiles)
        {
            if (!System.IO.File.Exists(filePath))
            {
                // Fallback to searching relative to project root if running from bin folder
                var currentDir = Directory.GetCurrentDirectory();
                // Traverse up until we find READMEs or hit root
                var repoRoot = FindRepoRoot(currentDir);
                if (repoRoot == null)
                {
                    throw new FileNotFoundException($"Could not find SQL file: {filePath}");
                }

                var absolutePath = Path.Combine(repoRoot, filePath);
                if (!System.IO.File.Exists(absolutePath))
                {
                    throw new FileNotFoundException($"Could not find SQL file: {absolutePath}");
                }

                await ExecuteScriptAsync(conn, absolutePath);
            }
            else
            {
                await ExecuteScriptAsync(conn, filePath);
            }
        }
    }

    private static async Task ExecuteScriptAsync(NpgsqlConnection conn, string filePath)
    {
        var sql = await System.IO.File.ReadAllTextAsync(filePath);

        // Split by simple go-like separators if needed, but Npgsql usually handles multiple statements in one command
        // unless there are specific PSQL commands (like \c).
        // The provided SQL files seem to be standard SQL.

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static string? FindRepoRoot(string currentDir)
    {
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "READMEs")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
