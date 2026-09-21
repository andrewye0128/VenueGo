using Microsoft.AspNetCore.Http;
using System;
using VenueGo.Extensions;
using VenueGo.ViewModels.Reservations;
using VenueGo.ViewModels.ReservationViewModels;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 以 Session 保存新增預約暫存資料的實作。
    /// </summary>
    public class SessionReservationDraftStore : IReservationDraftStore
    {
        /// <summary>
        /// Session 的鍵值。加上前綴避免與其他子系統的 Session 資料撞名。
        /// </summary>
        private const string SessionKey = "VenueGo:ReservationDraft";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public SessionReservationDraftStore(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ISession Session =>
            _httpContextAccessor.HttpContext?.Session
            ?? throw new InvalidOperationException(
                "無法取得 Session。請確認 Program.cs 已呼叫 AddSession() 與 UseSession()。");

        public ReservationDraft Get()
            => Session.GetObject<ReservationDraft>(SessionKey) ?? new ReservationDraft();

        public void Save(ReservationDraft draft)
            => Session.SetObject(SessionKey, draft);

        public void Clear()
            => Session.Remove(SessionKey);
    }
}