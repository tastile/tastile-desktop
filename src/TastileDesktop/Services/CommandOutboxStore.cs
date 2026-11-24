using Microsoft.Data.Sqlite;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// SQLite-backed persistent queue for offline commands (NF04-D06,
/// v1/14 §1-1). Every command is stored with its idempotency key and
/// client-observed timestamp BEFORE any network attempt, so a crash or
/// disconnect never loses a user operation.
///
/// Invariants:
/// - `seq` (AUTOINCREMENT) defines replay order = enqueue order.
/// - `idempotency_key` is UNIQUE — a retried enqueue of the same logical
///   command cannot create a duplicate row.
/// - State transitions are one-way: pending → confirmed | conflict |
///   failed. Conflicts are never auto-resolved (no LWW).
/// </summary>
public sealed class CommandOutboxStore : IDisposable
{
    private readonly object _gate = new();
    private readonly string _dbPath;
    private SqliteConnection? _connection;

    public CommandOutboxStore(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
    }

    /// <summary>%LOCALAPPDATA%\Tastile\Commands\outbox.db</summary>
    public static string DefaultDbPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tastile",
            "Commands",
            "outbox.db");

    private SqliteConnection Connection
    {
        get
        {
            if (_connection is not null)
            {
                return _connection;
            }

            var directory = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SQLitePCL.Batteries_V2.Init();
            var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode=WAL;";
                command.ExecuteNonQuery();
            }
            InitializeSchema(connection);
            _connection = connection;
            return connection;
        }
    }

    private static void InitializeSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS pending_commands(
                seq INTEGER PRIMARY KEY AUTOINCREMENT,
                idempotency_key TEXT NOT NULL UNIQUE,
                kind TEXT NOT NULL,
                http_method TEXT NOT NULL,
                path TEXT NOT NULL,
                envelope_json TEXT NOT NULL,
                observed_at TEXT NOT NULL,
                state TEXT NOT NULL DEFAULT 'pending',
                attempt_count INTEGER NOT NULL DEFAULT 0,
                last_error TEXT,
                response_json TEXT,
                accepted_at TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            )
            """;
        command.ExecuteNonQuery();
        using var index = connection.CreateCommand();
        index.CommandText = "CREATE INDEX IF NOT EXISTS idx_pending_commands_state ON pending_commands(state, seq)";
        index.ExecuteNonQuery();
    }

    /// <summary>
    /// Persists a command before any send attempt. Returns the assigned
    /// `seq`, or null when the idempotency key already exists (the
    /// command was already queued — replay dedup).
    /// </summary>
    public long? Enqueue(OutboxEnqueueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = DateTimeOffset.UtcNow.ToString("O");
        lock (_gate)
        {
            try
            {
                using var command = Connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO pending_commands(
                        idempotency_key, kind, http_method, path, envelope_json,
                        observed_at, state, attempt_count, created_at, updated_at)
                    VALUES($key, $kind, $method, $path, $envelope, $observed, 'pending', 0, $now, $now)
                    """;
                command.Parameters.AddWithValue("$key", request.IdempotencyKey);
                command.Parameters.AddWithValue("$kind", request.Kind);
                command.Parameters.AddWithValue("$method", request.HttpMethod);
                command.Parameters.AddWithValue("$path", request.Path);
                command.Parameters.AddWithValue("$envelope", request.EnvelopeJson);
                command.Parameters.AddWithValue("$observed", request.ObservedAt);
                command.Parameters.AddWithValue("$now", now);
                command.ExecuteNonQuery();
                using var idCommand = Connection.CreateCommand();
                idCommand.CommandText = "SELECT last_insert_rowid()";
                return (long)(idCommand.ExecuteScalar() ?? 0L);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // SQLITE_CONSTRAINT (unique)
            {
                return null;
            }
        }
    }

    /// <summary>Pending commands in enqueue (replay) order.</summary>
    public IReadOnlyList<PendingCommandRecord> GetPending()
        => GetByState(OutboxCommandState.Pending);

    /// <summary>All commands, optionally filtered by state, in enqueue order.</summary>
    public IReadOnlyList<PendingCommandRecord> GetByState(string? state = null)
    {
        lock (_gate)
        {
            using var command = Connection.CreateCommand();
            command.CommandText = state is null
                ? "SELECT * FROM pending_commands ORDER BY seq"
                : "SELECT * FROM pending_commands WHERE state = $state ORDER BY seq";
            if (state is not null)
            {
                command.Parameters.AddWithValue("$state", state);
            }

            var results = new List<PendingCommandRecord>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(ReadRecord(reader));
            }
            return results;
        }
    }

    public PendingCommandRecord? GetBySeq(long seq)
        => GetByState().FirstOrDefault(r => r.Seq == seq);

    /// <summary>Increments attempt_count and stamps last_error; state stays pending.</summary>
    public void RecordAttempt(long seq, string? error)
    {
        Update(seq, """
            UPDATE pending_commands
            SET attempt_count = attempt_count + 1,
                last_error = $error,
                updated_at = $now
            WHERE seq = $seq
            """, ("$error", (object?)error ?? DBNull.Value));
    }

    /// <summary>Marks a command committed (server returned 2xx).</summary>
    public void MarkConfirmed(long seq, string responseJson, string? acceptedAt)
    {
        Update(seq, """
            UPDATE pending_commands
            SET state = 'confirmed',
                response_json = $response,
                accepted_at = $accepted,
                last_error = NULL,
                updated_at = $now
            WHERE seq = $seq
            """, ("$response", responseJson), ("$accepted", (object?)acceptedAt ?? DBNull.Value));
    }

    /// <summary>Marks a command conflicted — surfaced to the user, never auto-resolved.</summary>
    public void MarkConflict(long seq, string error)
    {
        Update(seq, """
            UPDATE pending_commands
            SET state = 'conflict',
                last_error = $error,
                updated_at = $now
            WHERE seq = $seq
            """, ("$error", error));
    }

    /// <summary>Marks a command permanently failed (non-conflict rejection).</summary>
    public void MarkFailed(long seq, string error)
    {
        Update(seq, """
            UPDATE pending_commands
            SET state = 'failed',
                last_error = $error,
                updated_at = $now
            WHERE seq = $seq
            """, ("$error", error));
    }

    /// <summary>Returns a conflicted/failed command to pending for manual retry.</summary>
    public void Requeue(long seq)
    {
        Update(seq, """
            UPDATE pending_commands
            SET state = 'pending',
                last_error = NULL,
                updated_at = $now
            WHERE seq = $seq AND state IN ('conflict', 'failed')
            """);
    }

    /// <summary>Drops confirmed history older than the given count is NOT done — audit retention is a follow-up.</summary>
    public int PendingCount()
    {
        lock (_gate)
        {
            using var command = Connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM pending_commands WHERE state = 'pending'";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public int CountByState(string state)
    {
        lock (_gate)
        {
            using var command = Connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM pending_commands WHERE state = $state";
            command.Parameters.AddWithValue("$state", state);
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    private void Update(long seq, string sql, params (string Name, object Value)[] parameters)
    {
        lock (_gate)
        {
            using var command = Connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$seq", seq);
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }
            command.ExecuteNonQuery();
        }
    }

    private static PendingCommandRecord ReadRecord(SqliteDataReader reader)
        => new(
            Seq: reader.GetInt64(0),
            IdempotencyKey: reader.GetString(1),
            Kind: reader.GetString(2),
            HttpMethod: reader.GetString(3),
            Path: reader.GetString(4),
            EnvelopeJson: reader.GetString(5),
            ObservedAt: reader.GetString(6),
            State: reader.GetString(7),
            AttemptCount: reader.GetInt32(8),
            LastError: reader.IsDBNull(9) ? null : reader.GetString(9),
            ResponseJson: reader.IsDBNull(10) ? null : reader.GetString(10),
            AcceptedAt: reader.IsDBNull(11) ? null : reader.GetString(11),
            CreatedAt: reader.GetString(12),
            UpdatedAt: reader.GetString(13));

    public void Dispose()
    {
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
