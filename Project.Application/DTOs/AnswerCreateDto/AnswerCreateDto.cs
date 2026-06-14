using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Application.DTOs.AnswerCreateDto
{
    public class AnswerCreateDto
    {
        public int QuestionId { get; set; }

        public string Content { get; set; } = null!;

        public bool IsCorrect { get; set; }

        public byte? Status { get; set; }

        public Guid? CreatedBy { get; set; }

        public Guid? UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
