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
        // To-be 3.1: chương (cùng môn với câu) và lời giải; câu cũ để NULL
        public int? ChapterId { get; set; }
        public string? Explanation { get; set; }
        // To-be T16 (QT16-2): tùy chọn so khớp câu điền ngắn (loại 4); loại khác để NULL. Đáp án chấp nhận = Answers IsCorrect.
        public bool? ShortAnswerIgnoreDiacritics { get; set; }
        public bool? ShortAnswerNumeric { get; set; }
        public decimal? ShortAnswerTolerance { get; set; }
        // To-be T18 (QT18-2): đoạn dùng chung — GroupId và GroupOrder cùng có hoặc cùng NULL
        public int? GroupId { get; set; }
        public int? GroupOrder { get; set; }
        public virtual QuestionGroup? Group { get; set; }

        public Guid? CreatedBy { get; set; }

        public Guid? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CreatedAt { get; set; }

        public virtual ICollection<AnswerSubmission> AnswerSubmissions { get; set; } = new List<AnswerSubmission>();

        public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();

        public virtual ICollection<ExamDetailQuestion> ExamDetailQuestions { get; set; } = new List<ExamDetailQuestion>();

        public virtual ICollection<QuestionAnswer> QuestionAnswers { get; set; } = new List<QuestionAnswer>();

        [NotMapped]
        public bool IsUsedInExam { get; set; }

        /// <summary>T19 (nợ L2): trạng thái trước khi sửa (chỉ có sau SaveWithAnswersAsync với id); không lưu DB.</summary>
        [NotMapped]
        public byte? PreviousStatus { get; set; }

        public virtual QuestionLevel? QuestionLevel { get; set; }

        public virtual QuestionType QuestionType { get; set; } = null!;
        public virtual Chapter? Chapter { get; set; }
        public virtual ICollection<QuestionTag> QuestionTags { get; set; } = new List<QuestionTag>();
        // To-be T14: đính kèm media
        public virtual ICollection<QuestionMedia> QuestionMedia { get; set; } = new List<QuestionMedia>();
    }
}
