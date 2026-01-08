namespace Project.Infrastructure.Persistence.ViewModels
{
    public class ClassWithUser
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ClassCode { get; set; }
        public string? Description { get; set; }
        public int CountUser { get; set; } = 0;
        public int CountConfirm { get; set; } = 0;
        public int Capacity { get; set; }
        public int? CurrentCapacity { get; set; }
        public Guid? TeacherId { get; set; }
        public string? FullName { get; set; }
        public int? SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public byte? Status { get; set; }
    }
}
