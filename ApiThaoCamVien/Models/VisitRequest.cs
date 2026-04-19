namespace ApiThaoCamVien.Models
{
    public class VisitRequest
    {
        // UserId đã bỏ — app không còn gắn lịch sử thăm với user.
        public int? ListenDuration { get; set; } // số giây đã nghe audio
    }

    /// <summary>Body cho PATCH /api/Pois/visit/{visitId}/duration</summary>
    public class UpdateDurationRequest
    {
        public int Seconds { get; set; }
    }
}
