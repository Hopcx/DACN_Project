using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Subject
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public byte? Status { get; set; }

        public Guid? CreatedBy { get; set; }

        public Guid? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CreatedAt { get; set; }

        public virtual ICollection<Class> Classes { get; set; } = new List<Class>();

        public virtual ICollection<ExamSchedule> ExamSchedules { get; set; } = new List<ExamSchedule>();

        public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();
    }
}
