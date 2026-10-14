using Microsoft.AspNetCore.Mvc;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Constants;
using VenueGo.Models.ContentModels;
using VenueGo.Services;
using VenueGo.Services.Auth;
using VenueGo.ViewModels.ContentViewModels;

namespace VenueGo.Controllers
{
    [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager)]//權限過濾 >> 只有 Admin、Manager 可以進入這個 Controller
    public class ContentController : Controller
    {

        /*類別通用常數*/
        //每頁資料筆數
        private const int NewsPageSize = 10;

        //封面圖片允許的副檔名(一律比對小寫)與大小上限(5MB)
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxImageBytes = 5 * 1024 * 1024;


        /*依賴注入*/
        //Controller建構子注入 IWebHostEnvironment,用來取得專案根目錄的實體路徑
        private readonly IWebHostEnvironment _env;

        //取得校時後的系統時間(取代 DateTime.Now,Controller建構子注入)
        private readonly ITimeService _timeService;
        //取得目前登入者資訊(Controller建構子注入)
        private readonly ICurrentUserService _currentUser;

        //統一注入到建構子
        public ContentController(IWebHostEnvironment env, ITimeService timeService, ICurrentUserService currentUser)
        {
            _env = env;
            _timeService = timeService;
            _currentUser = currentUser;
        }


        /*最新消息*/
        //最新消息列表
        public IActionResult NewsIndex(string? category,int page = 1)
        {
            //查出所有最新消息並存回ViewModel
            CNewsFactory newsFactory = new CNewsFactory();
            var vm = newsFactory.QueryAdminPage(category,page, NewsPageSize,_timeService.Now);

            //傳到 View
            return View(vm);
        }


        //最新消息新增 >> 頁面產生
        [HttpGet]
        public IActionResult NewsCreate()
        {
            //產生一個空白Wrap傳到View使用
            CNewsWrap wrap = new CNewsWrap();
            //發布消息預設值設為現在
            wrap.PublishedAt = _timeService.Now;
            //轉移到NewsCreate的View
            return View(wrap);
        }



        //最新消息新增 >> 頁面傳回
        [HttpPost]
        public async Task<IActionResult> NewsCreate(CNewsWrap wrap, IFormFile? ImageFile)
        {
            //驗證分類清單項目是否來自清單內
            if (!NewsCategoryOptions.IsValid(wrap.Category))
            {
                //回傳錯誤訊息到View
                ModelState.AddModelError("Category", "請從清單中選擇分類");
            }

            //驗證上傳的封面圖片(沒上傳不算錯,會改用分類預設圖)
            string? imageError = ValidateNewsImage(ImageFile);
            if (imageError != null)
            {
                ModelState.AddModelError("ImageFile", imageError);
            }

            //驗證資料是否正確填寫 >> 若填寫不正確，則回到原本頁面並顯示錯誤訊息
            if (!ModelState.IsValid)
            {
                return View(wrap);
            }


            //驗證是否有登入if (UserId == null)
            int? UserId = _currentUser.UserId; //取得當前登入者id
            if (UserId == null)
            {
                TempData["NewsErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("NewsIndex");
            }

            //設定非填寫欄位的值
            wrap.CreatedBy = (int)UserId;
            wrap.CreatedAt = _timeService.Now;
            wrap.IsActive = true;
            wrap.UpdatedAt = null;
            wrap.UpdatedBy = null;
            wrap.CoverTextStyles = null;
            wrap.CoverText = null;
            wrap.CoverTitle = null;
            wrap.CoverTitleStyles = null;

            //封面:有上傳就存檔並使用上傳的圖,沒上傳就使用該分類的預設圖
            string? uploadedPath = await SaveNewsImageAsync(ImageFile);
            wrap.ImagePath = uploadedPath ?? NewsCategoryOptions.GetDefaultImagePath(wrap.Category);

            //呼叫Factory執行新增
            CNewsFactory newsFactory = new CNewsFactory();
            newsFactory.Create(wrap);
            //顯示成功訊息
            TempData["NewsSuccessMessage"] = "已新增最新消息";

            //回到最新消息列表頁面
            return RedirectToAction("NewsIndex");
        }


        //最新消息編輯 >> 頁面產生
        [HttpGet]
        public IActionResult NewsEdit(int? id)
        {
            //沒有帶 Id >> 回列表
            if (id == null)
            {
                return RedirectToAction("NewsIndex");
            }

            //查出原本的資料;找不到或已刪除 >> 回列表並顯示訊息
            CNewsWrap? wrap = new CNewsFactory().QueryById((int)id);
            if (wrap == null)
            {
                TempData["NewsErrorMessage"] = "找不到這則消息,可能已經被刪除";
                return RedirectToAction("NewsIndex");
            }

            return View(wrap);
        }


        //最新消息編輯 >> 頁面傳回
        [HttpPost]
        public async Task<IActionResult> NewsEdit(CNewsWrap wrap, IFormFile? ImageFile)
        {
            //1. 先查出資料庫原本那一筆 >> 判斷換圖要用原本的分類與封面路徑
            //   不用 Wrap 的值:Wrap 直接接表單,ImagePath 不在表單上,而且可能被竄改
            CNewsFactory newsFactory = new CNewsFactory();
            CNewsWrap? original = newsFactory.QueryById(wrap.NewsId);
            if (original == null)
            {
                TempData["NewsErrorMessage"] = "找不到這則消息,可能已經被刪除";
                return RedirectToAction("NewsIndex");
            }

            //2. 驗證:分類在清單內、上傳的圖片格式與大小
            if (!NewsCategoryOptions.IsValid(wrap.Category))
            {
                ModelState.AddModelError("Category", "請從清單中選擇分類");
            }

            string? imageError = ValidateNewsImage(ImageFile);
            if (imageError != null)
            {
                ModelState.AddModelError("ImageFile", imageError);
            }

            if (!ModelState.IsValid)
            {
                //表單沒有送封面路徑 >> 從原本的資料補回來,畫面才能顯示目前的封面
                wrap.ImagePath = original.ImagePath;
                return View(wrap);
            }

            //3. 取得登入者
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["NewsErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("NewsIndex");
            }

            //4. 決定新的封面路徑(null = 不換)
            //   有上傳 >> 用新上傳的圖
            //   沒上傳、換了分類、原本用的是預設圖 >> 改成新分類的預設圖(自己上傳的圖保留不動)
            string? uploadedPath = await SaveNewsImageAsync(ImageFile);
            string? newImagePath = uploadedPath;
            if (newImagePath == null
                && wrap.Category != original.Category
                && IsDefaultNewsImage(original.ImagePath))
            {
                newImagePath = NewsCategoryOptions.GetDefaultImagePath(wrap.Category);
            }

            //5. 存檔
            bool isEdited = newsFactory.Edit(wrap, newImagePath, userId.Value, _timeService.Now);
            if (!isEdited)
            {
                //存檔失敗(剛好被別人刪除) >> 這次上傳的圖沒有人使用,刪掉
                DeleteUploadedNewsImage(uploadedPath);
                TempData["NewsErrorMessage"] = "儲存失敗,該則消息已被其他人刪除";
                return RedirectToAction("NewsIndex");
            }

            //6. 換了上傳圖 >> 舊的上傳圖已經沒有人使用,刪掉(預設圖不會被刪)
            if (uploadedPath != null)
            {
                DeleteUploadedNewsImage(original.ImagePath);
            }

            TempData["NewsSuccessMessage"] = "已儲存最新消息";
            return RedirectToAction("NewsIndex");
        }

        //最新消息刪除(軟刪除)
        [HttpPost]
        public IActionResult NewsDelete(int? NewsId)
        {
            //取得當前登入者id
            int? userId = _currentUser.UserId;

            //檢查newsId是否為null
            if (NewsId == null)
            {
                return RedirectToAction("NewsIndex");
            }

            //檢查UserId是否為null
            if (userId == null)
            {
                TempData["NewsErrorMessage"] = "無法取得登入者資訊";
                return RedirectToAction("NewsIndex");
            }

            //呼叫Factory執行刪除
            CNewsFactory newsFactory = new CNewsFactory();
            bool isDelete = newsFactory.Delete((int)NewsId, (int)userId    , _timeService.Now);

            if (isDelete) 
            {
                TempData["NewsSuccessMessage"] = "該則消息已成功刪除";
            }
            else
            {
                TempData["NewsErrorMessage"] = "刪除失敗，該則消息已被其他人刪除";
            }
                return RedirectToAction("NewsIndex");


        }


        /*共用私有方法*/
        //驗證封面圖片 >> 回傳錯誤訊息;沒有上傳或檢查通過回傳 null
        private static string? ValidateNewsImage(IFormFile? imageFile)
        {
            //沒有上傳不算錯誤 >> 會改用分類預設圖
            if (imageFile == null || imageFile.Length == 0)
            {
                return null;
            }

            //副檔名轉小寫再比對,避免 .JPG、.Png 被擋下
            string extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(extension))
            {
                return "封面圖片只接受 jpg、png、webp 格式";
            }

            if (imageFile.Length > MaxImageBytes)
            {
                return "封面圖片不可超過 5MB";
            }

            return null;
        }

        //存最新消息封面圖片 >> 回傳網頁相對路徑;沒有檔案回傳 null
        //必須先通過 ValidateNewsImage 檢查再呼叫
        private async Task<string?> SaveNewsImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return null;
            }

            //檔名用 Guid 重新命名 >> 避免同名檔案互相覆蓋,也不使用上傳者自訂的檔名
            string fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName).ToLowerInvariant();

            //實際存放位置:wwwroot/images/news/uploads(資料夾不存在就建立)
            string folderPath = Path.Combine(_env.WebRootPath, "images", "news", "uploads");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            using (var stream = new FileStream(Path.Combine(folderPath, fileName), FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            //資料庫只存網頁路徑,不存實際的磁碟路徑
            return "/images/news/uploads/" + fileName;
        }

        //判斷封面是不是分類預設圖
        private static bool IsDefaultNewsImage(string? imagePath)
        {
            return imagePath != null && imagePath.StartsWith("/images/news/defaults/");
        }

        //刪除上傳的封面圖檔 >> 只刪 uploads 資料夾的檔案,預設圖、null 都不處理
        private void DeleteUploadedNewsImage(string? imagePath)
        {
            if (imagePath == null || !imagePath.StartsWith("/images/news/uploads/"))
            {
                return;
            }

            //只取檔名再組回 uploads 資料夾 >> 避免路徑裡夾帶 ../ 刪到其他資料夾的檔案
            string fileName = Path.GetFileName(imagePath);
            string filePath = Path.Combine(_env.WebRootPath, "images", "news", "uploads", fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}
