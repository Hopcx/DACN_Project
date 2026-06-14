namespace Project.Application.DTOs.UserPermissionDTO
{
    public class UserPermissionResponseDto
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int PermissionId { get; set; }
    }
}
