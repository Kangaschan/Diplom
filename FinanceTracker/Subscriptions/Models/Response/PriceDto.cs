namespace Subscriptions.Models.Response;

public class PriceDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public int DurationDays { get; set; }
}
