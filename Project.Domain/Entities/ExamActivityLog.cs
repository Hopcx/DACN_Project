using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class ExamActivityLog
    {
        [Key]
        public int Id { get; set; }

        public int ExamId { get; set; }

        public int? ExamDetailId { get; set; }

        public int? ExamScheduleId { get; set; }

        public Guid UserId { get; set; }

        public DateTime ActionTime { get; set; }

        public string? ActionType { get; set; }

        public virtual Exam Exam { get; set; } = null!;

        public virtual ExamDetail? ExamDetail { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
