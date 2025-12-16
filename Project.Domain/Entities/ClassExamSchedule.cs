using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class ClassExamSchedule
    {
        [Key]
        public int Id { get; set; }

        public int ClassId { get; set; }

        public int ExamScheduleId { get; set; }

        public virtual Class Class { get; set; } = null!;

        public virtual ExamSchedule ExamSchedule { get; set; } = null!;
    }
}
