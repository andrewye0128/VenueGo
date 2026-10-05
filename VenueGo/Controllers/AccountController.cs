using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VenueGo.Helpers;
using VenueGo.ViewModels.MemberViewModels;
using VenueGo.Services.Auth;
namespace VenueGo.Controllers
{
    [AllowAnonymous] // 確保登入控制器完全公開，不觸發任何攔截
    public class AccountController : Controller
    {
        private readonly VenueGo.Services.Auth.IAuthenticationService _authenticationService;
        private readonly IPasswordResetService _passwordResetService;
        public AccountController(
            VenueGo.Services.Auth.IAuthenticationService authenticationService,
            IPasswordResetService passwordResetService)

        {
            _authenticationService = authenticationService;
            _passwordResetService = passwordResetService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string ipAddress = GetClientIpAddress();

            // Email 的正規化（Trim、不分大小寫）與帳號比對，全部由 AuthenticationService 負責
            var loginResult = await _authenticationService.LoginAsync(
                model.Email,
                model.Password,
                 ipAddress
            );
            if (!loginResult.Success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    loginResult.ErrorMessage ?? "登入失敗。"
                );

                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    loginResult.UserId!.Value.ToString()
                ),

                new Claim(
                    ClaimTypes.Name,
                    loginResult.UserName ?? ""
                ),

                new Claim(
                    ClaimTypes.Email,
                    loginResult.Email ?? ""
                ),

                new Claim(
                    ClaimsPrincipalExtensions.EmployeeIdClaimType,
                    loginResult.EmployeeId!.Value.ToString()
                ),

                new Claim(
                    "EmployeeNo",
                    loginResult.EmployeeNo ?? ""
                )
            };

            foreach (var role in loginResult.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            //TempData["SuccessMessage"] = $"歡迎回來，{user.Name}！";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            //TempData["SuccessMessage"] = $"歡迎回來，{user.Name}！";
            return RedirectToAction("Index", "Home");

        }


        // IpAddress 欄位（LoginLogs、PasswordResetTokens）長度上限 45，剛好容納一個完整的 IPv6 文字位址
        private const int MaxIpAddressLength = 45;

        // [安全/穩定性修正] 原本直接信任 X-Forwarded-For 標頭：
        // (1) 這個標頭完全由用戶端決定，任何人都能偽造，登入紀錄裡的 IP 因此不可信。
        // (2) 標頭可以帶多個 IP、或被刻意塞很長，但資料庫欄位只有 45 字元，
        //     寫入登入紀錄 / 重設密碼紀錄時會直接丟例外，使用者看到的是 500 錯誤頁。
        // 改用伺服器實際看到的連線位址。若日後部署在反向代理（Nginx、IIS ARR…）後面，
        // 請在 Program.cs 設定 UseForwardedHeaders 並指定 KnownProxies，
        // 這裡的 RemoteIpAddress 就會是還原後的真實 IP，不需要再改這個方法。
        private string GetClientIpAddress()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            return ip.Length > MaxIpAddressLength ? ip[..MaxIpAddressLength] : ip;
        }
        // -------------------------------------------------------------
        // 1. 忘記密碼 - 顯示輸入 Email 頁面
        // -------------------------------------------------------------
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // -------------------------------------------------------------
        // 2. 忘記密碼 - 產生 Token 並寫入 PasswordResetTokens 表格
        // -------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string ipAddress = GetClientIpAddress();

            var rawToken = await _passwordResetService.CreateResetTokenAsync(
                model.Email,
                ipAddress
            );

            if (rawToken != null)
            {
                string resetLink = Url.Action(
                    "ResetPassword",
                    "Account",
                    new
                    {
                        token = rawToken,
                        email = model.Email
                    },
                    Request.Scheme
                ) ?? "";

                TempData["DevResetLink"] = resetLink;
            }

            // 不論 Email 是否存在，都使用相同訊息
            // 避免帳號列舉攻擊
            TempData["SuccessMessage"] =
                "重置密碼申請已送出！請點擊下方的模擬連結進行密碼重置。";

            return RedirectToAction(nameof(ForgotPassword));
        }

        // -------------------------------------------------------------
        // 3. 重置密碼 - 驗證連結 Token 是否有效
        // -------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token, string email)
        {
            if (string.IsNullOrEmpty(token) ||
       string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "無效的重置連結。";
                return RedirectToAction(nameof(Login));
            }

            var isValid = await _passwordResetService.ValidateResetTokenAsync(
                email,
                token
            );

            if (!isValid)
            {
                TempData["ErrorMessage"] =
                    "重置密碼連結已過期或已被使用，請重新申請。";

                return RedirectToAction(nameof(Login));
            }

            var model = new ResetPasswordViewModel
            {
                Token = token,
                Email = email
            };

            return View(model);
        }

        // -------------------------------------------------------------
        // 4. 重置密碼 - 寫入新密碼並將 Token 標示為已使用
        // -------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var success = await _passwordResetService.ResetPasswordAsync(
                model.Email,
                model.Token,
                model.Password
            );

            if (!success)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "重置密碼連結已過期或已被使用，請重新申請。"
                );

                return View(model);
            }

            TempData["SuccessMessage"] =
                "密碼重置成功！請使用新密碼重新登入。若帳號仍在鎖定中，請等鎖定時間結束後再登入。";

            return RedirectToAction(nameof(Login));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            TempData["SuccessMessage"] = "您已成功登出系統。";
            return RedirectToAction("Login", "Account");
        }
    }
}