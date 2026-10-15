using VenueGo.Enums;

namespace VenueGo.Services.CheckIn
{
    public record OvertimeFeeResult(int OverdueMinutes, decimal BillingHours, int UnitPrice, int ChargeAmount)
    {
        public bool IsFree => BillingHours == 0;
    }
}
