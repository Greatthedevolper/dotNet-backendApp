namespace DotNetApi.Models;

public class Listing
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Title { get; set; }
    public required string Desc { get; set; }
    public required string Tags { get; set; }
    public required string Email { get; set; }
    public required string Link { get; set; }
    public string? Image { get; set; }
    public int Approved { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
