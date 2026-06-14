namespace Project.Application.DTOs.ClassUserDTO
{
    public class ClassUserCreateDto
    {
        public int ClassId { get; set; }
        public Guid UserId { get; set; }
        public byte? Status { get; set; }
    }
}
