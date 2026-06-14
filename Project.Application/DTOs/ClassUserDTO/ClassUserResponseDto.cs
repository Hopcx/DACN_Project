namespace Project.Application.DTOs.ClassUserDTO
{
    public class ClassUserResponseDto
    {
        public int Id { get; set; }
        public int ClassId { get; set; }
        public Guid UserId { get; set; }
        public byte? Status { get; set; }
    }
}
