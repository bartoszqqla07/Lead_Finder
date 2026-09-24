using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LeadFinder.Web.Data;

public sealed class LeadFinderDbContext(DbContextOptions<LeadFinderDbContext> options) : DbContext(options)
{
    public DbSet<LeadEntity> Leads => Set<LeadEntity>();
    public DbSet<SearchRunEntity> SearchRuns => Set<SearchRunEntity>();
    public DbSet<AppSettingsEntity> Settings => Set<AppSettingsEntity>();
    public DbSet<BlockedPlaceEntity> BlockedPlaces => Set<BlockedPlaceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LeadEntity>(lead =>
        {
            lead.HasIndex(l => l.PlaceId).IsUnique();
            lead.HasIndex(l => l.City);
            // Enumy jako tekst: baza czytelna w dowolnej przeglądarce SQLite i odporna na zmianę kolejności wartości.
            lead.Property(l => l.Status).HasConversion<string>().HasMaxLength(32);
            lead.Property(l => l.Stage).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<SearchRunEntity>(run =>
        {
            run.Property(r => r.State).HasConversion<string>().HasMaxLength(32);
            // Wyszukiwania sprzed skanów regionalnych dotyczyły jednego miasta.
            run.Property(r => r.CityCount).HasDefaultValue(1);
        });

        modelBuilder.Entity<BlockedPlaceEntity>().HasKey(b => b.PlaceId);

        modelBuilder.Entity<AppSettingsEntity>().HasData(new AppSettingsEntity { Id = AppSettingsEntity.SingletonId });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite nie przechowuje strefy czasowej: bez tego EF zwraca DateTime z Kind=Unspecified,
        // który serializuje się do JSON bez "Z", a przeglądarka uzna go za czas lokalny.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        toDatabase => toDatabase.ToUniversalTime(),
        fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc));
}
