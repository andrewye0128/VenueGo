using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Extensions;
using VenueGo.Models.Entities;

namespace VenueGo.Models.CheckinModels
{
    public class COvertimeChargeFactory
    {
        private readonly dbVenueContext _db;

        public COvertimeChargeFactory(dbVenueContext db)
        {
            _db = db;
        }

        public COvertimeChargeWrap? GetByTicket(int ticketId)
        {
            int? orderId = _db.EntryTickets.AsNoTracking()
                .Where(t => t.TicketId == ticketId)
                .Select(t => (int?)t.OrderId)
                .FirstOrDefault();

            if (orderId == null)
            {
                return null;
            }

            OvertimeCharge? data = _db.OvertimeCharges.AsNoTracking()
                .FirstOrDefault(o => o.OrderId == orderId);

            if (data == null)
            {
                return null;
            }

            COvertimeChargeWrap wrap = new COvertimeChargeWrap();
            wrap.overtimeCharge = data;
            return wrap;
        }

        public List<SelectListItem> GetPayOption()
        {
            List<SelectListItem> option = new List<SelectListItem>();
            foreach (PaymentMethodOnSite method in GetPaymentMethodOnSite())
            {
                option.Add(new SelectListItem
                {
                    Text = method.GetDisplayName(),
                    // SelectListItem.Value不接受數字, HTML的 <option value="1"> 本來就是文字
                    Value = ((byte)method).ToString()
                });
            }
            return option;
        }

        // 防呆結帳表單檢查, 沒問題為null,有問題回傳錯誤訊息
        public string? ValidateInput(bool isWaived, PaymentMethodOnSite? method, string? remark)
        {
            if(isWaived)
            {
                if(method != null)
                {
                    return "免收無須選則支付方式";
                }
                if(remark == null)
                {
                    return "免收需備註原因";
                }
            }
            else
            {
                if(method == null)
                {
                    return "請選擇支付方式";
                }
                if(!GetPaymentMethodOnSite().Contains(method.Value))
                {
                    return "付款方式異常";
                }
            }

            if(remark != null && remark.Trim().Length > 100)
            {
                return "備註原因最多100個字";
            }

            return null;
        }

        private List<PaymentMethodOnSite> GetPaymentMethodOnSite()
        {
            List<PaymentMethodOnSite> methods = new List<PaymentMethodOnSite>();
            methods.Add(PaymentMethodOnSite.LinePay);
            methods.Add(PaymentMethodOnSite.CreditCard);
            methods.Add(PaymentMethodOnSite.Cash);

            return methods;
        }
    }
}
