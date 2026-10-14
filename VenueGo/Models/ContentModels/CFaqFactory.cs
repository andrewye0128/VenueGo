using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.ContentViewModels;

namespace VenueGo.Models.ContentModels
{
    public class CFaqFactory
    {
        //後台列表 >> 供 FaqIndex 頁面使用,隱藏的問題也會列出,不分頁
        //category:分類代碼,null 或不在清單內 = 全部
        public FaqIndexViewModel QueryAdminList(string? category)
        {
            //分類防呆:網址上的分類可能被亂改,不在清單內就當作「全部」
            if (!FaqCategoryOptions.IsValid(category))
            {
                category = null;
            }

            using (dbVenueContext db = new dbVenueContext())
            {
                //1. 一次查出要顯示的問題(未刪除),依分類內順序排好
                IQueryable<Faq> query = db.Faqs.Where(f => f.IsActive);
                if (category != null)
                {
                    query = query.Where(f => f.Category == category);
                }

                List<Faq> faqs = query
                    .OrderBy(f => f.SortOrder)
                    .ThenBy(f => f.FaqId)
                    .ToList();

                //2. 依分類清單的順序分組;沒有問題的分類也保留一組(畫面顯示「尚無問題」)
                FaqIndexViewModel vm = new FaqIndexViewModel();
                vm.CurrentCategory = category;

                foreach (KeyValuePair<string, string> option in FaqCategoryOptions.All)
                {
                    //單一分類時只要那一組
                    if (category != null && option.Key != category)
                    {
                        continue;
                    }

                    FaqGroupViewModel group = new FaqGroupViewModel();
                    group.Category = option.Key;
                    group.CategoryName = option.Value;

                    foreach (Faq faq in faqs.Where(f => f.Category == option.Key))
                    {
                        CFaqWrap wrap = new CFaqWrap();
                        wrap.faq = faq;
                        group.Faqs.Add(wrap);
                    }

                    vm.Groups.Add(group);
                }

                return vm;
            }
        }

        //依 Id 查詢單筆 >> 編輯頁帶出原本的資料
        //找不到或已刪除時回傳 null
        public CFaqWrap? QueryById(int id)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                Faq? faq = db.Faqs.FirstOrDefault(f => f.FaqId == id && f.IsActive);
                if (faq == null)
                {
                    return null;
                }

