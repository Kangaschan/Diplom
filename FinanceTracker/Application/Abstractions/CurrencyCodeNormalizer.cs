namespace Application.Abstractions;

public static class CurrencyCodeNormalizer
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().ToUpperInvariant();
        if (trimmed.Length == 3 && trimmed.All(character => character is >= 'A' and <= 'Z'))
        {
            normalized = trimmed;
            return true;
        }

        switch (trimmed)
        {
            case "₽":
            case "Р":
            case "РУБ":
            case "RUR":
                normalized = "RUB";
                return true;
            case "$":
                normalized = "USD";
                return true;
            case "€":
                normalized = "EUR";
                return true;
            case "BYR":
                normalized = "BYN";
                return true;
            default:
                return false;
        }
    }
}
