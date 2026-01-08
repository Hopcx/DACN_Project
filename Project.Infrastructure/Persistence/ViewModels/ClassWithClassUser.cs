namespace Project.Infrastructure.Persistence.ViewModels
{
    public class ClassWithClassUser
    {
        public Guid StudentId { get; set; }
        public int ClassId { get; set; }
        public string Name { get; set; }
        public string ClassCode { get; set; }
        public string Description { get; set; }
        public int Capacity { get; set; }
        public Guid? TeacherId { get; set; }
        public string? TeacherName { get; set; }
        public int? SubjectId { get; set; }
        public string? SubjectName { get; set; }
        public byte? Status { get; set; }
    }
}
