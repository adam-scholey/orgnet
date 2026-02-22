using Microsoft.EntityFrameworkCore;

namespace OrgNet.Desktop.Services;

/// <summary>
/// Local SQLite cache for offline-first capability.
/// Stores tenant info, user profile, and module list locally so the app
/// can render immediately on launch without waiting for API calls.
/// 
/// Architectural decision: SQLite over file-based JSON.
/// - Queryable, transactional, handles concurrent reads.
/// - EF Core Sqlite provider keeps the code consistent with the backend.
/// - DB file stored in LocalApplicationData — survives app updates.
/// </summary>
public class LocalCacheService
{
    private LocalCacheDbContext? _db;

    public async Task InitialiseAsync()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OrgNet", "cache.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var options = new DbContextOptionsBuilder<LocalCacheDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _db = new LocalCacheDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task SetAsync(string key, string value)
    {
        if (_db == null) return;
        var entry = await _db.CacheEntries.FindAsync(key);
        if (entry != null)
        {
            entry.Value = value;
            entry.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.CacheEntries.Add(new CacheEntry { Key = key, Value = value });
        }
        await _db.SaveChangesAsync();
    }

    public async Task<string?> GetAsync(string key)
    {
        if (_db == null) return null;
        var entry = await _db.CacheEntries.FindAsync(key);
        return entry?.Value;
    }

    public async Task RemoveAsync(string key)
    {
        if (_db == null) return;
        var entry = await _db.CacheEntries.FindAsync(key);
        if (entry != null)
        {
            _db.CacheEntries.Remove(entry);
            await _db.SaveChangesAsync();
        }
    }

    public async Task ClearAsync()
    {
        if (_db == null) return;
        _db.CacheEntries.RemoveRange(_db.CacheEntries);
        await _db.SaveChangesAsync();
    }
}

public class CacheEntry
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class LocalCacheDbContext : DbContext
{
    public LocalCacheDbContext(DbContextOptions<LocalCacheDbContext> options) : base(options) { }
    public DbSet<CacheEntry> CacheEntries => Set<CacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CacheEntry>(e =>
        {
            e.HasKey(c => c.Key);
            e.Property(c => c.Key).HasMaxLength(256);
        });
    }
}
