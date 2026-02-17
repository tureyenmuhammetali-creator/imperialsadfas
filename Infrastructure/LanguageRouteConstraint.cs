using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ImperialVip.Infrastructure;

/// <summary>
/// Route'da sadece geçerli dil kodlarını kabul eder: tr, de, ru, en
/// </summary>
public class LanguageRouteConstraint : IRouteConstraint
{
    private static readonly Regex LangPattern = new($"^({string.Join("|", LanguageViewLocationExpander.SupportedLanguages)})$");

    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, 
        RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var value))
            return true; // Parametre yoksa (opsiyonel) kabul et

        if (value == null || string.IsNullOrEmpty(value.ToString()))
            return true; // Boş = varsayılan dil

        var lang = value.ToString();
        return LangPattern.IsMatch(lang);
    }
}
