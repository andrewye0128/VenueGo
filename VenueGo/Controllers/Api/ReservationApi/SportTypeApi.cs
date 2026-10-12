namespace VenueGo.Controllers.Api.ReservationApi
{

    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using VenueGo.Data;
    using VenueGo.Dtos;
    using VenueGo.Dtos.BookingDtos;

    /// <summary>
    /// 運動類型 API：前台的運動類型下拉選單、預約頁的場地分組使用。
    /// <para>
    /// 【為何直接查資料庫，不放 Service】只有一個很簡單的查詢（啟用中、依編號排序），
    /// 拆到 Service 的好處不大。之後邏輯變複雜（例如要加照片、排序規則），再搬到 Service。
    /// </para>
    /// </summary>
    [ApiController]
    [Route("api/sport-types")]
    [AllowAnonymous]   // 刻意不用登入：還沒登入的人也要能選運動類型
    public class SportTypeApiController : ControllerBase
    {
        private readonly dbVenueContext _db;

        /// <param name="db">資料庫。</param>
        public SportTypeApiController(dbVenueContext db)
        {
            _db = db;
        }

        /// <summary>開放中的運動類型</summary>
        /// <remarks>
        /// 只回傳啟用中的運動，依 sportTypeId 排序。
        /// iconName 是圖示的短名稱（例如 basketball），資料庫沒有設定時是 null。
        /// </remarks>
        /// <param name="cancellationToken">使用者離開頁面時取消查詢</param>
        /// <response code="200">成功</response>
        [HttpGet]
        [ProducesResponseType<ApiResult<IReadOnlyList<SportTypeOptionDto>>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var data = await _db.SportTypes.AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.SportTypeId)
                .Select(s => new SportTypeOptionDto
                {
                    SportTypeId = s.SportTypeId,
                    SportName = s.SportName,
                    IconName = s.IconName,
                })
                .ToListAsync(cancellationToken);

            return Ok(ApiResult<IReadOnlyList<SportTypeOptionDto>>.Ok(data));
        }
    }
}
