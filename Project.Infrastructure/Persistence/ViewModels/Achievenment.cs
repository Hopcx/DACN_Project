namespace Project.Infrastructure.Persistence.ViewModels
{
    public class Achievenment
    {
        public int ClassId { get; set; }

        public string ClassName { get; set; }

        public string SubjectName { get; set; }

        public double AvgScore { get; set; } = 0;
    }
}
