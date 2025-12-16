using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class ExamSchedule
    {
        [Key]
        public int Id { get; set; }

        public int ExamId { get; set; }

        public string? Title { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public string? Description { get; set; }

        public byte? Status { get; set; }

        public int? SubjectId { get; set; }

        public int? RoomId { get; set; }

        public Guid? CreatedBy { get; set; }

        public Guid? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CreatedAt { get; set; }

        public virtual ICollection<ClassExamSchedule> ClassExamSchedules { get; set; } = new List<ClassExamSchedule>();

        public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();

        public virtual Room? Room { get; set; }

        public virtual Subject? Subject { get; set; }

        public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}
