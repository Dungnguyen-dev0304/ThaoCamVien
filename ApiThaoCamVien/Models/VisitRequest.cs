namespace ApiThaoCamVien.Models
{
    public class VisitRequest
    {
        // UserId đã bỏ — app không còn gắn lịch sử thăm với user.
        public int? ListenDuration { get; set; } // số giây đã nghe audio
    }
}
