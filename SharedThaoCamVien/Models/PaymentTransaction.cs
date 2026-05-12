using System;

namespace SharedThaoCamVien.Models
{
    /// <summary>
    /// Giao dịch thanh toán VNPay cho nội dung Premium.
    /// Status flow: pending → processing → success | failed | expired
    /// </summary>
    public class PaymentTransaction
    {
        public int Id { get; set; }

        /// <summary>Mã giao dịch nội bộ. Format: TXN-YYYYMMDD-XXXXX</summary>
        public string TransactionCode { get; set; } = string.Empty;

        public int PoiId { get; set; }
        public virtual Poi? Poi { get; set; }

        /// <summary>Session ID từ AppClientPresence (thiết bị mobile)</summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>Device fingerprint (lưu trên thiết bị, stable per install)</summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>Số tiền VND</summary>
        public decimal Amount { get; set; }

        public string Currency { get; set; } = "VND";

        /// <summary>vnpay</summary>
        public string PaymentMethod { get; set; } = "vnpay";

        /// <summary>pending | processing | success | failed | expired</summary>
        public string Status { get; set; } = "pending";

        /// <summary>URL thanh toán VNPay (encode làm QR hiển thị trong app)</summary>
        public string? VnPayUrl { get; set; }

        /// <summary>QR hết hạn sau 15 phút kể từ lúc tạo</summary>
        public DateTime? QrExpiredAt { get; set; }

        /// <summary>Mã tham chiếu giao dịch từ VNPay (vnp_TransactionNo)</summary>
        public string? GatewayRef { get; set; }

        /// <summary>JSON raw response từ VNPay IPN callback</summary>
        public string? GatewayResponse { get; set; }

        /// <summary>Lý do thất bại (nếu có)</summary>
        public string? FailureReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
