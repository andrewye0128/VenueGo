using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.VenueModels;
using VenueGo.ViewModels;

namespace VenueGo.Controllers
{
    public class VenueController : Controller
    {
        //取得wwwroot的實際路徑(Controller建構子注入)
        private readonly IWebHostEnvironment _env;
        public VenueController(IWebHostEnvironment env)
        {
            _env = env;
        }



        /****SportType****/

        //列出所有運動類型
        public IActionResult SportTypeIndex()
        {
            List<CSportTypeWrap> datas = (new CSportTypeFactory()).QueryAll();
            return View(datas);
        }

        //新增運動類型 >> 頁面產生
        public IActionResult SportTypeCreate()
        {
            return View();
        }


        //新增運動類型 >> 資料回傳存入DB
        [HttpPost]
        public IActionResult SportTypeCreate(CSportTypeWrap Wrap)
        {
            //先檢查填寫是否通過
            if (!ModelState.IsValid)
            {
                return View(Wrap);
            }

            //設定暫時預設欄位值 >> 要回來改
            Wrap.CreatedBy = 1; //尚未整合會員ID追蹤,先給1
            Wrap.IsActive = true;

            //存入DB >> 呼叫 Factory 進行 CRUD
            CSportTypeFactory SportTypeFactory = new CSportTypeFactory();
            SportTypeFactory.Create(Wrap);

            return RedirectToAction("SportTypeIndex");
        }


        //運動類型編輯 >> 頁面產生
        public IActionResult SportTypeEdit(int? id)
        {
            //驗證id非null
            if (id == null)
                return RedirectToAction("SportTypeIndex");

            //用id取出對應資料送到前端 >> 傳入 Factory 操作 Query
            CSportTypeFactory SportTypeFactory = new CSportTypeFactory();
            CSportTypeWrap data = SportTypeFactory.QueryById(id);

            return View(data);
        }



        //運動類型編輯 >> 參數送回
        [HttpPost]
        public IActionResult SportTypeEdit(CSportTypeWrap Wrap)
        {
            //驗證送回的資料非null
            if (!ModelState.IsValid)
                return View(Wrap);

            //將前端填寫資料送入 Factory 進行 Edit CRUD
            CSportTypeFactory SportTypeFactory = new CSportTypeFactory();
            SportTypeFactory.Edit(Wrap);

            return RedirectToAction("SportTypeIndex");
        }

        //運動類型刪除
        public IActionResult SportTypeDelete(int? id)
        {
            //驗證變數是否為null
            if (id == null)
                return RedirectToAction("SportTypeIndex");


            //把回傳變數送入 factory 執行軟刪除
            CSportTypeFactory SportTypeFactory = new CSportTypeFactory();
            SportTypeFactory.Delete((int)id);

            return RedirectToAction("SportTypeIndex");
        }


        /*Venue*/
        //列出所有場地
        public IActionResult VenueIndex()
        {
            List<CVenueWrap> datas = new CVenueFactory().QueryAll();
            return View(datas);
        }

        //場地新增>> 頁面產生 
        public IActionResult VenueCreate()
        {
            //把運動類型送到前端 >> 做成下拉選單
            //用viewModel存
            var vm = new VenueCreateViewModel();
            vm.SportTypes = new CVenueFactory().GetSportTypes();
            return View(vm);
        }


