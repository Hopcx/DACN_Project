namespace Project.Application.DTOs.LogDTO
{
    public class LogCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public byte? Status { get; set; }
    }
}
