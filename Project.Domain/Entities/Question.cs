using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Entities
{
    public class Question
    {
        [Key]
        public int Id { get; set; }

        public string Content { get; set; } = null!;

        public DateTime CreatedDate { get; set; }

        public byte? Status { get; set; }

        public int SubjectId { get; set; }

        public string? DocumentPath { get; set; }

        public int QuestionTypeId { get; set; }

        public int? QuestionLevelId { get; set; }

        public Guid? CreatedBy { get; set; }

        public Guid? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CreatedAt { get; set; }

        public virtual ICollection<AnswerSubmission> AnswerSubmissions { get; set; } = new List<AnswerSubmission>();

        public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();

        public virtual ICollection<ExamDetailQuestion> ExamDetailQuestions { get; set; } = new List<ExamDetailQuestion>();

        public virtual ICollection<QuestionAnswer> QuestionAnswers { get; set; } = new List<QuestionAnswer>();

        public virtual QuestionLevel? QuestionLevel { get; set; }

        public virtual QuestionType QuestionType { get; set; } = null!;
    }
}
