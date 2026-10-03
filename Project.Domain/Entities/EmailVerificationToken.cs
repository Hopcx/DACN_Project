using System.ComponentModel.DataAnnotations;

namespace Project.Domain.Entities;

public class EmailVerificationToken
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    [MaxLength(64)] public string TokenHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
