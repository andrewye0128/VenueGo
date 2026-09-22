namespace VenueGo.Services
{
    /// <summary>
    /// 全系統的時間來源。
    ///
    /// ── 為什麼不直接用 DateTime.Now ──────────────────────
    /// 伺服器的時鐘可能不準，而且多台機器之間不會自己對齊。
    /// 老師的要求是「不能只依賴伺服器時間」。
    ///
    /// ── 為什麼 Now 是同步的、不用 await ──────────────────
    /// 因為它不碰網路。時鐘誤差是「慢慢累積」的，不是隨機跳動的——
    /// 伺服器如果快了三秒，下一秒還是快三秒，不會突然變成快三十秒。
    /// 所以不需要每次都問外部 API「現在幾點」，
    /// 只要偶爾問一次「我快了幾秒」，之後自己加回去就好。
    ///
    ///     每 30 分鐘校時一次： 偏移量 = API 時間 − 伺服器時間
    ///     平常要時間：         現在   = 伺服器時間 + 偏移量
    ///
    /// 精準度跟每次都問一樣，但零延遲、也不會因為第三方服務掛掉就整站變慢。
    /// 校時由 TimeSyncHostedService 在背景執行，跟任何一個請求都無關。
    /// </summary>
    public interface ITimeService
    {
        /// <summary>
        /// 目前時間（已套用偏移量，並捨去毫秒以符合 datetime2(0)）。
        /// 純計算，不碰網路，可以放心在迴圈裡呼叫。
        /// </summary>
        DateTime Now { get; }

        /// <summary>今天 00:00。等同 Now.Date。</summary>
        DateTime Today { get; }

        /// <summary>目前套用的偏移量。校時失敗時會沿用上一次的值。</summary>
        TimeSpan Offset { get; }

        /// <summary>上次校時成功的時間。從未成功過則為 null。</summary>
        DateTime? LastSyncedAt { get; }

        /// <summary>
        /// 向外部時間 API 校時一次。
        /// 由背景服務呼叫；失敗不會拋例外，只會記 log 並沿用舊的偏移量。
        /// </summary>
        Task SyncAsync(CancellationToken cancellationToken = default);
    }
}
