namespace Project.Application.DTOs.AttemptDTO;

// Explicit allowlist: no source entity/navigation, points or correctness flags.
// To-be T15: Media = media chụp trong snapshot, kèm URL ký tạm cho đúng học viên (rỗng khi không cấp URL).
public sealed record AttemptQuestionDto(Guid Id, int QuestionTypeId, string Content,
    IReadOnlyList<AttemptOptionDto> Options, IReadOnlyList<AttemptMediaDto>? Media = null);
public sealed record AttemptOptionDto(Guid Id, string Content);
public sealed record AttemptMediaDto(Guid Id, string Kind, int? Width, int? Height, string Url);
public sealed record AttemptAnswerDto(Guid QuestionId, IReadOnlyList<Guid> SelectedAnswerIds);
