using Microsoft.AspNetCore.Mvc.Razor;

namespace ImperialVip.Infrastructure;

/// <summary>
/// Her dil için ayrı view dosyaları arar. Örn: Views/tr/Home/Index.cshtml, Views/de/Home/Index.cshtml
/// Çeviri değil - 4 ayrı site (TR, DE, RU, EN)
/// </summary>
public class LanguageViewLocationExpander : IViewLocationExpander
{
    public static readonly string[] SupportedLanguages = { "tr", "de", "ru", "en" };
    private const string LangKey = "lang";

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        var lang = context.ActionContext.RouteData.Values[LangKey]?.ToString();
        if (string.IsNullOrEmpty(lang) || !SupportedLanguages.Contains(lang))
            lang = "tr";
        
        context.Values[LangKey] = lang;
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        // Admin, Account gibi dil kullanmayan alanlar için sadece varsayılan konuma bak
        var controller = context.ActionContext.ActionDescriptor.RouteValues["controller"];
        if (string.Equals(controller, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase))
        {
            return viewLocations;
        }

        var lang = context.Values[LangKey]?.ToString() ?? "tr";
        
        // Önce dil klasörüne bak: Views/{lang}/Controller/Action.cshtml
        var languageLocations = viewLocations
            .Select(loc => loc.Replace("Views/", $"Views/{lang}/"));
        
        return languageLocations.Concat(viewLocations);
    }
}
