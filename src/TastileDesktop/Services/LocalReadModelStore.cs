using System.Text.Json;
using Microsoft.Data.Sqlite;
using TastileDesktop.Models;

namespace TastileDesktop.Services;

/// <summary>
/// Local SQLite read model for the Sync API v2 contract (tastile-core
/// v1/14 §6, desktop NF04-D02). Stores the latest synced entity states as
/// opaque Read-view JSON documents plus the sync cursor, so the app can
/// render schedule/actuals while offline.
///
/// Contract invariants honored here:
/// - The cursor is the sole ordering/dedup mechanism (v1/14 §6-0).
/// - Delta changes are applied in response order; entity writes and the
///   cursor persist in the SAME local transaction (§6-4).
/// - `entity.state` is a full-state replace — application is idempotent.
/// - `state = null` / TOMBSTONE deletes the local entity (§6-2).
/// - Snapshot pages stage into `sync_snapshot_staging`; the live entity
///   table and the delta cursor switch atomically only when the final
///   page (`next_cursor == null`) commits, so a crashed walk never
///   leaves a half-applied snapshot.
/// </summary>
public sealed class LocalReadModelStore : IDisposable
{
    /// <summary>Meta key holding the persisted delta cursor (opaque).</summary>
    public const string SyncCursorKey = "sync_cursor";

    /// <summary>Meta key holding the UTC timestamp of the last successful sync.</summary>
    public const string LastSyncAtKey = "last_sync_at";

    private readonly object _gate = new();
    private readonly string _dbPath;
    private SqliteConnection? _connection;

    public LocalReadModelStore(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
    }

