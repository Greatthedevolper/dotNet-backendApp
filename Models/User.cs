namespace DotNetApi.Models;

public class User
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? Password { get; set; }
    public string? Role { get; set; }
    public string? VerificationToken { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
}
