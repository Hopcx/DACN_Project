namespace Project.Application.DTOs.QuestionDTO
{
    public class QuestionResponseDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public byte? Status { get; set; }
        public int SubjectId { get; set; }
        public string? DocumentPath { get; set; }
        public int QuestionTypeId { get; set; }
        public int? QuestionLevelId { get; set; }
        public bool IsUsedInExam { get; set; }
        /// <summary>T19 (nợ L2): trạng thái trước khi sửa, chỉ dùng nội bộ để phân loại audit — không trả cho client.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public byte? PreviousStatus { get; set; }
        public int? ChapterId { get; set; }
        public string? Explanation { get; set; }
        public List<int> TagIds { get; set; } = new();
        // To-be T14: số media đính kèm (chi tiết qua /web/questions/{id}/media). DocumentPath: deprecated, thay bằng media.
        public int MediaCount { get; set; }
        // To-be T16: tùy chọn câu điền ngắn (loại 4; loại khác null). Answers của loại 4 = đáp án chấp nhận.
        public bool? ShortAnswerIgnoreDiacritics { get; set; }
        public bool? ShortAnswerNumeric { get; set; }
        public decimal? ShortAnswerTolerance { get; set; }
        // To-be T18: đoạn dùng chung chứa câu (null nếu câu lẻ)
        public int? GroupId { get; set; }
        public int? GroupOrder { get; set; }
        public string? GroupTitle { get; set; }
        public List<QuestionAnswerResponseDto> Answers { get; set; } = new();
    }

    public class QuestionAnswerResponseDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public string? MatchContent { get; set; }   // To-be T17: vế phải (loại 5)
    }

    public class QuestionSubjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
