namespace VenueGo.Services
{
    /// <summary>
    /// 開發用時光機：讓「網站以為現在是幾點」和真實時間不同，不必再改電腦的時鐘。
    /// <para>
    /// 【影響範圍】只影響透過 <see cref="ITimeService"/>.Now 取得時間的程式。
    /// 直接寫 DateTime.Now 的地方、資料庫的 sysdatetime() 預設值、前端瀏覽器的時鐘都不受影響。
    /// </para>
    /// <para>
    /// 【只在開發環境有效】其他環境 <see cref="IsAvailable"/> 是 false，
    /// 偏移量永遠是 0，呼叫 TravelTo／TravelBy 會拋出 InvalidOperationException。
    /// </para>
    /// <para>
    /// 【重新啟動也會記得】每次調整都會存到本機的一個小檔案（見 TimeMachine 的說明），
    /// 網站重新啟動（包含 Hot Reload 自動重啟）後會回到上次的狀態。
    /// </para>
    /// </summary>
    public interface ITimeMachine
    {
        /// <summary>這個環境能不能用時光機（只有 Development 可以）。</summary>
        bool IsAvailable { get; }

        /// <summary>是否正在時光旅行中（偏移量不是 0）。</summary>
        bool IsTraveling { get; }

        /// <summary>網站時間比真實時間快多少；負數代表回到過去。</summary>
        TimeSpan TravelOffset { get; }

        /// <summary>
        /// 最後一次調整的記號。每調整一次就會變，沒調整過是 0。
        /// <para>給前端判斷「頁面產生之後，時光機有沒有被動過」，數字本身沒有意義。</para>
        /// </summary>
        long Stamp { get; }

        /// <summary>
        /// 跳到指定的台北時間，之後時間照常往前走。
        /// <para>
        /// ⚠️ 這裡只看系統時鐘，不知道 TimeService 的校時偏移量（通常不到幾秒）。
        /// 要精準到秒的話，改用 TravelBy(目標 − 目前的網站時間)，/api/dev/time 就是這樣做的。
        /// </para>
        /// </summary>
        void TravelTo(DateTime taipeiTime);

        /// <summary>在目前的基礎上再往前（正數）或往後（負數）撥。</summary>
        void TravelBy(TimeSpan delta);

        /// <summary>回到真實時間。</summary>
        void Reset();
    }
}
