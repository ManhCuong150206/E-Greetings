using System.ComponentModel.DataAnnotations;

namespace E_Greeting.Models;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Full name is required")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(3, ErrorMessage = "Password must be more than 2 characters!")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm Password is required")]
    [Compare("Password", ErrorMessage = "Confirm password does not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}