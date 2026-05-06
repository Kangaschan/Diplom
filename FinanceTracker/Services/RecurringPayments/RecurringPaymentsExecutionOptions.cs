namespace Services.RecurringPayments;

public sealed class RecurringPaymentsExecutionOptions
{
    public const string SectionName = "RecurringPayments";

    public int PollingIntervalSeconds { get; set; } = 60;
}