                CFaqWrap wrap = new CFaqWrap();
                wrap.faq = faq;
                return wrap;
            }
        }

        //Create
        //SortOrder 由這裡決定(排在該分類最後),其他存入的值由 Controller 決定
        public void Create(CFaqWrap Wrap)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                Wrap.SortOrder = GetNextSortOrder(db, Wrap.Category);

                db.Faqs.Add(Wrap.faq);
                db.SaveChanges();
            }
        }

        //Edit
        //userId / now 由 Controller 傳入(登入者 UserId、ITimeService 校時後的時間)
        //回傳 false = 找不到這則問題(不存在或已刪除),沒有存檔
        public bool Edit(CFaqWrap Wrap, int userId, DateTime now)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                //1. 查出資料庫原本那一筆;已刪除的不能再被修改
                Faq? faqDb = db.Faqs.FirstOrDefault(f => f.FaqId == Wrap.FaqId && f.IsActive);
                if (faqDb == null)
                {
                    return false;
                }

                //2. 換了分類 >> 排到新分類的最後(先算再改分類,才不會把自己算進去)
                if (faqDb.Category != Wrap.Category)
                {
                    faqDb.SortOrder = GetNextSortOrder(db, Wrap.Category);
                }

                //3. 只更新表單上可以修改的欄位
                //   CreatedAt / CreatedBy / IsActive 不從 Wrap 取值,避免被表單夾帶的值覆蓋
                faqDb.Category = Wrap.Category;
                faqDb.Question = Wrap.Question;
                faqDb.Answer = Wrap.Answer;
                faqDb.IsPublished = Wrap.IsPublished;

                //4. 稽核欄位
                faqDb.UpdatedAt = now;
                faqDb.UpdatedBy = userId;

                db.SaveChanges();
                return true;
            }
        }

        //Delete(軟刪除) >> 刪除不重新編號,其他問題的順序不受影響
        //回傳 false = 找不到這則問題(不存在或已經刪除過)
        public bool Delete(int id, int userId, DateTime now)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                Faq? faqDb = db.Faqs.FirstOrDefault(f => f.FaqId == id && f.IsActive);
                if (faqDb == null)
                {
                    return false;
                }

                faqDb.IsActive = false;
                faqDb.UpdatedAt = now;
                faqDb.UpdatedBy = userId;

                db.SaveChanges();
                return true;
            }
        }

        //顯示/隱藏切換 >> 隱藏的問題保留原本的順序,恢復顯示時回到原位
        //回傳 false = 找不到這則問題
        public bool TogglePublished(int id, int userId, DateTime now)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                Faq? faqDb = db.Faqs.FirstOrDefault(f => f.FaqId == id && f.IsActive);
                if (faqDb == null)
                {
                    return false;
                }

                faqDb.IsPublished = !faqDb.IsPublished;
                faqDb.UpdatedAt = now;
                faqDb.UpdatedBy = userId;

                db.SaveChanges();
                return true;
            }
        }

        //上移/下移 >> 跟同分類的相鄰問題交換位置(不跨分類;隱藏的問題也算在內)
        //moveUp:true = 上移,false = 下移
        //回傳 false = 找不到這則問題,或已經在最上面/最下面,沒有存檔
        public bool Move(int id, bool moveUp, int userId, DateTime now)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                Faq? target = db.Faqs.FirstOrDefault(f => f.FaqId == id && f.IsActive);
                if (target == null)
                {
                    return false;
                }

                //1. 查出同分類的全部問題,依目前畫面上的順序排好
                List<Faq> siblings = db.Faqs
                    .Where(f => f.IsActive && f.Category == target.Category)
                    .OrderBy(f => f.SortOrder)
                    .ThenBy(f => f.FaqId)
                    .ToList();

                //2. 找出自己和要交換的相鄰問題的位置
                int index = siblings.FindIndex(f => f.FaqId == id);
                int neighborIndex = moveUp ? index - 1 : index + 1;
                if (neighborIndex < 0 || neighborIndex >= siblings.Count)
                {
                    return false;
                }

                //3. 在清單裡交換兩筆的位置
                Faq neighbor = siblings[neighborIndex];
                siblings[neighborIndex] = target;
                siblings[index] = neighbor;

                //4. 依新的位置重新給 SortOrder(1、2、3…)
                //   不直接交換兩筆的 SortOrder:萬一有兩筆數字相同(例如同時新增),交換後順序不會變
                //   重新編號只會讓數字變整齊,畫面上的順序除了交換的兩筆以外都不變
                for (int i = 0; i < siblings.Count; i++)
                {
                    siblings[i].SortOrder = i + 1;
                }

                //5. 稽核欄位:只記錄交換的兩筆(其他筆只是數字重新整理,順序沒變)
                target.UpdatedAt = now;
                target.UpdatedBy = userId;
                neighbor.UpdatedAt = now;
                neighbor.UpdatedBy = userId;

                //一次 SaveChanges = 同一個交易,全部成功或全部不存
                db.SaveChanges();
                return true;
            }
        }


        /*類別私有方法*/
        //該分類下一個 SortOrder = 目前最大值 + 1;沒有問題時從 1 開始
        //已刪除的不算(刪除不重新編號,但已刪除的不會顯示,不影響排序)
        private static int GetNextSortOrder(dbVenueContext db, string category)
        {
            int? max = db.Faqs
                .Where(f => f.IsActive && f.Category == category)
                .Max(f => (int?)f.SortOrder);

            return (max ?? 0) + 1;
        }
    }
}
