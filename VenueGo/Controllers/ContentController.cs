using Microsoft.AspNetCore.Mvc;
using VenueGo.Data;
using VenueGo.Dtos;
using VenueGo.Filters;
using VenueGo.Constants;
using VenueGo.Models.ContentModels;
using VenueGo.Services;
using VenueGo.Services.Auth;
using VenueGo.ViewModels.ContentViewModels;

namespace VenueGo.Controllers
{
    [EmployeeAuthorizeFilters(RoleNames.Admin, RoleNames.Manager)]//權限過濾 >> 只有 Admin、Manager 可以進入這個 Controller
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
        //CoverSource:封面來源 default(分類預設圖)/ upload(上傳圖片)/ template(公版底稿)
        //CoverImageData:公版底稿在瀏覽器合成好的 PNG(base64 文字)
        public async Task<IActionResult> NewsCreate(CNewsWrap wrap, IFormFile? ImageFile, string? CoverSource, string? CoverImageData)
        {
            //驗證分類清單項目是否來自清單內
            if (!NewsCategoryOptions.IsValid(wrap.Category))
            {
                //回傳錯誤訊息到View
                ModelState.AddModelError("Category", "請從清單中選擇分類");
            }

            //驗證封面:依來源各自檢查
            //   上傳圖片 >> 檢查格式與大小(沒上傳不算錯,會改用分類預設圖)
            //   公版底稿 >> 檢查內文、樣式與合成圖
            //   其他來源不用上傳的檔案 >> 就算表單夾帶也不使用
            if (CoverSource == "upload")
            {
                string? imageError = ValidateNewsImage(ImageFile);
                if (imageError != null)
                {
                    ModelState.AddModelError("ImageFile", imageError);
                }
            }
            else
            {
                ImageFile = null;
            }

            byte[]? coverPng = null;
            if (CoverSource == "template")
            {
                coverPng = ValidateCoverTemplate(wrap, CoverImageData);
            }
            else
            {
                //不是公版底稿就不使用封面內文 >> 移除它的驗證結果(錯誤會顯示在隱藏的區塊,使用者看不到)
                ModelState.Remove("CoverText");
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

            //封面
            //   公版底稿 >> 存合成圖,標題由分類決定(ValidateCoverTemplate 已整理好內文與樣式)
            //   其他 >> 有上傳就用上傳的圖,沒上傳就用分類預設圖;Cover 欄位全部清空
            if (coverPng != null)
            {
                wrap.ImagePath = await SaveCoverPngAsync(coverPng);
                wrap.CoverTitle = NewsCategoryOptions.GetName(wrap.Category);
                wrap.CoverTitleStyles = NewsCoverOptions.TitleStyles;
            }
            else
            {
                string? uploadedPath = await SaveNewsImageAsync(ImageFile);
                wrap.ImagePath = uploadedPath ?? NewsCategoryOptions.GetDefaultImagePath(wrap.Category);
                ClearCoverFields(wrap);
            }

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

            //原本的分類 >> 封面是公版底稿時,換分類要提醒重新產生封面
            ViewData["OriginalCategory"] = wrap.Category;
            return View(wrap);
        }


        //最新消息編輯 >> 頁面傳回
        [HttpPost]
        //CoverSource:封面來源 keep(保留目前封面)/ upload(上傳圖片)/ template(公版底稿)
        //CoverImageData:公版底稿在瀏覽器合成好的 PNG(base64 文字)
        public async Task<IActionResult> NewsEdit(CNewsWrap wrap, IFormFile? ImageFile, string? CoverSource, string? CoverImageData)
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

            //2. 驗證:分類在清單內;封面依來源各自檢查(同新增)
            if (!NewsCategoryOptions.IsValid(wrap.Category))
            {
                ModelState.AddModelError("Category", "請從清單中選擇分類");
            }

            if (CoverSource == "upload")
            {
                string? imageError = ValidateNewsImage(ImageFile);
                if (imageError != null)
                {
                    ModelState.AddModelError("ImageFile", imageError);
                }
            }
            else
            {
                ImageFile = null;
            }

            byte[]? coverPng = null;
            if (CoverSource == "template")
            {
                coverPng = ValidateCoverTemplate(wrap, CoverImageData);
            }
            else
            {
                //不是公版底稿就不使用封面內文 >> 移除它的驗證結果(錯誤會顯示在隱藏的區塊,使用者看不到)
                ModelState.Remove("CoverText");
            }

            if (!ModelState.IsValid)
            {
                //表單沒有送封面路徑與標題 >> 從原本的資料補回來,畫面才能顯示目前的封面
                wrap.ImagePath = original.ImagePath;
                wrap.CoverTitle = original.CoverTitle;
                //不是公版底稿時表單沒有送內文 >> 補回原本的,編輯器才能還原
                if (CoverSource != "template")
                {
                    wrap.CoverText = original.CoverText;
                    wrap.CoverTextStyles = original.CoverTextStyles;
                }
                ViewData["OriginalCategory"] = original.Category;
                return View(wrap);
            }

            //3. 取得登入者
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["NewsErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToAction("NewsIndex");
            }

            //4. 決定新的封面(newImagePath = null 表示不換圖)
            //   公版底稿 >> 存新的合成圖,Cover 欄位換成這次的內容
            //   有上傳 >> 用新上傳的圖,Cover 欄位清空
            //   都沒有、換了分類、原本用的是預設圖 >> 改成新分類的預設圖(自己上傳的圖、公版底稿保留不動)
            string? savedPath = null;
            bool replaceCover = false;
            if (coverPng != null)
            {
                savedPath = await SaveCoverPngAsync(coverPng);
                wrap.CoverTitle = NewsCategoryOptions.GetName(wrap.Category);
                wrap.CoverTitleStyles = NewsCoverOptions.TitleStyles;
                replaceCover = true;
            }
            else
            {
                savedPath = await SaveNewsImageAsync(ImageFile);
                if (savedPath != null)
                {
                    ClearCoverFields(wrap);
                    replaceCover = true;
                }
            }

            string? newImagePath = savedPath;
            if (newImagePath == null
                && wrap.Category != original.Category
                && IsDefaultNewsImage(original.ImagePath))
            {
                newImagePath = NewsCategoryOptions.GetDefaultImagePath(wrap.Category);
            }

            //5. 存檔
            bool isEdited = newsFactory.Edit(wrap, newImagePath, replaceCover, userId.Value, _timeService.Now);
            if (!isEdited)
            {
                //存檔失敗(剛好被別人刪除) >> 這次存的圖沒有人使用,刪掉
                DeleteUploadedNewsImage(savedPath);
                TempData["NewsErrorMessage"] = "儲存失敗,該則消息已被其他人刪除";
                return RedirectToAction("NewsIndex");
            }

            //6. 換了新圖(上傳圖或公版合成圖) >> 舊圖已經沒有人使用,刪掉(預設圖不會被刪)
            if (savedPath != null)
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


        /*常見問題*/
        //每個 Action 都帶著 returnCategory(使用者原本停在哪個分類頁籤),處理完導回同一個頁籤

        //常見問題列表(不分頁,依分類分組)
        public IActionResult FaqIndex(string? category)
        {
            FaqIndexViewModel vm = new CFaqFactory().QueryAdminList(category);
            return View(vm);
        }


        //常見問題新增 >> 頁面產生
        //在單一分類頁籤按新增 >> 分類預設為該分類;在「全部」頁籤 >> 不預設,由管理者自己選
        [HttpGet]
        public IActionResult FaqCreate(string? returnCategory)
        {
            CFaqWrap wrap = new CFaqWrap();
            if (FaqCategoryOptions.IsValid(returnCategory))
            {
                wrap.Category = returnCategory!;
            }
            //新增時預設顯示
            wrap.IsPublished = true;

            ViewData["ReturnCategory"] = returnCategory;
            return View(wrap);
        }


        //常見問題新增 >> 頁面傳回
        [HttpPost]
        public IActionResult FaqCreate(CFaqWrap wrap, string? returnCategory)
        {
            //驗證分類在清單內(只擋空值的 [Required] 擋不了被竄改的代碼)
            if (!string.IsNullOrEmpty(wrap.Category) && !FaqCategoryOptions.IsValid(wrap.Category))
            {
                ModelState.AddModelError("Category", "請從清單中選擇分類");
            }
            //回答裡的連結網址不合法(例如 www.、http://、javascript:)就擋下;全形括號只在預覽提醒,不擋
            AddFaqAnswerErrors(wrap.Answer);

            if (!ModelState.IsValid)
            {
                ViewData["ReturnCategory"] = returnCategory;
                return View(wrap);
            }

            //取得登入者:先檢查 null 再使用
            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["FaqErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToFaqIndex(returnCategory);
            }

            //後端決定的值:SortOrder 由 Factory 決定(排在該分類最後)
            wrap.IsActive = true;
            wrap.CreatedAt = _timeService.Now;
            wrap.CreatedBy = userId.Value;
            wrap.UpdatedAt = null;
            wrap.UpdatedBy = null;

            new CFaqFactory().Create(wrap);

            TempData["FaqSuccessMessage"] = "已新增常見問題";
            return RedirectToFaqIndex(returnCategory);
        }


        //常見問題編輯 >> 頁面產生
        [HttpGet]
        public IActionResult FaqEdit(int? id, string? returnCategory)
        {
            if (id == null)
            {
                return RedirectToFaqIndex(returnCategory);
            }

            CFaqWrap? wrap = new CFaqFactory().QueryById((int)id);
            if (wrap == null)
            {
                TempData["FaqErrorMessage"] = "找不到這則問題,可能已經被刪除";
                return RedirectToFaqIndex(returnCategory);
            }

            ViewData["ReturnCategory"] = returnCategory;
            return View(wrap);
        }


        //常見問題編輯 >> 頁面傳回
        [HttpPost]
        public IActionResult FaqEdit(CFaqWrap wrap, string? returnCategory)
        {
            if (!string.IsNullOrEmpty(wrap.Category) && !FaqCategoryOptions.IsValid(wrap.Category))
            {
                ModelState.AddModelError("Category", "請從清單中選擇分類");
            }
            //回答裡的連結網址不合法(例如 www.、http://、javascript:)就擋下;全形括號只在預覽提醒,不擋
            AddFaqAnswerErrors(wrap.Answer);

            if (!ModelState.IsValid)
            {
                ViewData["ReturnCategory"] = returnCategory;
                return View(wrap);
            }

            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["FaqErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToFaqIndex(returnCategory);
            }

            //換分類時,Factory 會把它排到新分類的最後
            bool isEdited = new CFaqFactory().Edit(wrap, userId.Value, _timeService.Now);
            if (isEdited)
            {
                TempData["FaqSuccessMessage"] = "已儲存常見問題";
            }
            else
            {
                TempData["FaqErrorMessage"] = "儲存失敗,該則問題已被其他人刪除";
            }

            return RedirectToFaqIndex(returnCategory);
        }


        //常見問題刪除(軟刪除)
        [HttpPost]
        public IActionResult FaqDelete(int? FaqId, string? returnCategory)
        {
            if (FaqId == null)
            {
                return RedirectToFaqIndex(returnCategory);
            }

            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["FaqErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToFaqIndex(returnCategory);
            }

            bool isDeleted = new CFaqFactory().Delete((int)FaqId, userId.Value, _timeService.Now);
            if (isDeleted)
            {
                TempData["FaqSuccessMessage"] = "該則問題已成功刪除";
            }
            else
            {
                TempData["FaqErrorMessage"] = "刪除失敗,該則問題已被其他人刪除";
            }

            return RedirectToFaqIndex(returnCategory);
        }


        //常見問題 顯示/隱藏 切換
        [HttpPost]
        public IActionResult FaqTogglePublished(int? FaqId, string? returnCategory)
        {
            if (FaqId == null)
            {
                return RedirectToFaqIndex(returnCategory);
            }

            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["FaqErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToFaqIndex(returnCategory);
            }

            bool isToggled = new CFaqFactory().TogglePublished((int)FaqId, userId.Value, _timeService.Now);
            if (!isToggled)
            {
                TempData["FaqErrorMessage"] = "切換失敗,該則問題已被其他人刪除";
            }

            //成功時不放訊息:列表上的狀態標籤已經變了,畫面本身就看得出來
            return RedirectToFaqIndex(returnCategory);
        }


        //常見問題 上移/下移
        //direction:"up" = 上移,其他 = 下移
        [HttpPost]
        public IActionResult FaqMove(int? FaqId, string? direction, string? returnCategory)
        {
            if (FaqId == null)
            {
                return RedirectToFaqIndex(returnCategory);
            }

            int? userId = _currentUser.UserId;
            if (userId == null)
            {
                TempData["FaqErrorMessage"] = "無法取得登入者資訊,請重新登入。";
                return RedirectToFaqIndex(returnCategory);
            }

            bool moveUp = direction == "up";
            bool isMoved = new CFaqFactory().Move((int)FaqId, moveUp, userId.Value, _timeService.Now);
            if (!isMoved)
            {
                //已經在最上面/最下面(按鈕本來就停用),或是剛好被別人刪除
                TempData["FaqErrorMessage"] = "無法移動這則問題,請重新整理後再試";
            }

            return RedirectToFaqIndex(returnCategory);
        }


        //常見問題回答預覽(AJAX) >> 新增/編輯頁輸入回答時呼叫,回傳解析後的片段、錯誤、提醒
        //只解析傳進來的文字,不讀寫資料庫
        [HttpPost]
        public IActionResult FaqPreview(string? answer)
        {
            FaqAnswerParseResult result = FaqAnswerParser.Parse(answer);
            return Json(ApiResult<FaqAnswerParseResult>.Ok(result));
        }


        /*共用私有方法*/
        //回答的連結錯誤加進 ModelState(顯示在回答欄位下方)
        private void AddFaqAnswerErrors(string? answer)
        {
            foreach (string error in FaqAnswerParser.Parse(answer).Errors)
            {
                ModelState.AddModelError("Answer", error);
            }
        }

        //導回常見問題列表,停在原本的分類頁籤;分類不在清單內就回到「全部」
        private RedirectToActionResult RedirectToFaqIndex(string? category)
        {
            string? validCategory = FaqCategoryOptions.IsValid(category) ? category : null;
            return RedirectToAction("FaqIndex", new { category = validCategory });
        }

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

            using (var stream = new FileStream(Path.Combine(GetNewsUploadFolder(), fileName), FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            //資料庫只存網頁路徑,不存實際的磁碟路徑
            return "/images/news/uploads/" + fileName;
        }

        //上傳圖與公版合成圖的實際存放位置:wwwroot/images/news/uploads(資料夾不存在就建立)
        private string GetNewsUploadFolder()
        {
            string folderPath = Path.Combine(_env.WebRootPath, "images", "news", "uploads");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            return folderPath;
        }

        //公版底稿的驗證:內文、樣式、合成圖 >> 有錯就加進 ModelState("CoverText")
        //通過時:wrap.CoverTextStyles 換成整理過的 JSON,回傳合成圖的位元組;不通過回傳 null
        private byte[]? ValidateCoverTemplate(CNewsWrap wrap, string? imageData)
        {
            //1. 內文不可空白(字數上限由 CNewsWrap 的 [StringLength] 檢查)
            if (string.IsNullOrWhiteSpace(wrap.CoverText))
            {
                ModelState.AddModelError("CoverText", "公版底稿的封面內文不可空白");
                return null;
            }

            //2. 樣式:範圍、顏色、字級
            string? styleError = NewsCoverOptions.ValidateStyles(wrap.CoverText, wrap.CoverTextStyles, out string normalizedJson);
            if (styleError != null)
            {
                ModelState.AddModelError("CoverText", styleError);
                return null;
            }
            wrap.CoverTextStyles = normalizedJson;

            //3. 合成圖
            byte[]? png = DecodeCoverPng(imageData);
            if (png == null)
            {
                ModelState.AddModelError("CoverText", "封面圖片產生失敗,請重新整理頁面後再試一次");
                return null;
            }
            return png;
        }

        //合成圖上限 3MB(1200 × 1500 的純色底稿＋文字,實際約幾百 KB)
        private const int MaxCoverPngBytes = 3 * 1024 * 1024;

        //把瀏覽器送來的 data URL 轉回 PNG 位元組;格式、大小、尺寸不對就回傳 null
        private static byte[]? DecodeCoverPng(string? imageData)
        {
            //1. 開頭一定是 canvas.toDataURL 產生的 PNG 格式
            const string prefix = "data:image/png;base64,";
            if (imageData == null || !imageData.StartsWith(prefix))
            {
                return null;
            }

            //2. base64 轉回位元組
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(imageData.Substring(prefix.Length));
            }
            catch (FormatException)
            {
                return null;
            }
            if (bytes.Length > MaxCoverPngBytes || bytes.Length < 24)
            {
                return null;
            }

            //3. 檔頭:PNG 檔案固定以這 8 個位元組開頭
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            if (!bytes.Take(8).SequenceEqual(signature))
            {
                return null;
            }

            //4. 尺寸:PNG 第 16～23 個位元組是寬、高(大端序)>> 一定要是 1200 × 1500
            int width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            int height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
            if (width != 1200 || height != 1500)
            {
                return null;
            }

            return bytes;
        }

        //存公版合成圖 >> 回傳網頁路徑
        private async Task<string> SaveCoverPngAsync(byte[] png)
        {
            string fileName = Guid.NewGuid() + ".png";
            await System.IO.File.WriteAllBytesAsync(Path.Combine(GetNewsUploadFolder(), fileName), png);
            return "/images/news/uploads/" + fileName;
        }

        //不是公版底稿的封面 >> 4 個 Cover 欄位都要是 null(資料庫 CK_News_CoverAllOrNone)
        private static void ClearCoverFields(CNewsWrap wrap)
        {
            wrap.CoverTitle = null;
            wrap.CoverTitleStyles = null;
            wrap.CoverText = null;
            wrap.CoverTextStyles = null;
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
