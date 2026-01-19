namespace Project.Application.Exceptions
{
    /// <summary>
    /// Exception được throw khi validation thất bại
    /// </summary>
    public class ValidationException : Exception
    {
        public IDictionary<string, string[]> Errors { get; }

        public ValidationException() : base("Một hoặc nhiều lỗi kiểm tra dữ liệu đã xảy ra.")
        {
            Errors = new Dictionary<string, string[]>();
        }
        public ValidationException(IDictionary<string, string[]> errors) : this()
        {
            Errors = errors;
        }
    }
}
