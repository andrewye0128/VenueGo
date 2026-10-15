using VenueGo.Data;
using VenueGo.Dtos.ContentDtos;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.ContentViewModels;
using VenueGo.Dtos;


namespace VenueGo.Models.ContentModels
{
    public class CNewsFactory
    {
        //前台列表每頁筆數上限 >> 網址上的 pageSize 可以被亂改,超過就以這個值為準
        private const int MaxPublicPageSize = 20;

        //列表摘要的字數
        private const int SummaryLength = 100;

        //後台列表 >> 供 NewsIndex 頁面使用,草稿、排程中的消息也會列出
        //category:分類代碼,null 或不在清單內 = 全部
        public NewsIndexViewModel QueryAdminPage(string? category, int page, int pageSize, DateTime now)
        {
            //分類防呆:網址上的分類可能被亂改,不在清單內就當作「全部」
            if (!NewsCategoryOptions.IsValid(category))
            {
                category = null;
            }

            //頁碼防呆:小於1就修正回第1頁
            if (page < 1)
            {
                page = 1;
            }

            using (dbVenueContext db = new dbVenueContext())
            {
                //1. 篩選:只列出未刪除的;有選分類時再加上分類條件
                var query = db.News.Where(n => n.IsActive == true);
                if (category != null)
                {
                    query = query.Where(n => n.Category == category);
                }

                //2. 總數:分頁之前先算,換頁按鈕要用
                NewsIndexViewModel vm = new NewsIndexViewModel();
                vm.CurrentCategory = category;
                vm.PageSize = pageSize;
                vm.TotalCount = query.Count();

                //頁碼超過總頁數(例如最後一頁的消息被刪光),修正回最後一頁,避免畫面空白
                if (page > vm.TotalPages)
                {
                    page = vm.TotalPages;
                }
                vm.CurrentPage = page;

                //3. 排序:置頂優先(true 排在 false 前面),再依發布時間新到舊
                //   同一時間發布的再依 NewsId 新到舊,確保每次換頁的順序都一樣
                //4. 分頁:跳過前面幾頁的筆數,只取這一頁
                //   例如 page=2、pageSize=10 >> 跳過 10 筆,取第 11~20 筆
                List<News> newsForThisPage = query
                    .OrderByDescending(n => n.IsPinned)
                    .ThenByDescending(n => n.PublishedAt)
                    .ThenByDescending(n => n.NewsId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                //5. 轉成圖卡:中文分類名稱、狀態文字在這裡算好,View 只負責顯示
                foreach (News news in newsForThisPage)
                {
                    NewsCardViewModel card = new NewsCardViewModel();
                    card.NewsId = news.NewsId;
                    card.Title = news.Title;
                    card.CategoryName = NewsCategoryOptions.GetName(news.Category);
                    card.ImagePath = news.ImagePath;
                    card.PublishedAt = news.PublishedAt;
                    card.CreatedAt = news.CreatedAt;
                    card.IsPinned = news.IsPinned;
                    card.StatusText = GetStatusText(news, now);

                    vm.Cards.Add(card);
                }
                //回傳組好的ViewModel 給 Controller 
                return vm;
            }
        }


        //Create
        public void Create(CNewsWrap Wrap)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                //EF 只認得實體,所以加入的是 Wrap 包住的 News
                db.News.Add(Wrap.news);
                db.SaveChanges();
            }
        }

        //QueryById >> 依 Id 查詢單筆 >> 編輯頁帶出原本的資料
        //找不到或已刪除時回傳 null
        public CNewsWrap? QueryById(int id)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                //已刪除(IsActive = false)的消息當作不存在,不能再被編輯
                News? news = db.News.FirstOrDefault(n => n.NewsId == id && n.IsActive);
                if (news == null)
                {
                    return null;
                }

