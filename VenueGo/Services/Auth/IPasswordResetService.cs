namespace VenueGo.Services.Auth
{
    public interface IPasswordResetService
    {
        Task<string?> CreateResetTokenAsync(
             string email,
             string ipAddress);

        Task<bool> ValidateResetTokenAsync(
            string email,
            string rawToken);

        Task<bool> ResetPasswordAsync(
            string email,
            string rawToken,
            string newPassword);
        Task<bool> ConfirmResetEmailSentAsync(
            string email,
            string rawToken);

        Task<bool> CancelResetTokenAsync(
            string email,
            string rawToken);
    }
}