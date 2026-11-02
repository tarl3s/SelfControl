using System.Globalization;

namespace SelfControl.App;

/// <summary>
/// UI translation. English text is the key and the fallback: <c>L.T("Save")</c> returns the current language's
/// version ("Salvar" in pt-BR). Languages: "en" and "pt-BR"; the Portuguese table is in Loc.PtBr.cs.
/// </summary>
public static partial class L
{
    public const string English = "en", Portuguese = "pt-BR";

    static string? _setting;
    static string _lang = FromCulture();

    /// <summary>The language in use: "en" or "pt-BR".</summary>
    public static string Current => _lang;
    /// <summary>What the person chose: null (follow Windows), "en" or "pt-BR".</summary>
    public static string? Setting => _setting;

    /// <summary>Windows UI language: Portuguese if it starts with "pt", else English.</summary>
    public static string FromCulture() =>
        CultureInfo.CurrentUICulture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? Portuguese : English;

    /// <summary>Chooses the language: null/"" = follow Windows; "en" or "pt-BR" (any "pt*") = fixed.</summary>
    public static void Set(string? setting)
    {
        _setting = string.IsNullOrWhiteSpace(setting) ? null : setting.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? Portuguese : English;
        _lang = _setting ?? FromCulture();
    }

    public static string T(string english) => In(_lang, english);

    /// <summary>Like <see cref="T"/> for a format string: <c>L.F("{0} of {1}", a, b)</c>.</summary>
    public static string F(string english, params object?[] args) => string.Format(T(english), args);

    /// <summary>The text in a given language (used to say something in the language about to be chosen).</summary>
    public static string In(string lang, string english) =>
        lang == Portuguese && PtBr.TryGetValue(english, out var pt) ? pt : english;

    /// <summary>True when the Portuguese table has this English key.</summary>
    public static bool HasPortuguese(string english) => PtBr.ContainsKey(english);
    public static IEnumerable<string> PortugueseKeys => PtBr.Keys;
}