                //把查到的實體放進 Wrap,編輯頁的表單綁定的是 Wrap
                CNewsWrap wrap = new CNewsWrap();
                wrap.news = news;
                return wrap;
            }
        }

        //Edit
        //replaceCover = true >> 這次換了封面(上傳圖或公版底稿),4 個 Cover 欄位改成 Wrap 的值
        //                      (呼叫前 Controller 已經驗證並設定好:公版底稿有值、上傳圖清成 null)
        public bool Edit(CNewsWrap Wrap, string? newImagePath, bool replaceCover, int userId, DateTime now)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                //1. 用 Wrap 的 Id 查出資料庫原本那一筆;已刪除的不能再被修改
                News? newsDb = db.News.FirstOrDefault(n => n.NewsId == Wrap.NewsId && n.IsActive);
                if (newsDb == null)
                {
                    return false;
                }

                //2. 只更新表單上可以修改的欄位
                //   CreatedAt / CreatedBy / IsActive 不從 Wrap 取值,避免被表單夾帶的值覆蓋
                newsDb.Category = Wrap.Category;
                newsDb.Title = Wrap.Title;
                newsDb.Content = Wrap.Content;
                newsDb.PublishedAt = Wrap.PublishedAt;
                newsDb.IsPublished = Wrap.IsPublished;
                newsDb.IsPinned = Wrap.IsPinned;

                //3. 封面:有新路徑才換,沒有就保留原本的
                //   不用 Wrap.ImagePath >> Wrap 直接接表單,這個值可能被竄改
                if (newImagePath != null)
                {
                    newsDb.ImagePath = newImagePath;
                }

                //   Cover 欄位只有換封面時才更新;沒換封面時保留原本的(表單上的 Cover 欄位可能是空的或被竄改)
                if (replaceCover)
                {
                    newsDb.CoverTitle = Wrap.CoverTitle;
                    newsDb.CoverTitleStyles = Wrap.CoverTitleStyles;
                    newsDb.CoverText = Wrap.CoverText;
                    newsDb.CoverTextStyles = Wrap.CoverTextStyles;
                }

                //4. 稽核欄位:記錄最後修改時間與修改者
                newsDb.UpdatedAt = now;
                newsDb.UpdatedBy = userId;

                db.SaveChanges();
                return true;
            }
        }

        //Delete(軟刪除)
        public bool Delete(int id, int userId, DateTime now)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                //1. 查出要刪除的那一筆;已刪除的不用再刪一次
                News? newsDb = db.News.FirstOrDefault(n => n.NewsId == id && n.IsActive);
                if (newsDb == null)
                {
                    return false;
                }

                //2. 軟刪除:資料留在資料表,只是不再顯示
                //   封面圖檔也保留,資料還在,圖片路徑就要能用
                newsDb.IsActive = false;

                //3. 軟刪除也是一次更新 >> 記錄是誰、何時刪除
                newsDb.UpdatedAt = now;
                newsDb.UpdatedBy = userId;

                db.SaveChanges();
                return true;
            }
        }



        /*前台API所需資料 CRUD */
        //前台列表 >> 供 GET /api/news 使用,只列出已發布的消息
        public PagedResult<NewsListItemDto> QueryPublicList(string? category, int page, int pageSize, DateTime now)
        {
            //分類防呆:網址上的分類可能被亂改,不在清單內就當作「全部」
            if (!NewsCategoryOptions.IsValid(category))
            {
                category = null;
            }

            //頁碼防呆:小於1就修正回第1頁
            if (page < 1)
            {
                page = 1;
            }

            //每頁筆數防呆:網址上的 pageSize 可能被亂改,限制在 1~20 筆,避免一次查出全部資料
            if (pageSize < 1)
            {
                pageSize = 1;
            }
            if (pageSize > MaxPublicPageSize)
            {
                pageSize = MaxPublicPageSize;
            }

            using (dbVenueContext db = new dbVenueContext())
            {
                //1. 篩選:前台只看得到「已發布」的消息,三個條件缺一不可
                //   IsActive = 沒被刪除;IsPublished = 上架開關打開;PublishedAt <= now = 發布時間已經到了
                IQueryable<News> query = db.News.Where(n => n.IsActive && n.IsPublished && n.PublishedAt <= now);
                if (category != null)
                {
                    query = query.Where(n => n.Category == category);
                }

                //2. 總數與總頁數:PageResult 的屬性只能在建立時給值,要先自己算出總頁數才能修正頁碼
                //   沒有資料時總頁數也要是 1,避免頁碼被修正成 0
                int totalCount = query.Count();
                int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
                if (page > totalPages)
                {
                    page = totalPages;
                }

                //3. 排序與分頁:跟後台列表相同,置頂優先,再依發布時間、NewsId 新到舊
                List<News> newsForThisPage = query
                    .OrderByDescending(n => n.IsPinned)
                    .ThenByDescending(n => n.PublishedAt)
                    .ThenByDescending(n => n.NewsId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                //4. 轉成 DTO:中文分類名稱、摘要在這裡算好,前台只負責顯示
                List<NewsListItemDto> items = new List<NewsListItemDto>();
                foreach (News news in newsForThisPage)
                {
                    NewsListItemDto dto = new NewsListItemDto();
                    dto.Id = news.NewsId;
                    dto.Title = news.Title;
                    dto.IsPinned = news.IsPinned;
                    dto.Category = news.Category;
                    dto.CategoryName = NewsCategoryOptions.GetName(news.Category);
                    dto.PublishedAt = news.PublishedAt;
                    dto.ImagePath = news.ImagePath;
                    dto.Summary = BuildSummary(news.Content);

                    items.Add(dto);
                }

                //5. 包成 PagedResult 回傳(Dtos.PagedResult,全組前台 API 共用的分頁格式):屬性是 init,只能在建立物件的大括號裡一次給完
                //   只回傳總筆數,總頁數由前台的 UPagination 自己算
                return new PagedResult<NewsListItemDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
        }


        //依傳入的id撈出已經刊登的最新消息組成 DTO 回傳
        public NewsDetailDto? QueryPublicDetail(int id, DateTime now)
        {  
            using (dbVenueContext db = new dbVenueContext())
            {
                //準備回傳物件
                NewsDetailDto newsDetailDto = new NewsDetailDto();
                //先撈出已經刊登的消息
                var tempQuery = from n in db.News
                                where n.PublishedAt <= now && n.IsActive == true && n.IsPublished == true
                                select n;
                //再從已刊登消息中查出 id 對應之消息
                News? thisNews = (from n in tempQuery
                                 where n.NewsId == id
                                 select n).FirstOrDefault();

                //thisNew做null檢查
                if (thisNews == null)
                    return null;

                //前一則消息
                News? previousNews = (from n in tempQuery  
                                     where n.PublishedAt < thisNews.PublishedAt
                                     orderby n.PublishedAt descending
                                     select n
                                     
                                     ).FirstOrDefault();

                //後一則消息
                News? nextNews = (from n in tempQuery
                                 where n.PublishedAt > thisNews.PublishedAt
                                 orderby n.PublishedAt ascending
                                 select n).FirstOrDefault();

                //組合為 DTO
                if(previousNews != null)
                {
                    newsDetailDto.Previous = new NewsNavItemDto();//上一則
                    newsDetailDto.Previous.Title = previousNews.Title;
                    newsDetailDto.Previous.Id = previousNews.NewsId;
                }

                if(nextNews != null)
                {
                    newsDetailDto.Next = new NewsNavItemDto();//下一則
                    newsDetailDto.Next.Title = nextNews.Title;
                    newsDetailDto.Next.Id = nextNews.NewsId;
                }

                newsDetailDto.Id = thisNews.NewsId;
                newsDetailDto.Title = thisNews.Title;
                newsDetailDto.Content = thisNews.Content;
                newsDetailDto.IsPinned = thisNews.IsPinned;
                newsDetailDto.Category = thisNews.Category;
                newsDetailDto.CategoryName = NewsCategoryOptions.GetName(thisNews.Category);
                newsDetailDto.PublishedAt = thisNews.PublishedAt;
                newsDetailDto.ImagePath = thisNews.ImagePath;
                    
                //回傳 Dto 到 Controller
                return newsDetailDto;
            }
        }

        /*類別私有方法*/
        //取得刊登狀態文字 >> 依上架開關與發布時間判斷
        private static string GetStatusText(News news, DateTime now)
        {
            //沒有上架 >> 不管發布時間是什麼,都是草稿
            if (!news.IsPublished)
            {
                return "草稿";
            }

            //已上架,但發布時間還沒到 >> 時間到了前台才會顯示
            if (news.PublishedAt > now)
            {
                return "排程中";
            }

            return "已發布";
        }


        //列表摘要 >> 取內文前 100 字,超過就加上「…」
        private static string BuildSummary(string content)
        {
            //換行改成空格:摘要只顯示一兩行,留著換行會讓版面亂掉
            //先換 \r\n 再換 \n,Windows 的換行才不會變成兩個空格
            string singleLine = content.Replace("\r\n", " ").Replace("\n", " ").Trim();

            if (singleLine.Length <= SummaryLength)
            {
                return singleLine;
            }

            return singleLine.Substring(0, SummaryLength) + "…";
        }
    }
}

