namespace OrgNet.Pwa;

public static class StringExtensions
{
    public static string Truncate(this string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }
}
