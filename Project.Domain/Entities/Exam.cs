using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Exam
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public int SubjectId { get; set; }

        public int NumberOfQuestions { get; set; }

        public int NumberOfRepeat { get; set; }

        public byte Status { get; set; }

        public double MaximmumMark { get; set; }

        public double PassMark { get; set; }

        public bool? AllowViewResult { get; set; }

        public int Duration { get; set; }

        public int? ScoreMethodId { get; set; }

        public Guid? CreatedBy { get; set; }

        public Guid? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CreatedAt { get; set; }

        public int? ExamScheduleId { get; set; }

        public virtual ICollection<ExamActivityLog> ExamActivityLogs { get; set; } = new List<ExamActivityLog>();

        public virtual ICollection<ExamDetail> ExamDetails { get; set; } = new List<ExamDetail>();

        public virtual ExamSchedule? ExamSchedule { get; set; }

        public virtual ScoreMethod? ScoreMethod { get; set; }

        public virtual Subject Subject { get; set; } = null!;
    }
}
