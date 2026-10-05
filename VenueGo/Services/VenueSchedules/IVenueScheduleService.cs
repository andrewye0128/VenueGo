namespace VenueGo.Services.VenueSchedules
{
    //場地時段服務 >> 場地模組(HungYu)提供給其他模組(目前是預約模組)的查詢介面
    //其他模組需要「場地能不能用、哪些時段營業、哪些不開放、哪一格是尖峰、單價多少」時,
    //一律透過這個介面取得,不要直接查詢場地模組的資料表(WeekBusinessHours、VenueUnavailableSlots、
    //SportTypePriceRules、SportTypePeakHours、Venues、SportTypes),規則只由場地模組維護一份
    //
    //在 Program.cs 註冊為 Scoped,使用方透過建構子注入 IVenueScheduleService 即可
    //所有方法都是唯讀查詢,不會修改任何資料

    //「不能使用」的判斷全部在這裡處理,使用方不需要自己判斷 IsActive:
    //  Venues.IsActive = false             >> 場地已刪除(軟刪除),當作不存在
    //  SportTypes.IsActive = false         >> 運動類型已刪除,屬於它的場地也當作不能使用
    //  SportTypePriceRules.IsActive = false >> 暫停定價:場地照常可以使用,但單價、價格都是 null

    public interface IVenueScheduleService
    {
        //某場地某一天的時段表
        //null   >> 場地不能使用(查無此場地、場地已刪除、運動類型已刪除)
        //空清單 >> 場地可以使用,但這天公休
        //有資料 >> 當天每一格,依時間由早到晚排序,只包含營業時段
        Task<IReadOnlyList<VenueSlotInfo>?> GetDayScheduleAsync(
            int venueId, DateOnly date, CancellationToken cancellationToken = default);

        //某場地一段期間(含頭含尾)每一天的時段表,給日曆統計使用;內部一次查完整段期間
        //null   >> 場地不能使用
        //字典   >> 從 fromDate 到 toDate 每一天都有,值的內容與 GetDayScheduleAsync 相同(公休日是空清單)
        //空字典 >> fromDate 晚於 toDate
        Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<VenueSlotInfo>>?> GetRangeScheduleAsync(
            int venueId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);

        //單一場地的資料(含價格摘要),給選場地送出、建立預約前的檢查使用
        //null >> 場地不能使用
        Task<UsableVenueInfo?> GetUsableVenueAsync(
            int venueId, CancellationToken cancellationToken = default);

        //可以使用的場地清單,依運動類型 Id、再依場地 Id 排序;不會回傳 null,沒有符合的場地時是空清單
        //sportTypeId:null = 不限運動類型;keyword:場地名稱包含這個字串,null 或空白 = 不限
        //分頁由使用方自行處理
        Task<IReadOnlyList<UsableVenueInfo>> SearchUsableVenuesAsync(
            int? sportTypeId, string? keyword, CancellationToken cancellationToken = default);

        //可以使用的運動類型清單,依運動類型 Id 排序;不會回傳 null
        Task<IReadOnlyList<UsableSportTypeInfo>> GetUsableSportTypesAsync(
            CancellationToken cancellationToken = default);

        //某運動類型在某一天的尖峰起始時間,給預約明細標示「尖峰/離峰」使用
        //這一格的開始時間 >= 回傳值就是尖峰
        //null >> 那天不分尖峰/離峰、沒有價格規則或價格規則停用(整天都是離峰)
        //跟上面的方法不同:不判斷場地、運動類型是否已刪除,已刪除場地的歷史預約也能取得
        Task<TimeOnly?> GetPeakStartTimeAsync(
            int sportTypeId, DateOnly date, CancellationToken cancellationToken = default);
    }
}
