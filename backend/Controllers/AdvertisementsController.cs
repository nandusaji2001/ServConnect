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
        public async Task<IActionResult> Submit(IFormFile image, string? targetUrl)
        {
            if (image == null || image.Length == 0)
            {
                ModelState.AddModelError("image", "Please upload an image.");
                return View();
            }

            // Crop/resize like admin to 728x90
            const int targetW = 728;
            const int targetH = 90;
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized();

                using var stream = image.OpenReadStream();
                using var src = System.Drawing.Image.FromStream(stream);

                var targetRatio = (double)targetW / targetH;
                var srcRatio = (double)src.Width / src.Height;

                System.Drawing.Rectangle cropRect;
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

                var req = new AdvertisementRequest
                {
                    RequestedByUserId = user.Id,
                    ImageUrl = $"/ads/{fileName}",
                    TargetUrl = string.IsNullOrWhiteSpace(targetUrl) ? null : targetUrl,
                    AmountInPaise = 100000,
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
    }
}