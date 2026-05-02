namespace Subscriptions.Models.Response;

public class SubscriptionInfoDto
{
    public required string Name { get; set; }
    public required IEnumerable<PriceDto> Prices { get; set; } 
}
