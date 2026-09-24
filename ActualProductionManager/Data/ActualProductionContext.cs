using ActualProductionManager.Models.Databases;
using Microsoft.EntityFrameworkCore;

namespace ActualProductionManager.Data;

/// <summary>
/// 実績生産管理システムのデータベースコンテキストクラス。
/// PostgreSQLデータベースとの接続を管理し、各テーブルエンティティへのアクセスを提供します。
/// </summary>
/// <param name="options">DbContext オプション。</param>
/// <param name="configuration">アプリケーション設定インターフェース。</param>
public partial class ActualProductionContext(DbContextOptions<ActualProductionContext> options, IConfiguration configuration) : DbContext(options)
{
    private string Schema => configuration["Database:Schema"] ?? "dev";

    public virtual DbSet<Item> Items { get; set; }
    public virtual DbSet<Line> Lines { get; set; }
    public virtual DbSet<ProductionCondition> ProductionConditions { get; set; }
    public virtual DbSet<SetupTime> SetupTimes { get; set; }
    public virtual DbSet<Mold> Molds { get; set; }
    public virtual DbSet<MoldMaintenanceHistory> MoldMaintenanceHistories { get; set; }

    /// <summary>
    /// データベースモデルの構成を定義します。各エンティティのテーブル名、カラム名、主キー、外部キーなどをマッピングします。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダーインスタンス。</param>
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

        modelBuilder.Entity<Mold>(entity =>
        {
            entity.HasKey(e => e.Code).HasName("pk_molds_01");

            entity.ToTable("molds", Schema);

            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.StorageLocation).HasColumnName("storage_location");
            entity.Property(e => e.WarningShots).HasColumnName("warning_shots");
            entity.Property(e => e.ReplacementShots).HasColumnName("replacement_shots");
            entity.Property(e => e.Remarks).HasColumnName("remarks");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<MoldMaintenanceHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_mold_maintenance_histories_01");
            entity.ToTable("mold_maintenance_histories", Schema);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MoldCode).HasColumnName("mold_code");
            entity.Property(e => e.MaintenanceDate).HasColumnName("maintenance_date");
            entity.Property(e => e.MaintenanceShots).HasColumnName("maintenance_shots");
            entity.Property(e => e.Remarks).HasColumnName("remarks");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne<Mold>().WithMany().HasForeignKey(e => e.MoldCode).HasConstraintName("fk_mold_maintenance_histories_01");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    /// <summary>
    /// モデル作成処理の部分メソッド。追加のカスタム設定が必要な場合はこのメソッドを実装します。
    /// </summary>
    /// <param name="modelBuilder">モデルビルダーインスタンス。</param>
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
