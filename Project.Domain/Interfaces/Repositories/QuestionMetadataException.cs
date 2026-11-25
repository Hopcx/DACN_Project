namespace Project.Domain.Interfaces.Repositories;

/// <summary>
/// T19 (nợ L1): chương/tag của câu hỏi không còn hợp lệ khi kiểm lại bên trong transaction lưu
/// (vd. chương vừa bị ẩn giữa lúc lưu). Tầng service đổi thành 400 với cùng message.
/// </summary>
public sealed class QuestionMetadataException(string message) : Exception(message);
