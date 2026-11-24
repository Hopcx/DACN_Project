namespace Project.Application.Services;

/// <summary>
/// To-be T17: loại câu hỏi hệ thống (seed + khóa sửa/xóa). Chấm điểm và kiểm tra đều theo các Id này.
/// 1 Đúng/Sai, 2 Một đáp án, 3 Nhiều đáp án, 4 Điền ngắn (T16), 5 Ghép cặp, 6 Đúng/Sai nhiều mệnh đề (T17).
/// </summary>
public static class QuestionKinds
{
    public const int TrueFalse = 1;
    public const int SingleChoice = 2;
    public const int MultipleChoice = 3;
    public const int ShortAnswer = 4;
    public const int Matching = 5;
    public const int Statements = 6;
    public const int Max = Statements;

    public const int MinItems = 2;
    public const int MaxItems = 10;

    public static bool IsSupported(int typeId) => typeId is >= 1 and <= Max;

    /// <summary>Loại có nhiều mục, mỗi mục một câu trả lời riêng (AttemptAnswerDto.Items).</summary>
    public static bool HasItems(int typeId) => typeId is Matching or Statements;
}
