using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ApiThaoCamVien.Models;

public partial class WebContext : DbContext
{
    public WebContext()
    {
    }

    public WebContext(DbContextOptions<WebContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Poi> Pois { get; set; }

    public virtual DbSet<PoiCategory> PoiCategories { get; set; }

    public virtual DbSet<PoiMedium> PoiMedia { get; set; }

    public virtual DbSet<PoiVisitHistory> PoiVisitHistories { get; set; }

    public virtual DbSet<QrCode> QrCodes { get; set; }

    public virtual DbSet<Tour> Tours { get; set; }

    public virtual DbSet<TourPoi> TourPois { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserLocationLog> UserLocationLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=DUNGNGUYEN\\SQLEXPRESS;Database=web;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Poi>(entity =>
        {
            entity.HasKey(e => e.PoiId).HasName("PK__pois__6176E7AC68F9D4D2");

            entity.ToTable("pois");

            entity.Property(e => e.PoiId).HasColumnName("poi_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ImageThumbnail)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("image_thumbnail");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(11, 8)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(11, 8)")
                .HasColumnName("longitude");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Priority)
                .HasDefaultValue(1)
                .HasColumnName("priority");
            entity.Property(e => e.Radius)
                .HasDefaultValue(15)
                .HasColumnName("radius");

            entity.HasOne(d => d.Category).WithMany(p => p.Pois)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK_pois_category");
        });

        modelBuilder.Entity<PoiCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__poi_cate__D54EE9B48CAB2166");

            entity.ToTable("poi_category");

            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CategoryName)
                .HasMaxLength(100)
                .HasColumnName("category_name");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
        });

        modelBuilder.Entity<PoiMedium>(entity =>
        {
            entity.HasKey(e => e.MediaId).HasName("PK__poi_medi__D0A840F456B70667");

            entity.ToTable("poi_media");

            entity.Property(e => e.MediaId).HasColumnName("media_id");
            entity.Property(e => e.Language)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("vi")
                .HasColumnName("language");
            entity.Property(e => e.MediaType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("media_type");
            entity.Property(e => e.MediaUrl)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("media_url");
            entity.Property(e => e.PoiId).HasColumnName("poi_id");

            entity.HasOne(d => d.Poi).WithMany(p => p.PoiMedia)
                .HasForeignKey(d => d.PoiId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_media_poi");
        });

        modelBuilder.Entity<PoiVisitHistory>(entity =>
        {
            entity.HasKey(e => e.VisitId).HasName("PK__poi_visi__375A75E1DEEE7CA0");

            entity.ToTable("poi_visit_history");

            entity.Property(e => e.VisitId).HasColumnName("visit_id");
            entity.Property(e => e.ListenDuration).HasColumnName("listen_duration");
            entity.Property(e => e.PoiId).HasColumnName("poi_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VisitTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("visit_time");

            entity.HasOne(d => d.Poi).WithMany(p => p.PoiVisitHistories)
                .HasForeignKey(d => d.PoiId)
                .HasConstraintName("FK_visit_poi");

            entity.HasOne(d => d.User).WithMany(p => p.PoiVisitHistories)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_visit_user");
        });

        modelBuilder.Entity<QrCode>(entity =>
        {
            entity.HasKey(e => e.QrId).HasName("PK__qr_codes__6CD5101BE1633F49");

            entity.ToTable("qr_codes");

            entity.Property(e => e.QrId).HasColumnName("qr_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.PoiId).HasColumnName("poi_id");
            entity.Property(e => e.QrCodeData)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("qr_code_data");

            entity.HasOne(d => d.Poi).WithMany(p => p.QrCodes)
                .HasForeignKey(d => d.PoiId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_qr_poi");
        });

        modelBuilder.Entity<Tour>(entity =>
        {
            entity.HasKey(e => e.TourId).HasName("PK__tour__4B16B9E665B63F6D");

            entity.ToTable("tour");

            entity.Property(e => e.TourId).HasColumnName("tour_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EstimatedTime).HasColumnName("estimated_time");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
        });

        modelBuilder.Entity<TourPoi>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__tour_poi__3213E83FE13266EB");

            entity.ToTable("tour_poi");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderIndex).HasColumnName("order_index");
            entity.Property(e => e.PoiId).HasColumnName("poi_id");
            entity.Property(e => e.TourId).HasColumnName("tour_id");

            entity.HasOne(d => d.Poi).WithMany(p => p.TourPois)
                .HasForeignKey(d => d.PoiId)
                .HasConstraintName("FK_tourpoi_poi");

            entity.HasOne(d => d.Tour).WithMany(p => p.TourPois)
                .HasForeignKey(d => d.TourId)
                .HasConstraintName("FK_tourpoi_tour");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__users__B9BE370F89EDFDDA");

            entity.ToTable("users");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(100)
                .HasColumnName("display_name");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("email");
        });

        modelBuilder.Entity<UserLocationLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__user_loc__3213E83F20760A9F");

            entity.ToTable("user_location_log");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(11, 8)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(11, 8)")
                .HasColumnName("longitude");
            entity.Property(e => e.RecordedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("recorded_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserLocationLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_location_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
