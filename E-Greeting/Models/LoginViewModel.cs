// Models/LoginViewModel.cs
using System.ComponentModel.DataAnnotations;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress]
    public string Email { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    public string Password { get; set; }
}