using System.ComponentModel.DataAnnotations;

namespace PCA.Web.Models;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string ReturnUrl { get;set; } = string.Empty;
}

public class CreateUserViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200), Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Department { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public List<string> SelectedRoles { get; set; } = new();
}

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required, MaxLength(200), Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Department { get; set; } = string.Empty;

    [Display(Name = "New Password")]
    [DataType(DataType.Password), MinLength(6)]
    public string? NewPassword { get; set; }

    public List<string> SelectedRoles { get; set; } = new();
    public List<string> AllRoles { get; set; } = new();
    public string Email { get; set; } = string.Empty;
}

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}
