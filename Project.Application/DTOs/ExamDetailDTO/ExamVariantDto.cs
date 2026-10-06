namespace Project.Application.DTOs.ExamDetailDTO;

public sealed class RandomQuestionSelectionDto
{
    public int? QuestionLevelId { get; set; }
    public int Count { get; set; }
}

public sealed class ExamVariantSaveDto
{
    public string Code { get; set; } = string.Empty;
    public byte Status { get; set; } = 2;
    public List<int> QuestionIds { get; set; } = [];
    public List<RandomQuestionSelectionDto> RandomSelections { get; set; } = [];
}

public sealed class ExamVariantResponseDto
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public string Code { get; set; } = string.Empty;
    public byte? Status { get; set; }
    public List<ExamVariantQuestionDto> Questions { get; set; } = [];
}

public sealed class ExamVariantQuestionDto
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public double Point { get; set; }
}

public sealed class ExamQuestionOptionDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? QuestionLevelId { get; set; }
    public string? QuestionLevelName { get; set; }
}

public sealed class ExamSubjectOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte? Status { get; set; }
}
