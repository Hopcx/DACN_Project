namespace Project.Application.DTOs.AttemptDTO;

// Explicit allowlist: no source entity/navigation, points or correctness flags.
public sealed record AttemptQuestionDto(Guid Id, int QuestionTypeId, string Content,
    IReadOnlyList<AttemptOptionDto> Options);
public sealed record AttemptOptionDto(Guid Id, string Content);
public sealed record AttemptAnswerDto(Guid QuestionId, IReadOnlyList<Guid> SelectedAnswerIds);
