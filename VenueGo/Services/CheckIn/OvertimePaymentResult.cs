using VenueGo.Models.Enums;

namespace VenueGo.Services.CheckIn
{
    public record OvertimePaymentResult(bool Success, string? Reason, int? ChargeAmount)
    {
        public static OvertimePaymentResult OK(int chargeAmount) => new(true, null, chargeAmount);
        public static OvertimePaymentResult Fail(string reason) => new(false, reason, null);
    }
}
