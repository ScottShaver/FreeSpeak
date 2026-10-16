using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace FreeSpeakWeb.Components.Account;

/// <summary>
/// Provides localized descriptions for ASP.NET Core Identity errors so that messages surfaced
/// on the account pages (for example password policy failures) follow the user's UI culture.
/// </summary>
/// <param name="localizer">The localizer used to look up the error descriptions.</param>
public class LocalizedIdentityErrorDescriber(IStringLocalizer<FreeSpeakWeb.Resources.Account.LocalizedIdentityErrorDescriber> localizer) : IdentityErrorDescriber
{
    /// <summary>
    /// Builds an <see cref="IdentityError"/> whose description comes from the resource bundle.
    /// </summary>
    /// <param name="code">The Identity error code (the default English method name).</param>
    /// <param name="args">Optional format arguments for the localized description.</param>
    /// <returns>The localized identity error.</returns>
    private IdentityError Create(string code, params object[] args) => new()
    {
        Code = code,
        Description = localizer[code, args].Value
    };

    /// <inheritdoc />
    public override IdentityError DefaultError() => Create(nameof(DefaultError));

    /// <inheritdoc />
    public override IdentityError ConcurrencyFailure() => Create(nameof(ConcurrencyFailure));

    /// <inheritdoc />
    public override IdentityError PasswordMismatch() => Create(nameof(PasswordMismatch));

    /// <inheritdoc />
    public override IdentityError InvalidToken() => Create(nameof(InvalidToken));

    /// <inheritdoc />
    public override IdentityError RecoveryCodeRedemptionFailed() => Create(nameof(RecoveryCodeRedemptionFailed));

    /// <inheritdoc />
    public override IdentityError LoginAlreadyAssociated() => Create(nameof(LoginAlreadyAssociated));

    /// <inheritdoc />
    public override IdentityError InvalidUserName(string? userName) => Create(nameof(InvalidUserName), userName ?? string.Empty);

    /// <inheritdoc />
    public override IdentityError InvalidEmail(string? email) => Create(nameof(InvalidEmail), email ?? string.Empty);

    /// <inheritdoc />
    public override IdentityError DuplicateUserName(string userName) => Create(nameof(DuplicateUserName), userName);

    /// <inheritdoc />
    public override IdentityError DuplicateEmail(string email) => Create(nameof(DuplicateEmail), email);

    /// <inheritdoc />
    public override IdentityError InvalidRoleName(string? role) => Create(nameof(InvalidRoleName), role ?? string.Empty);

    /// <inheritdoc />
    public override IdentityError DuplicateRoleName(string role) => Create(nameof(DuplicateRoleName), role);

    /// <inheritdoc />
    public override IdentityError UserAlreadyHasPassword() => Create(nameof(UserAlreadyHasPassword));

    /// <inheritdoc />
    public override IdentityError UserLockoutNotEnabled() => Create(nameof(UserLockoutNotEnabled));

    /// <inheritdoc />
    public override IdentityError UserAlreadyInRole(string role) => Create(nameof(UserAlreadyInRole), role);

    /// <inheritdoc />
    public override IdentityError UserNotInRole(string role) => Create(nameof(UserNotInRole), role);

    /// <inheritdoc />
    public override IdentityError PasswordTooShort(int length) => Create(nameof(PasswordTooShort), length);

    /// <inheritdoc />
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Create(nameof(PasswordRequiresUniqueChars), uniqueChars);

    /// <inheritdoc />
    public override IdentityError PasswordRequiresNonAlphanumeric() => Create(nameof(PasswordRequiresNonAlphanumeric));

    /// <inheritdoc />
    public override IdentityError PasswordRequiresDigit() => Create(nameof(PasswordRequiresDigit));

    /// <inheritdoc />
    public override IdentityError PasswordRequiresLower() => Create(nameof(PasswordRequiresLower));

    /// <inheritdoc />
    public override IdentityError PasswordRequiresUpper() => Create(nameof(PasswordRequiresUpper));
}
