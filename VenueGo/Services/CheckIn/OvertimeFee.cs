using VenueGo.Models.Entities;

namespace VenueGo.Services.CheckIn
{
    public static class OvertimeFee
    {
        private const int unitPrice = 500;

        private const int freeMinutes = 5;

        private const int halfMiuntes = 30;

        public static OvertimeFeeResult Calculate(DateTime endTime, DateTime actualOutTime)
        {
            int overdueMinutes = 0;
            TimeSpan exceededTime = actualOutTime - endTime;
            if(exceededTime.TotalMinutes > 0)
            {
                overdueMinutes = (int)exceededTime.TotalMinutes;
            }

            decimal billingHours;
            if(overdueMinutes <= freeMinutes)
            {
                billingHours = 0;
            }

            else if(overdueMinutes <= halfMiuntes)
            {
                billingHours = 0.5m;
            }

            else
            {
                billingHours = 1;
            }
            int charge = (int)(billingHours * unitPrice);

            return new OvertimeFeeResult(overdueMinutes, billingHours, unitPrice, charge);
        }
    }
}
