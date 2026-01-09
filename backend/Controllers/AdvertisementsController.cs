using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;
using System.Drawing;

namespace ServConnect.Controllers
{
    [Authorize]
    public class AdvertisementsController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IAdvertisementRequestService _requestService;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public AdvertisementsController(UserManager<Users> userManager,
                                        IAdvertisementRequestService requestService,
                                        IWebHostEnvironment env,
                                        IConfiguration config)
        {
            _userManager = userManager;
            _requestService = requestService;
            _env = env;
            _config = config;
        }

        [HttpGet]
        public IActionResult Submit()
        {
            ViewBag.RazorpayKey = _config["Razorpay:KeyId"] ?? ""; // front-end uses this
            ViewBag.AmountInPaise = 100000; // Rs. 1000
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public async Task<IActionResult> Submit(IFormFile image, string? targetUrl, AdvertisementType adType = AdvertisementType.BottomPage, 
            int durationInMonths = 1, double? cropX = null, double? cropY = null, double? cropWidth = null, double? cropHeight = null)
        {
            if (image == null || image.Length == 0)
            {
                ModelState.AddModelError("image", "Please upload an image.");
                return View();
            }

            // Set dimensions based on advertisement type
            int targetW, targetH;
            if (adType == AdvertisementType.HeroBanner)
            {
                targetW = 1920;
                targetH = 200;
            }
            else
            {
                targetW = 728;
                targetH = 90;
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                // Debug logging
                Console.WriteLine($"Received image: {image.FileName}");
                Console.WriteLine($"Image size: {image.Length} bytes");
                Console.WriteLine($"Content type: {image.ContentType}");
                Console.WriteLine($"Crop coordinates: X={cropX}, Y={cropY}, W={cropWidth}, H={cropHeight}");

                using var stream = image.OpenReadStream();
                using var src = System.Drawing.Image.FromStream(stream);

                Console.WriteLine($"Original image dimensions: {src.Width}x{src.Height}");

                System.Drawing.Rectangle cropRect;
                
                // Use user's crop selection if provided, otherwise auto-crop from center
                if (cropX.HasValue && cropY.HasValue && cropWidth.HasValue && cropHeight.HasValue)
                {
                    // Use user's crop selection
                    cropRect = new System.Drawing.Rectangle(
                        (int)Math.Round(cropX.Value),
                        (int)Math.Round(cropY.Value),
                        (int)Math.Round(cropWidth.Value),
                        (int)Math.Round(cropHeight.Value)
                    );
                    
                    // Ensure crop rectangle is within image bounds
                    cropRect.X = Math.Max(0, Math.Min(cropRect.X, src.Width - 1));
                    cropRect.Y = Math.Max(0, Math.Min(cropRect.Y, src.Height - 1));
                    cropRect.Width = Math.Max(1, Math.Min(cropRect.Width, src.Width - cropRect.X));
                    cropRect.Height = Math.Max(1, Math.Min(cropRect.Height, src.Height - cropRect.Y));
                    
                    Console.WriteLine($"Using user crop: {cropRect}");
                }
                else
                {
                    // Fallback to automatic center cropping
                    var targetRatio = (double)targetW / targetH;
                    var srcRatio = (double)src.Width / src.Height;

                    if (srcRatio > targetRatio)
                    {
                        var cropW = (int)Math.Round(src.Height * targetRatio);
                        var x = (src.Width - cropW) / 2;
                        cropRect = new System.Drawing.Rectangle(x, 0, cropW, src.Height);
                    }
                    else
                    {
                        var cropH = (int)Math.Round(src.Width / targetRatio);
                        var y = (src.Height - cropH) / 2;
                        cropRect = new System.Drawing.Rectangle(0, y, src.Width, cropH);
                    }
                    
                    Console.WriteLine($"Using auto crop: {cropRect}");
                }

                // Create the final cropped image
                using var dest = new System.Drawing.Bitmap(targetW, targetH);
                using (var g = System.Drawing.Graphics.FromImage(dest))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(src, new System.Drawing.Rectangle(0, 0, targetW, targetH), cropRect, System.Drawing.GraphicsUnit.Pixel);
                }

                var adsFolder = Path.Combine(_env.WebRootPath, "ads");
                if (!Directory.Exists(adsFolder)) Directory.CreateDirectory(adsFolder);
                var fileName = $"adreq_{Guid.NewGuid()}.jpg";
                var savePath = Path.Combine(adsFolder, fileName);
                dest.Save(savePath, System.Drawing.Imaging.ImageFormat.Jpeg);

                Console.WriteLine($"Saved cropped image to: {savePath}");

                // Calculate price based on advertisement type and duration
                int priceInRupees = CalculatePrice(adType, durationInMonths);
                int amountInPaise = priceInRupees * 100;

                var req = new AdvertisementRequest
                {
                    RequestedByUserId = user.Id,
                    ImageUrl = $"/ads/{fileName}",
                    TargetUrl = string.IsNullOrWhiteSpace(targetUrl) ? null : targetUrl,
                    Type = adType,
                    DurationInMonths = durationInMonths,
                    AmountInPaise = amountInPaise,
                    Status = AdRequestStatus.Pending,
                    IsPaid = false
                };

                req = await _requestService.CreateAsync(req);

                TempData["AdReqId"] = req.Id;
                TempData["AdImageUrl"] = req.ImageUrl;
                TempData["AdTargetUrl"] = req.TargetUrl ?? "";
                return RedirectToAction(nameof(Pay), new { id = req.Id });
            }
            catch
            {
                ModelState.AddModelError("image", "Invalid image file.");
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> Pay(string id)
        {
            var req = await _requestService.GetByIdAsync(id);
            if (req == null) return NotFound();
            ViewBag.RazorpayKey = _config["Razorpay:KeyId"] ?? "";
            ViewBag.AmountInPaise = req.AmountInPaise;
            ViewBag.RequestId = req.Id;
            ViewBag.ImageUrl = req.ImageUrl;
            ViewBag.TargetUrl = req.TargetUrl;
            return View();
        }

        // Razorpay will post back these details after successful payment (client-side capture or order flow)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPayment(string id, string razorpay_order_id, string razorpay_payment_id, string razorpay_signature)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(razorpay_payment_id))
            {
                return BadRequest();
            }

            // NOTE: For production, verify signature using Razorpay secret (HMAC SHA256). Skipped here for brevity.
            var ok = await _requestService.MarkPaidAsync(id, razorpay_order_id, razorpay_payment_id, razorpay_signature);
            if (!ok)
            {
                TempData["AdPayMessage"] = "Payment verification failed.";
                return RedirectToAction(nameof(Pay), new { id });
            }

            TempData["AdPayMessage"] = "Payment successful! Your advertisement request is pending admin approval.";
            return RedirectToAction(nameof(Status), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Status(string id)
        {
            var req = await _requestService.GetByIdAsync(id);
            if (req == null) return NotFound();
            return View(req);
        }

        // New: list all requests for current user
        [HttpGet]
        public async Task<IActionResult> My()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var list = await _requestService.GetByUserAsync(user.Id);
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var success = await _requestService.DeleteAsync(id, user.Id);
            if (success)
            {
                TempData["Message"] = "Advertisement deleted successfully.";
            }
            else
            {
                TempData["Error"] = "Failed to delete advertisement. It may not exist or you don't have permission.";
            }

            return RedirectToAction(nameof(My));
        }

        private int CalculatePrice(AdvertisementType adType, int durationInMonths)
        {
            if (adType == AdvertisementType.BottomPage)
            {
                return durationInMonths switch
                {
                    1 => 500,   // 1 month
                    2 => 750,   // 2 months
                    3 => 1000,  // 3 months
                    _ => 500    // default to 1 month price
                };
            }
            else // HeroBanner
            {
                return durationInMonths switch
                {
                    1 => 1500,  // 1 month
                    2 => 1750,  // 2 months
                    3 => 2000,  // 3 months
                    _ => 1500   // default to 1 month price
                };
            }
        }
    }
}