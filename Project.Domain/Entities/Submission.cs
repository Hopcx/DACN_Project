using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Submission
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public int ExamDetailId { get; set; }

        public int ExamScheduleId { get; set; }

        public DateTime SubmitTime { get; set; }

        public TimeOnly TimeTaken { get; set; }

        public double TotalMark { get; set; }

        public bool IsPassed { get; set; }

        public int UnAnswered { get; set; }

        public int Answered { get; set; }

        public string? Note { get; set; }

        public byte? Type { get; set; }

        public bool? Status { get; set; }

        public virtual ICollection<AnswerSubmission> AnswerSubmissions { get; set; } = new List<AnswerSubmission>();

        public virtual ExamDetail ExamDetail { get; set; } = null!;

        public virtual ExamSchedule ExamSchedule { get; set; } = null!;

        public virtual User User { get; set; } = null!;
    }
}
