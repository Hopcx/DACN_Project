namespace Project.Application.DTOs.AnswerSubmissionDTO
{
    public class AnswerSubmissionCreateDto
    {
        public int SubmissionId { get; set; }
        public int AnswerId { get; set; }
        public int QuestionId { get; set; }
    }
}
