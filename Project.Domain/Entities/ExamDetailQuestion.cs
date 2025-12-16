using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class ExamDetailQuestion
    {
        [Key]
        public int Id { get; set; }

        public int ExamDetailId { get; set; }

        public int QuestionId { get; set; }

        public double Point { get; set; }

        public virtual ExamDetail ExamDetail { get; set; } = null!;

        public virtual Question Question { get; set; } = null!;
    }
}