        //場地新增 >> 資料回傳存入DB
        [HttpPost]
        public async Task<IActionResult> VenueCreate(VenueCreateViewModel vm)
        {
            //判斷填寫欄位是否合規 >> 不合規就重新填寫
            if (!ModelState.IsValid)
            {
                //把下拉選單資料塞回vm回傳前端再填寫一次
                vm.SportTypes = new CVenueFactory().GetSportTypes();
                return View(vm);
            }

            //照片上傳
            // null >> 表單有夾帶檔案欄位
            // PhotoFile.Length > 0 >> 確認檔案內容不是空檔案（避免選到大小為 0 byte 的檔案）
            if (vm.PhotoFile != null && vm.PhotoFile.Length > 0)
            {
                // 產生不重複檔名 >> 唯一值 + 原始檔案副檔名
                string fileName = Guid.NewGuid() + Path.GetExtension(vm.PhotoFile.FileName);

                // 實際存放的資料夾路徑 >> 在 wwwroot 路徑下
                string folderPath = Path.Combine(_env.WebRootPath, "images", "venues");

                // 資料夾不存在就先建立，避免 CopyToAsync 找不到路徑報錯
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                //組合路徑 & 檔名
                string filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await vm.PhotoFile.CopyToAsync(stream);
                }

                // 存進資料庫的是網頁可存取的相對路徑,不是實體硬碟路徑
                vm.PhotoPath = "/images/venues/" + fileName;
            }

            CVenueWrap VenueWrap = new CVenueWrap();
            VenueWrap.VenueName = vm.VenueName;
            VenueWrap.SportTypeId = vm.SportTypeId;
            VenueWrap.Location = vm.Location;
            VenueWrap.IsActive = true;
            VenueWrap.Capacity = vm.Capacity;
            VenueWrap.PhotoPath = vm.PhotoPath;
            VenueWrap.CreatedBy = 1;//尚未整合會員權限,先設為1

            //送進Factory執行新增
            CVenueFactory VenueFactory = new CVenueFactory();
            VenueFactory.Create(VenueWrap);

            return RedirectToAction("VenueIndex");
        }



        //場地編輯 >> 畫面產生
        public IActionResult VenueEdit(int? id)
        {
            //驗證id是否null
            if (id == null)
                return RedirectToAction("VenueIndex");

            //依傳回id執行進查找對應場地資料
            CVenueFactory VenueFactory = new CVenueFactory();
            var data = VenueFactory.QueryById((int)id);
            if (data == null)
                return RedirectToAction("VenueIndex");

            //把data內資料塞進 ViewModel
            VenueEditViewModel vm = new VenueEditViewModel();
            vm.VenueId = (int)id;
            vm.VenueName = data.VenueName;
            vm.SportTypeId = data.SportTypeId;
            vm.Capacity = data.Capacity;
            vm.Location = data.Location;
            vm.PhotoPath = data.PhotoPath;

            //要把運動類型清單一起送到前端
            vm.SportTypes = new CVenueFactory().GetSportTypes();


            return View(vm);
        }


        //場地編輯 >> 資料傳回
        [HttpPost]
        public async Task<IActionResult> VenueEdit(VenueEditViewModel vm)
        {
            //驗證欄位填寫是否合規
            if (!ModelState.IsValid)
            {
                //回傳下拉清單選項回去
                vm.SportTypes = new CVenueFactory().GetSportTypes();
                return View(vm);
            }

            //先處理照片路徑改變 >> 若有上傳照片,就要改變vm的PhotoPath
            if (vm.PhotoFile != null && vm.PhotoFile.Length > 0)
            {
                // 執行照片存邏輯
                string fileName = Guid.NewGuid() + Path.GetExtension(vm.PhotoFile.FileName);


                string folderPath = Path.Combine(_env.WebRootPath, "images", "venues");


                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await vm.PhotoFile.CopyToAsync(stream);
                }

                // 相對路徑存進資料庫
                vm.PhotoPath = "/images/venues/" + fileName;
            }


            //執行場地資料編輯 >> 送進Factory處理
            CVenueFactory VenueFactory = new CVenueFactory();
            VenueFactory.Edit(vm);



            //編輯完成,回到場地清單
            return RedirectToAction("VenueIndex");
        }


        //場地刪除
        public IActionResult VenueDelete(int? id)
        {
            //驗證變數是否為null
            if (id == null)
                return RedirectToAction("VenueIndex");


            //把回傳變數送入 factory 執行軟刪除
            CVenueFactory VenueFactory = new CVenueFactory();
            VenueFactory.Delete((int)id);

            return RedirectToAction("VenueIndex");
        }
    }
}
