using SharedThaoCamVien.Models;

namespace WebThaoCamVien.ViewModels
{
    public class IndexViewModel
    {
        // ── STAT CARDS ──────────────────────────────
        public int TotalVisits { get; set; }
        public int TodayVisits { get; set; }
        public int TotalUsers { get; set; }
        public int AvgListenDuration { get; set; }

        // ── BIỂU ĐỒ 7 NGÀY ─────────────────────────
        public List<DayVisitData> Last7Days { get; set; } = new();

        // ── TOP 5 POI ───────────────────────────────
        public List<TopPoiData> TopPois { get; set; } = new();

        // ── BẢNG LỊCH SỬ ────────────────────────────
        public List<VisitRowData> RecentVisits { get; set; } = new();

        // ── BỘ LỌC ──────────────────────────────────
        public List<Poi> AllPois { get; set; } = new();
        public int? FilterPoiId { get; set; }
        public int FilterDays { get; set; } = 7;

        // ── PHÂN TRANG ──────────────────────────────
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
    }

    public class DayVisitData
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class TopPoiData
    {
        public string PoiName { get; set; }
        public int? CategoryId { get; set; }
        public int VisitCount { get; set; }
    }

    public class VisitRowData
    {
        public DateTime? VisitTime { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public string? PoiName { get; set; }
        public int? CategoryId { get; set; }
        public int? ListenDuration { get; set; }
    }
}
