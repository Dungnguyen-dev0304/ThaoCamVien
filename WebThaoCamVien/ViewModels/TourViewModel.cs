using SharedThaoCamVien.Models;

namespace WebThaoCamVien.ViewModels
{
    public class TourListViewModel
    {
        public List<Tour> Tours { get; set; } = new();
    }

    public class EditTourViewModel
    {
        public Tour Tour { get; set; } = new();

        // POI đã được thêm vào tour (có thứ tự)
        public List<TourPoi> TourPois { get; set; } = new();

        // Tất cả POI trong hệ thống để chọn
        public List<Poi> AllPois { get; set; } = new();
    }
}