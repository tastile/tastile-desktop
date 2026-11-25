using Microsoft.Data.Sqlite;

namespace TastileDesktop.Tests;

/// <summary>
/// Scoped replacement for <see cref="SqliteConnection.ClearAllPools"/> in
/// test teardown. ClearAllPools() is process-global: under xUnit's default
/// parallelization it can dispose the sqlite3 handle of a pooled connection
/// that another test class is still using, surfacing as
/// ObjectDisposedException ("SQLitePCL.sqlite3") in whichever test happens
/// to be mid-statement.
///
/// Every SQLite-backed test uses a unique per-test database path, so each
/// path maps to its own connection-string pool group. Clearing only that
/// group releases the native handle (so the temp files can be deleted on
/// Windows) without touching connections owned by parallel test classes.
/// </summary>
internal static class SqliteTestPools
{
    /// <summary>
    /// Empties the connection pools for the given database file paths.
    /// The connection string must match the one the store under test used
    /// (<c>Data Source={path}</c>) for the pool group lookup to hit.
    /// </summary>
    public static void Clear(params string[] dbPaths)
    {
        foreach (var dbPath in dbPaths)
        {
            // PoolGroup is assigned by the ConnectionString setter, so an
            // unopened connection is enough to address the right pool.
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            SqliteConnection.ClearPool(connection);
        }
    }
}
