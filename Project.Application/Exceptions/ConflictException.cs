namespace Project.Application.Exceptions
{
    /// <summary>Xung đột với dữ liệu hiện có (trùng tên, trạng thái không cho phép) → HTTP 409.</summary>
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message)
        {
        }
    }
}
