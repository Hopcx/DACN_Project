using FluentValidation;
using Project.Application.DTOs.UserDTO;

namespace Project.Application.Validators
{
    /// <summary>
    /// FluentValidation validator cho UserCreateDto
    /// Thay thế cho Data Annotations validation
    /// </summary>
    public class UserCreateDtoValidator : AbstractValidator<UserCreateDto>
    {
        public UserCreateDtoValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("FullName không được để trống")
                .MaximumLength(100).WithMessage("FullName không được vượt quá 100 ký tự");

            RuleFor(x => x.UserName)
                .NotEmpty().WithMessage("UserName không được để trống")
                .MinimumLength(6).WithMessage("UserName phải có ít nhất 6 ký tự")
                .Matches("^[a-zA-Z0-9_]+$").WithMessage("UserName chỉ được chứa chữ cái, số và dấu gạch dưới");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email không được để trống")
                .EmailAddress().WithMessage("Định dạng Email không hợp lệ");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("PhoneNumber không được để trống")
                .Matches(@"^[0-9]{10,11}$").WithMessage("PhoneNumber phải gồm 10–11 chữ số");

            RuleFor(x => x.Address)
                .NotEmpty().WithMessage("Address không được để trống")
                .MaximumLength(200).WithMessage("Address không được vượt quá 200 ký tự");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password không được để trống")
                .MinimumLength(6).WithMessage("Password phải có ít nhất 6 ký tự")
                .Matches("[A-Z]").WithMessage("Password phải chứa ít nhất một chữ cái viết hoa")
                .Matches("[a-z]").WithMessage("Password phải chứa ít nhất một chữ cái viết thường")
                .Matches("[0-9]").WithMessage("Password phải chứa ít nhất một chữ số");

            RuleFor(x => x.DateOfBirth)
                .NotEmpty().WithMessage("DateOfBirth không được để trống")
                .LessThan(DateTime.Now.AddYears(-13)).WithMessage("Người dùng phải đủ ít nhất 13 tuổi")
                .GreaterThan(DateTime.Now.AddYears(-100)).WithMessage("DateOfBirth không hợp lệ");

            RuleFor(x => x.LevelId)
                .NotEmpty().WithMessage("LevelId không được để trống")
                .InclusiveBetween(1, 4).WithMessage("LevelId phải nằm trong khoảng từ 1 đến 4");

            RuleFor(x => x.Status)
                .InclusiveBetween((byte)0, (byte)1).WithMessage("Status chỉ được nhận giá trị 0 hoặc 1");
        }

    }
}
