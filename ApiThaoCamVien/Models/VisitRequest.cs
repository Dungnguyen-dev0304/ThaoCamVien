namespace ApiThaoCamVien.Models
{
    public class VisitRequest
    {
        public int? UserId { get; set; }         // null nếu chưa đăng nhập
        public int? ListenDuration { get; set; } // số giây đã nghe audio
    }
}