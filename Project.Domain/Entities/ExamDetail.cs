using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class ExamDetail
    {
        [Key]
        public int Id { get; set; }

        public int ExamId { get; set; }

        public string Code { get; set; } = null!;

        public byte? Status { get; set; }

        public DateTime CreateDate { get; set; }

        public Guid CreateBy { get; set; }

        public DateTime UpdateDate { get; set; }

        public Guid UpdateBy { get; set; }

        public virtual Exam Exam { get; set; } = null!;

        public virtual ICollection<ExamActivityLog> ExamActivityLogs { get; set; } = new List<ExamActivityLog>();

        public virtual ICollection<ExamDetailQuestion> ExamDetailQuestions { get; set; } = new List<ExamDetailQuestion>();

        public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}
