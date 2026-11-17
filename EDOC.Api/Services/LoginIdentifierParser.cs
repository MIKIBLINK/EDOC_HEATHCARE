namespace EDOC.Api.Services;

public static class LoginIdentifierParser
{
    public static string? NormalizePhoneNumber(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;
        var value = identifier.Trim();
        if (value.Contains('@')) return null;
        if (value.Any(character => !char.IsAsciiDigit(character)
            && !char.IsWhiteSpace(character)
            && character is not '+' and not '-' and not '(' and not ')')) return null;
        if (value.Skip(1).Contains('+')) return null;

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (value.StartsWith('+'))
            return digits.Length is >= 8 and <= 15 && digits[0] != '0' ? $"+{digits}" : null;
        if (digits.StartsWith('0') && digits.Length is 9 or 10)
            return $"+855{digits[1..]}";
        return digits.Length is >= 8 and <= 15 && digits[0] != '0' ? $"+{digits}" : null;
    }
}
