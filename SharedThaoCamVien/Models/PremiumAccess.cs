using System;

namespace SharedThaoCamVien.Models
{
    /// <summary>
    /// Ghi nhận thiết bị đã mua quyền truy cập nội dung premium cho một POI.
    /// Mỗi row = một lần mua thành công → device có thể xem POI đó mãi mãi (hoặc đến ExpiresAt).
    /// </summary>
    public class PremiumAccess
    {
        public int Id { get; set; }

        public int TransactionId { get; set; }
        public virtual PaymentTransaction? Transaction { get; set; }

        public int PoiId { get; set; }
        public virtual Poi? Poi { get; set; }

        public string SessionId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Null = không hết hạn. Có thể set = 30 ngày nếu muốn.</summary>
        public DateTime? ExpiresAt { get; set; }
    }
}
