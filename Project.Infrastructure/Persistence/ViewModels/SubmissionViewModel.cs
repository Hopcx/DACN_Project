using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Infrastructure.Persistence.ViewModels
{
    public class SubmissionViewModel
    {
        public int ID { get; set; }
        public Guid TeacherId { get; set; }
        public string Note { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string ExamName { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public int ClassId { get; set; }
        public string ClassName {  get; set; } 
        public DateTime SubmitTime { get; set; }
        public bool? Status { get; set; }
    }
}
