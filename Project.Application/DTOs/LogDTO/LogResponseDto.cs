namespace Project.Application.DTOs.LogDTO
{
    public class LogResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public byte? Status { get; set; }
    }
}
