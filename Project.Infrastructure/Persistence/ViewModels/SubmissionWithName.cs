using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Project.Infrastructure.Persistence.ViewModels
{
    public class SubmissionWithName
    {

        public int Id { get; set; }
        public string Name { get; set; }
        public string SubjectName { get; set; }
        public int ExamDetailId { get; set; }
        public Guid UserId { get; set; }
        public int SubmissionId { get; set; }
        public DateTime SubmitTime { get; set; }
        public TimeSpan TimeTaken { get; set; }
        public double TotalMark { get; set; }
        public bool IsPassed { get; set; }
        public string Note { get; set; }
    }
}
