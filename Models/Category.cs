namespace DotNetApi.Models;
public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Listing> Listings { get; set; } = [];
}