namespace Project.Application.DTOs.QuestionDTO
{
    public class QuestionCreateDto
    {
        public string Content { get; set; } = string.Empty;
        public byte? Status { get; set; }
        public int SubjectId { get; set; }
        public string? DocumentPath { get; set; }
        public int QuestionTypeId { get; set; }
        public int? QuestionLevelId { get; set; }
        // To-be T2: PUT thay toàn bộ — không gửi = xóa (giống DocumentPath)
        public int? ChapterId { get; set; }
        public string? Explanation { get; set; }
        public List<int>? TagIds { get; set; }
        // To-be T16: tùy chọn câu điền ngắn (loại 4) — loại khác phải để null
        public bool? ShortAnswerIgnoreDiacritics { get; set; }
        public bool? ShortAnswerNumeric { get; set; }
        public decimal? ShortAnswerTolerance { get; set; }
        public List<QuestionAnswerInputDto> Answers { get; set; } = new();
    }

    public class QuestionAnswerInputDto
    {
        public string Content { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        // To-be T17: vế phải của câu ghép cặp (loại 5; Content = vế trái). Loại 6: IsCorrect = mệnh đề đúng.
        public string? MatchContent { get; set; }
    }
}
