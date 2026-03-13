using FreeSpeakWeb.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace FreeSpeakWeb.Components.Account
{
    /// <summary>
    /// A no-operation email sender for Identity that logs emails instead of sending them.
    /// This is a placeholder implementation that should be replaced with a real email service
    /// in production. Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block 
    /// from RegisterConfirmation.razor after updating with a real implementation.
    /// </summary>
    internal sealed class IdentityNoOpEmailSender : IEmailSender<ApplicationUser>
    {
        private readonly IEmailSender emailSender = new NoOpEmailSender();

        /// <summary>
        /// Sends a confirmation link email to verify the user's email address.
        /// </summary>
        /// <param name="user">The user to send the email to.</param>
        /// <param name="email">The email address to send to.</param>
        /// <param name="confirmationLink">The URL link for email confirmation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
            emailSender.SendEmailAsync(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

        /// <summary>
        /// Sends a password reset link email to the user.
        /// </summary>
        /// <param name="user">The user to send the email to.</param>
        /// <param name="email">The email address to send to.</param>
        /// <param name="resetLink">The URL link for password reset.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
            emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

        /// <summary>
        /// Sends a password reset code email to the user.
        /// </summary>
        /// <param name="user">The user to send the email to.</param>
        /// <param name="email">The email address to send to.</param>
        /// <param name="resetCode">The code for password reset.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
            emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password using the following code: {resetCode}");
    }
}
