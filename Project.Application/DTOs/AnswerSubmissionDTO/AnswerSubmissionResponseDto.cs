namespace Project.Application.DTOs.AnswerSubmissionDTO
{
    public class AnswerSubmissionResponseDto
    {
        public int Id { get; set; }
        public int SubmissionId { get; set; }
        public int AnswerId { get; set; }
        public int QuestionId { get; set; }
    }
}
