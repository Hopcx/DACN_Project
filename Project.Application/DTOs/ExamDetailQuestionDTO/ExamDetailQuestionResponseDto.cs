namespace Project.Application.DTOs.ExamDetailQuestionDTO
{
    public class ExamDetailQuestionResponseDto
    {
        public int Id { get; set; }
        public int ExamDetailId { get; set; }
        public int QuestionId { get; set; }
        public double Point { get; set; }
    }
}
