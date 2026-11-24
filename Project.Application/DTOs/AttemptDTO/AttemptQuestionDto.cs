using System.Text.Json.Serialization;

namespace Project.Application.DTOs.AttemptDTO;

// Explicit allowlist: no source entity/navigation, points or correctness flags.
// To-be T15: Media = media chụp trong snapshot, kèm URL ký tạm cho đúng học viên (rỗng khi không cấp URL).
// To-be T17: Matches = vế phải của câu ghép cặp (loại 5), theo thứ tự đã xáo lúc chụp, Id riêng (không suy ra được cặp); loại khác null.
public sealed record AttemptQuestionDto(Guid Id, int QuestionTypeId, string Content,
    IReadOnlyList<AttemptOptionDto> Options, IReadOnlyList<AttemptMediaDto>? Media = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<AttemptOptionDto>? Matches = null);
public sealed record AttemptOptionDto(Guid Id, string Content);
public sealed record AttemptMediaDto(Guid Id, string Kind, int? Width, int? Height, string Url);
// To-be T16: Text = bài làm câu điền ngắn (loại 4; SelectedAnswerIds rỗng); loại 1–3 không gửi Text.
// To-be T17: Items = câu trả lời từng mục của câu ghép cặp (MatchId) / nhiều mệnh đề (Value); loại khác không gửi Items.
public sealed record AttemptAnswerDto(Guid QuestionId, IReadOnlyList<Guid> SelectedAnswerIds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Text = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<AttemptItemDto>? Items = null);
// OptionId = vế trái (loại 5) hoặc mệnh đề (loại 6); MatchId = vế phải đã chọn (loại 5); Value = Đúng/Sai (loại 6).
public sealed record AttemptItemDto(Guid OptionId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? MatchId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Value = null);
