using ActualProductionManager.Models.Databases;
using Microsoft.EntityFrameworkCore;

namespace ActualProductionManager.Data;

public partial class ActualProductionContext : DbContext
{
    private readonly IConfiguration _configuration;

    private string Schema => _configuration["Database:Schema"] ?? "dev";

    public ActualProductionContext(DbContextOptions<ActualProductionContext> options, IConfiguration configuration)
        : base(options)
    {
        _configuration = configuration;
    }

    public virtual DbSet<Item> Items { get; set; }
    public virtual DbSet<Line> Lines { get; set; }
    public virtual DbSet<ProductionCondition> ProductionConditions { get; set; }
    public virtual DbSet<SetupTime> SetupTimes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresExtension(Schema, "postgres_fdw")
            .HasPostgresExtension(Schema, "tablefunc");

        modelBuilder.Entity<Item>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("items", Schema);

            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<Line>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("lines", Schema);

            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ProductionCondition>(entity =>
        {
            entity.HasKey(e => new { e.LineCode, e.ItemCode }).HasName("pk_production_conditions_01");

            entity.ToTable("production_conditions", Schema);

            entity.Property(e => e.LineCode).HasColumnName("line_code");
            entity.Property(e => e.ItemCode).HasColumnName("item_code");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.PiecesPerCycle).HasColumnName("pieces_per_cycle");
            entity.Property(e => e.TargetCycleTime).HasColumnName("target_cycle_time");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<SetupTime>(entity =>
        {
            entity.HasKey(e => new { e.LineCode, e.ItemCode }).HasName("pk_setup_times_01");

            entity.ToTable("setup_times", Schema);

            entity.Property(e => e.LineCode).HasColumnName("line_code");
            entity.Property(e => e.ItemCode).HasColumnName("item_code");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.TargetSetupTime).HasColumnName("target_setup_time");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