    /// <summary>Default per-user location: %LOCALAPPDATA%\Tastile\Sync\readmodel.db.</summary>
    public static string DefaultDbPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tastile",
            "Sync",
            "readmodel.db");

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
            foreach (var pragma in new[] { "PRAGMA journal_mode=WAL;", "PRAGMA foreign_keys=ON;" })
            {
                using var command = connection.CreateCommand();
                command.CommandText = pragma;
                command.ExecuteNonQuery();
            }

            InitializeSchema(connection);
            _connection = connection;
            return connection;
        }
    }

    private static void InitializeSchema(SqliteConnection connection)
    {
        foreach (var sql in new[]
        {
            """
            CREATE TABLE IF NOT EXISTS sync_meta(
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS sync_entities(
                entity_kind INTEGER NOT NULL,
                entity_id TEXT NOT NULL,
                state_json TEXT NOT NULL,
                PRIMARY KEY(entity_kind, entity_id)
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS sync_snapshot_staging(
                entity_kind INTEGER NOT NULL,
                entity_id TEXT NOT NULL,
                state_json TEXT NOT NULL,
                PRIMARY KEY(entity_kind, entity_id)
            )
            """,
        })
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    public string? GetMeta(string key)
    {
        lock (_gate)
        {
            using var command = Connection.CreateCommand();
            command.CommandText = "SELECT value FROM sync_meta WHERE key = $key";
            command.Parameters.AddWithValue("$key", key);
            return command.ExecuteScalar() as string;
        }
    }

    public void SetMeta(string key, string value)
    {
        lock (_gate)
        {
            using var command = Connection.CreateCommand();
            command.CommandText = """
                INSERT INTO sync_meta(key, value) VALUES($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>The persisted delta cursor; null until the first snapshot commits.</summary>
    public string? SyncCursor => GetMeta(SyncCursorKey);

    /// <summary>UTC time of the last fully applied sync page batch; used for staleness display.</summary>
    public DateTimeOffset? LastSyncAt
    {
        get
        {
            var raw = GetMeta(LastSyncAtKey);
            return DateTimeOffset.TryParse(raw, out var parsed) ? parsed : null;
        }
    }

    /// <summary>Number of entities currently held in the local read model.</summary>
    public int EntityCount()
    {
        lock (_gate)
        {
            using var command = Connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sync_entities";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    /// <summary>All stored entities, optionally filtered by entity kind.</summary>
    public IReadOnlyList<LocalSyncEntity> GetEntities(int? entityKind = null)
    {
        lock (_gate)
        {
            using var command = Connection.CreateCommand();
            command.CommandText = entityKind.HasValue
                ? "SELECT entity_kind, entity_id, state_json FROM sync_entities WHERE entity_kind = $kind ORDER BY entity_id"
                : "SELECT entity_kind, entity_id, state_json FROM sync_entities ORDER BY entity_kind, entity_id";
            if (entityKind.HasValue)
            {
                command.Parameters.AddWithValue("$kind", entityKind.Value);
            }

            var results = new List<LocalSyncEntity>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new LocalSyncEntity(
                    EntityKind: reader.GetInt32(0),
                    EntityId: reader.GetString(1),
                    StateJson: reader.GetString(2)));
            }
            return results;
        }
    }

    /// <summary>Raw stored state JSON for one entity, or null when absent/unparseable row.</summary>
    public string? GetEntityStateJson(int entityKind, string entityId)
    {
        lock (_gate)
        {
            using var command = Connection.CreateCommand();
            command.CommandText =
                "SELECT state_json FROM sync_entities WHERE entity_kind = $kind AND entity_id = $id";
            command.Parameters.AddWithValue("$kind", entityKind);
            command.Parameters.AddWithValue("$id", entityId);
            return command.ExecuteScalar() as string;
        }
    }

    /// <summary>Parsed state document for one entity, or null when absent.</summary>
    public JsonElement? GetEntityState(int entityKind, string entityId)
    {
        var row = GetEntities(entityKind).FirstOrDefault(e =>
            string.Equals(e.EntityId, entityId, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(row.StateJson);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Clears the staging table at the start of a snapshot walk. Staging
    /// is throwaway: a crash mid-walk leaves the live table untouched.
    /// </summary>
    public void BeginSnapshot()
    {
        lock (_gate)
        {
            using var command = Connection.CreateCommand();
            command.CommandText = "DELETE FROM sync_snapshot_staging";
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Upserts one snapshot page into staging. Entities with `state = null`
    /// are not expected from the snapshot endpoint; they are treated as
    /// absent (removed from staging) defensively.
    /// </summary>
    public void StageSnapshotEntities(IEnumerable<SyncEntity> entities)
    {
        lock (_gate)
        {
            using var transaction = Connection.BeginTransaction();
            foreach (var entity in entities)
            {
                if (entity.State is null || entity.State.Value.ValueKind == JsonValueKind.Null)
                {
                    DeleteEntity(Connection, transaction, "sync_snapshot_staging", entity.EntityKind, entity.EntityId);
                    continue;
                }

                UpsertEntity(Connection, transaction, "sync_snapshot_staging", entity.EntityKind, entity.EntityId, entity.State.Value.GetRawText());
            }
            transaction.Commit();
        }
    }

    /// <summary>
    /// Atomically replaces the live entity table with the staged snapshot
    /// and seeds the delta cursor with `snapshotCursor` (v1/14 §6-1:
    /// `snapshotCursor` is the delta sync starting point).
    /// </summary>
    public void CommitSnapshot(string snapshotCursor)
    {
        lock (_gate)
        {
            using var transaction = Connection.BeginTransaction();
            Execute(Connection, transaction, "DELETE FROM sync_entities");
            Execute(Connection, transaction, """
                INSERT INTO sync_entities(entity_kind, entity_id, state_json)
                SELECT entity_kind, entity_id, state_json FROM sync_snapshot_staging
                """);
            Execute(Connection, transaction, "DELETE FROM sync_snapshot_staging");
            UpsertMeta(Connection, transaction, SyncCursorKey, snapshotCursor);
            UpsertMeta(Connection, transaction, LastSyncAtKey, DateTimeOffset.UtcNow.ToString("O"));
            transaction.Commit();
        }
    }

    /// <summary>
    /// Applies one delta page in cursor order inside a single transaction
    /// together with the cursor advance (v1/14 §6-4). `nextCursor` is the
    /// page's `next_cursor`: non-empty values are persisted; the empty
    /// string marks the tail of the change log and leaves the previously
    /// persisted cursor in place (the server re-delivers that tail once —
    /// application is idempotent full-state replace).
    /// </summary>
    public void ApplyDeltaPage(IReadOnlyList<SyncChange> changes, string? nextCursor)
    {
        lock (_gate)
        {
            using var transaction = Connection.BeginTransaction();
            foreach (var change in changes)
            {
                var isTombstone = change.ChangeKind == SyncChangeKind.Tombstone
                    || change.State is null
                    || change.State.Value.ValueKind == JsonValueKind.Null;
                if (isTombstone)
                {
                    DeleteEntity(Connection, transaction, "sync_entities", change.EntityKind, change.EntityId);
                }
                else
                {
                    UpsertEntity(Connection, transaction, "sync_entities", change.EntityKind, change.EntityId, change.State!.Value.GetRawText());
                }
            }

            if (!string.IsNullOrEmpty(nextCursor))
            {
                UpsertMeta(Connection, transaction, SyncCursorKey, nextCursor);
            }
            transaction.Commit();
        }
    }

    /// <summary>
    /// Drops the persisted cursor so the next sync re-snapshots (v1/14
    /// §6-5 `CURSOR_EXPIRED` recovery). Entities are retained so offline
    /// reads keep showing the last known state, marked stale via
    /// <see cref="LastSyncAt"/>.
    /// </summary>
    public void ResetSyncCursor()
    {
        lock (_gate)
        {
            using var command = Connection.CreateCommand();
            command.CommandText = "DELETE FROM sync_meta WHERE key = $key";
            command.Parameters.AddWithValue("$key", SyncCursorKey);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Wipes entities, staging, and sync metadata (sign-out / reset).</summary>
    public void ClearAll()
    {
        lock (_gate)
        {
            foreach (var sql in new[]
            {
                "DELETE FROM sync_entities",
                "DELETE FROM sync_snapshot_staging",
                "DELETE FROM sync_meta",
            })
            {
                using var command = Connection.CreateCommand();
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void UpsertEntity(SqliteConnection connection, SqliteTransaction transaction, string table, int entityKind, string entityId, string stateJson)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO {table}(entity_kind, entity_id, state_json) VALUES($kind, $id, $state)
            ON CONFLICT(entity_kind, entity_id) DO UPDATE SET state_json = excluded.state_json
            """;
        command.Parameters.AddWithValue("$kind", entityKind);
        command.Parameters.AddWithValue("$id", entityId);
        command.Parameters.AddWithValue("$state", stateJson);
        command.ExecuteNonQuery();
    }

    private static void DeleteEntity(SqliteConnection connection, SqliteTransaction transaction, string table, int entityKind, string entityId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {table} WHERE entity_kind = $kind AND entity_id = $id";
        command.Parameters.AddWithValue("$kind", entityKind);
        command.Parameters.AddWithValue("$id", entityId);
        command.ExecuteNonQuery();
    }

    private static void UpsertMeta(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO sync_meta(key, value) VALUES($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}

/// <summary>A stored sync entity row (state kept as raw JSON text).</summary>
public sealed record LocalSyncEntity(int EntityKind, string EntityId, string StateJson);
