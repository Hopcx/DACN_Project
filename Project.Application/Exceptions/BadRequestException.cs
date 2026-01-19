namespace Project.Application.Exceptions
{
    /// <summary>
    /// Exception được throw khi request không hợp lệ
    /// </summary>
    public class BadRequestException : Exception
    {
        public BadRequestException(string message) : base(message)
        {
        }
    }
}
