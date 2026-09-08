using Microsoft.AspNetCore.Mvc;
using VenueGo.Models.VenueModels;

namespace VenueGo.Controllers
{
    public class VenueController : Controller
    {
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


    }
}
