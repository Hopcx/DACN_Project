namespace Project.Application.DTOs.QuestionDTO
{
    /// <summary>T19 (nợ L7): một trang của GET /web/questions khi client gửi page.</summary>
    public class QuestionPageDto
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public List<QuestionResponseDto> Items { get; set; } = new();
    }
}
