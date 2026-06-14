namespace Project.Application.DTOs.ExamDetailDTO
{
    public class ExamDetailCreateDto
    {
        public int ExamId { get; set; }
        public string Code { get; set; } = string.Empty;
        public byte? Status { get; set; }
        public DateTime CreateDate { get; set; }
        public Guid CreateBy { get; set; }
        public DateTime UpdateDate { get; set; }
        public Guid UpdateBy { get; set; }
    }
}
