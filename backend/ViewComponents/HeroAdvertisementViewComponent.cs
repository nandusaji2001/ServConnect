using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.ViewComponents
{
    public class HeroAdvertisementViewComponent : ViewComponent
    {
        private readonly IAdvertisementService _adService;
        
        public HeroAdvertisementViewComponent(IAdvertisementService adService)
        {
            _adService = adService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var heroAds = await _adService.GetActiveByTypeAsync(AdvertisementType.HeroBanner, 10);
            return View(heroAds);
        }
    }
}
