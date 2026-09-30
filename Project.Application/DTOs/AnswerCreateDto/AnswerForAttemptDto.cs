using System.Linq.Expressions;
using Project.Domain.Entities;

namespace Project.Application.DTOs.AnswerCreateDto;

// Use this projection for future student attempt queries; never reuse AnswerResponseDto.
public sealed class AnswerForAttemptDto
{
    public int Id { get; init; }
    public int QuestionId { get; init; }
    public string Content { get; init; } = null!;

    public static Expression<Func<Answer, AnswerForAttemptDto>> Projection => answer => new AnswerForAttemptDto
    {
        Id = answer.Id,
        QuestionId = answer.QuestionId,
        Content = answer.Content
    };
}
