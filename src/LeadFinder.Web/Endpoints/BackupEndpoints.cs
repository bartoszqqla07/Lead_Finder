using LeadFinder.Web.Data;
using LeadFinder.Web.Search;
using LeadFinder.Web.Settings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LeadFinder.Web.Endpoints;

/// <summary>
/// Kopia zapasowa całej bazy (leady, notatki, historia, ustawienia z kluczem API) – do przeniesienia
/// aplikacji na inny komputer. Baza celowo nie trafia do gita: to dane firm i klucz API.
/// </summary>
public static class BackupEndpoints
{
    private const long MaxBackupBytes = 500L * 1024 * 1024;
    private const string BackupContentType = "application/octet-stream";

    public static void MapBackupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/backup");
        group.MapGet("", DownloadAsync);
        group.MapPost("/restore", RestoreAsync);
    }

    /// <summary>
    /// Spójna kopia przez <c>VACUUM INTO</c> – działa także wtedy, gdy aplikacja w tym czasie zapisuje do bazy,
    /// i zawiera wszystko w jednym pliku (bez osobnych plików -wal/-shm).
    /// </summary>
    private static async Task<IResult> DownloadAsync(AppPaths paths, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"leadfinder-backup-{Guid.NewGuid():N}.db");
        try
        {
            await VacuumIntoAsync(paths.DatabasePath, tempPath, ct);
            var bytes = await File.ReadAllBytesAsync(tempPath, ct);
            return Results.File(bytes, BackupContentType, $"leadfinder-kopia-{DateTime.Now:yyyy-MM-dd}.db");
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    /// <summary>
    /// Zastępuje bazę plikiem kopii. Przed podmianą sprawdza, czy plik to baza LeadFindera, i zapisuje
    /// obecne dane obok jako <c>leadfinder.przed-przywroceniem-*.db</c> – pomyłkę da się cofnąć.
    /// </summary>
    /// <remarks>
    /// Wymaga Content-Type application/octet-stream: taki POST wymusza preflight CORS, więc obca strona
    /// otwarta w przeglądarce nie podmieni bazy żądaniem do localhost.
    /// </remarks>
    private static async Task<IResult> RestoreAsync(
        HttpContext context, AppPaths paths, SearchJobRunner searchRunner, IServiceScopeFactory scopeFactory, CancellationToken ct)
    {
        if (!string.Equals(context.Request.ContentType, BackupContentType, StringComparison.OrdinalIgnoreCase))
            return Problem(StatusCodes.Status415UnsupportedMediaType, "Wyślij plik kopii jako application/octet-stream.");

        if (searchRunner.Current is { IsFinished: false })
            return Problem(StatusCodes.Status409Conflict, "Trwa wyszukiwanie – poczekaj, aż się skończy, i spróbuj ponownie.");

        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } sizeLimit)
            sizeLimit.MaxRequestBodySize = MaxBackupBytes;

        var uploadPath = Path.Combine(Path.GetTempPath(), $"leadfinder-restore-{Guid.NewGuid():N}.db");
        try
        {
            await using (var file = File.Create(uploadPath))
                await context.Request.Body.CopyToAsync(file, ct);

            if (await ValidateBackupAsync(uploadPath, ct) is { } error)
                return Problem(StatusCodes.Status400BadRequest, error);

            var safetyCopy = Path.Combine(paths.DataDirectory, $"leadfinder.przed-przywroceniem-{DateTime.Now:yyyyMMdd-HHmmss}.db");
            await VacuumIntoAsync(paths.DatabasePath, safetyCopy, ct);

            // Pula połączeń trzyma otwarty plik – bez tego Windows nie pozwoli go nadpisać.
            SqliteConnection.ClearAllPools();
            File.Copy(uploadPath, paths.DatabasePath, overwrite: true);
            TryDelete(paths.DatabasePath + "-wal");
            TryDelete(paths.DatabasePath + "-shm");

            // Kopia mogła powstać w starszej wersji aplikacji – dociągamy brakujące migracje.
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LeadFinderDbContext>();
            await db.Database.MigrateAsync(ct);
            var leadCount = await db.Leads.CountAsync(ct);

            return Results.Ok(new { leadCount, safetyCopy = Path.GetFileName(safetyCopy) });
        }
        finally
        {
            TryDelete(uploadPath);
        }
    }

    /// <summary>Null, gdy plik to poprawna baza LeadFindera; inaczej komunikat dla użytkownika.</summary>
    private static async Task<string?> ValidateBackupAsync(string path, CancellationToken ct)
    {
        try
        {
            await using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            await connection.OpenAsync(ct);

            await using var check = connection.CreateCommand();
            check.CommandText = "PRAGMA integrity_check";
            if (await check.ExecuteScalarAsync(ct) is not "ok")
                return "Plik kopii jest uszkodzony.";

            await using var tables = connection.CreateCommand();
            tables.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Leads', '__EFMigrationsHistory')";
            return await tables.ExecuteScalarAsync(ct) is 2L ? null : "To nie jest kopia danych LeadFindera.";
        }
        catch (SqliteException)
        {
            return "To nie jest kopia danych LeadFindera (plik nie jest bazą SQLite).";
        }
    }

    private static async Task VacuumIntoAsync(string sourcePath, string targetPath, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = sourcePath, Pooling = false }.ToString());
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $target";
        command.Parameters.AddWithValue("$target", targetPath);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static IResult Problem(int status, string detail) => Results.Problem(detail: detail, statusCode: status);

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Plik tymczasowy – system i tak go kiedyś posprząta.
        }
    }
}
