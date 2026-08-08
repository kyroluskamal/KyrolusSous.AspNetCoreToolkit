namespace KyrolusSous.Repositories.EF.Runtime.TestApp.Models;

public class Review
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid ProductId { get; set; }
    public virtual Product? Product { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public Guid CustomerId { get; set; }
    public virtual Customer? Customer { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateOnly AddedIn { get; set; }
    public TimeOnly? AddedAt { get; set; }
    public TimeSpan FinishedAt { get; set; }
    public DateTime? DiscontinuedAt { get; set; }
}


