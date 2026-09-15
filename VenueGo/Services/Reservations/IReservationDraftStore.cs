using VenueGo.ViewModels.ReservationViewModels;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 新增預約流程暫存資料的存取介面。
    /// <para>
    /// 【為何要抽介面】目前是存在 Session，但期末改成 Vue 前後端分離時，
    /// 暫存可能改為前端保管、或改存 Redis。抽成介面後只需替換實作，
    /// Controller 與 Service 完全不用改。
    /// 對期中而言，另一個好處是寫單元測試時可以換成記憶體版本，
    /// 不必真的起一個 HTTP Context。
    /// </para>
    /// </summary>
    public interface IReservationDraftStore
    {
        /// <summary>
        /// 取得目前的暫存資料。若尚未建立則回傳一個全新的空白物件，
        /// 呼叫端不需要檢查 null。
        /// </summary>
        ReservationDraft Get();

        /// <summary>寫回暫存資料。每次修改後都必須呼叫，否則變更不會保留。</summary>
        void Save(ReservationDraft draft);

        /// <summary>清除暫存資料。進入新增流程的第一步與離開流程時呼叫。</summary>
        void Clear();
    }
}
