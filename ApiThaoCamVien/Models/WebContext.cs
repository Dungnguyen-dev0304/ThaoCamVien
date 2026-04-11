using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;

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

    // Các bảng dữ liệu (DbSet)
    public virtual DbSet<Poi> Pois { get; set; }
    public virtual DbSet<PoiCategory> PoiCategories { get; set; }
    public virtual DbSet<PoiMedium> PoiMedia { get; set; }
    public virtual DbSet<PoiVisitHistory> PoiVisitHistories { get; set; }
    public virtual DbSet<QrCode> QrCodes { get; set; }
    public virtual DbSet<Tour> Tours { get; set; }
    public virtual DbSet<TourPoi> TourPois { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserLocationLog> UserLocationLogs { get; set; }

    public virtual DbSet<PoiTranslation> PoiTranslations { get; set; }
    public DbSet<PoiAudio> PoiAudios { get; set; }

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //    => optionsBuilder.UseSqlServer("Server=.;Database=web;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=.;Database=web;Trusted_Connection=True;TrustServerCertificate=True;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1. Cấu hình bảng POI
        modelBuilder.Entity<Poi>(entity =>
        {
            entity.HasKey(e => e.PoiId).HasName("PK__pois__6176E7AC68F9D4D2");
            entity.ToTable("pois");

            entity.Property(e => e.PoiId).HasColumnName("poi_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())").HasColumnType("datetime").HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ImageThumbnail).HasMaxLength(255).IsUnicode(false).HasColumnName("image_thumbnail");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Latitude).HasColumnType("decimal(11, 8)").HasColumnName("latitude");
            entity.Property(e => e.Longitude).HasColumnType("decimal(11, 8)").HasColumnName("longitude");
            entity.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
            entity.Property(e => e.Priority).HasDefaultValue(1).HasColumnName("priority");
            entity.Property(e => e.Radius).HasDefaultValue(15).HasColumnName("radius");

            // Quan hệ 1 chiều với Category
            entity.HasOne<PoiCategory>().WithMany(p => p.Pois)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK_pois_category");
        });

        // 2. Cấu hình bảng POI_CATEGORY
        modelBuilder.Entity<PoiCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__poi_cate__D54EE9B48CAB2166");
            entity.ToTable("poi_category");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CategoryName).HasMaxLength(100).HasColumnName("category_name");
            entity.Property(e => e.Description).HasMaxLength(255).HasColumnName("description");
        });

        // 3. Cấu hình bảng POI_MEDIUM
        modelBuilder.Entity<PoiMedium>(entity =>
        {
            entity.HasKey(e => e.MediaId).HasName("PK__poi_medi__D0A840F456B70667");
            entity.ToTable("poi_media");
            entity.Property(e => e.MediaId).HasColumnName("media_id");
            entity.Property(e => e.PoiId).HasColumnName("poi_id");
            entity.Property(e => e.MediaUrl).HasMaxLength(255).IsUnicode(false).HasColumnName("media_url");

            entity.HasOne(d => d.Poi).WithMany()
                .HasForeignKey(d => d.PoiId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_media_poi");
        });

        // 4. Cấu hình bảng QR_CODES
        modelBuilder.Entity<QrCode>(entity =>
        {
            entity.HasKey(e => e.QrId).HasName("PK__qr_codes__6CD5101BE1633F49");
            entity.ToTable("qr_codes");
            entity.Property(e => e.PoiId).HasColumnName("poi_id");

            entity.HasOne(d => d.Poi).WithMany()
                .HasForeignKey(d => d.PoiId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_qr_poi");
        });

        // 5. Cấu hình TOUR và TOUR_POI
        modelBuilder.Entity<Tour>(entity =>
        {
            entity.HasKey(e => e.TourId).HasName("PK__tour__4B16B9E665B63F6D");
            entity.ToTable("tour");
        });

        modelBuilder.Entity<TourPoi>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__tour_poi__3213E83FE13266EB");
            entity.ToTable("tour_poi");
            entity.HasOne(d => d.Poi).WithMany().HasForeignKey(d => d.PoiId).HasConstraintName("FK_tourpoi_poi");
            entity.HasOne(d => d.Tour).WithMany(p => p.TourPois).HasForeignKey(d => d.TourId).HasConstraintName("FK_tourpoi_tour");
        });

        // 6. Cấu hình USER
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__users__B9BE370F89EDFDDA");
            entity.ToTable("users");
        });

        // 7. Cấu hình POI_VISIT_HISTORY
        modelBuilder.Entity<PoiVisitHistory>(entity =>
        {
            entity.HasKey(e => e.VisitId).HasName("PK__poi_visi__375A75E1DEEE7CA0");
            entity.ToTable("poi_visit_history");
            entity.HasOne(d => d.Poi).WithMany().HasForeignKey(d => d.PoiId).HasConstraintName("FK_visit_poi");
            entity.HasOne(d => d.User).WithMany(p => p.PoiVisitHistories).HasForeignKey(d => d.UserId).HasConstraintName("FK_visit_user");
        });

        // 8. Cấu hình USER_LOCATION_LOG (Đã sửa lỗi Warning Decimal)
        modelBuilder.Entity<UserLocationLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__user_loc__3213E83F20760A9F");
            entity.ToTable("user_location_log");

            // Giữ lại định nghĩa ID
            entity.Property(e => e.Id).HasColumnName("id");

            // Giữ tọa độ chính xác cao (Bạn làm rất tốt chỗ này)
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(11, 8)")
                .HasColumnName("latitude");

            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(11, 8)")
                .HasColumnName("longitude");

            // PHẢI GIỮ DÒNG NÀY: Để DB tự động lưu thời gian khi người dùng di chuyển
            entity.Property(e => e.RecordedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("recorded_at");

            entity.Property(e => e.UserId).HasColumnName("user_id");

            // Khóa ngoại trỏ về bảng User
            entity.HasOne(d => d.User).WithMany(p => p.UserLocationLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_location_user");
        });

        // Thêm vào cấu hình cho bảng POI_TRANSLATIONS
        modelBuilder.Entity<PoiTranslation>(entity =>
        {
            entity.HasKey(e => e.TranslationId).HasName("PK_poi_translations");
            entity.ToTable("poi_translations");
            entity.Property(e => e.TranslationId).HasColumnName("translation_id");
            entity.Property(e => e.PoiId).HasColumnName("poi_id");
            entity.Property(e => e.LanguageCode).HasMaxLength(10).HasColumnName("language_code");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Description).HasColumnName("description");

            entity.HasOne(d => d.Poi)
                .WithMany()
                .HasForeignKey(d => d.PoiId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_translations_poi");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}