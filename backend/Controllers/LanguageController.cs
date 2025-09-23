using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ServConnect.Controllers
{
    // Handles switching UI culture via cookie and redirecting back
    public class LanguageController : Controller
    {
        [HttpGet]
        public IActionResult Set(string culture = "en", string returnUrl = "/")
        {
            if (string.IsNullOrWhiteSpace(returnUrl)) returnUrl = "/";

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
            );

            if (!Url.IsLocalUrl(returnUrl))
            {
                returnUrl = Url.Action("Index", "Home") ?? "/";
            }

            return LocalRedirect(returnUrl);
        }
    }
}