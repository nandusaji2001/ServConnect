using Microsoft.AspNetCore.Mvc;
using ServConnect.Services;

namespace ServConnect.ViewComponents
{
    public class AdvertisementBannerViewComponent : ViewComponent
    {
        private readonly IAdvertisementService _adService;
        public AdvertisementBannerViewComponent(IAdvertisementService adService)
        {
            _adService = adService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var ads = await _adService.GetActiveAsync(10);
            return View(ads);
        }
    }
}