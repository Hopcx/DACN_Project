using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class AnswerSubmission
    {
        [Key]
        public int Id { get; set; }

        public int SubmissionId { get; set; }

        public int AnswerId { get; set; }

        public int QuestionId { get; set; }

        public virtual Answer Answer { get; set; } = null!;

        public virtual Question Question { get; set; } = null!;

        public virtual Submission Submission { get; set; } = null!;
    }
}
