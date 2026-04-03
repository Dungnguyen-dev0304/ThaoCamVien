using SharedThaoCamVien.Models;

namespace WebThaoCamVien.ViewModels
{
    public class EditPOIViewModel
    {
        public Poi Poi { get; set; }
        public List<PoiTranslation> Translations { get; set; } = new();
    }
}
