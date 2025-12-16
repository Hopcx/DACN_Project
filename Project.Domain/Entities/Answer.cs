using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Answer
    {
        [Key]
        public int Id { get; set; }

        public int QuestionId { get; set; }

        public string Content { get; set; } = null!;

        public bool IsCorrect { get; set; }

        public byte? Status { get; set; }

        public Guid? CreatedBy { get; set; }

        public Guid? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CreatedAt { get; set; }

        public virtual ICollection<AnswerSubmission> AnswerSubmissions { get; set; } = new List<AnswerSubmission>();

        public virtual Question Question { get; set; } = null!;

        public virtual ICollection<QuestionAnswer> QuestionAnswers { get; set; } = new List<QuestionAnswer>();
    }
}
