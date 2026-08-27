namespace KyrolusSous.Auth.Abstractions;

/// <summary>
/// Well-known constants for the Kyrolus Auth ecosystem.
/// </summary>
public static class KyrolusAuthConstants
{
    /// <summary>
    /// External authentication provider names. These double as the ASP.NET Core
    /// authentication scheme names registered by the Kyrolus provider packages.
    /// </summary>
    public static class Providers
    {
        public const string Google = "Google";
        public const string Apple = "Apple";
        public const string Facebook = "Facebook";
        public const string GitHub = "GitHub";
        public const string X = "X";
        public const string Microsoft = "Microsoft";
        public const string LinkedIn = "LinkedIn";
        public const string Discord = "Discord";
    }

    /// <summary>
    /// Standard OpenID Connect / OAuth 2.0 claim types, as they appear in provider JSON payloads.
    /// </summary>
    public static class Claims
    {
        public const string Sub = "sub";
        public const string Name = "name";
        public const string GivenName = "given_name";
        public const string FamilyName = "family_name";
        public const string MiddleName = "middle_name";
        public const string Nickname = "nickname";
        public const string PreferredUsername = "preferred_username";
        public const string Email = "email";
        public const string EmailVerified = "email_verified";
        public const string PhoneNumber = "phone_number";
        public const string PhoneNumberVerified = "phone_number_verified";
        public const string Picture = "picture";
        public const string Profile = "profile";
        public const string Website = "website";
        public const string Locale = "locale";
        public const string ZoneInfo = "zoneinfo";
        public const string UpdatedAt = "updated_at";
        public const string Role = "role";
        public const string Provider = "kyrolus:provider";
        public const string ProviderKey = "kyrolus:provider_key";
        public const string TenantId = "tenant_id";
    }

    /// <summary>
    /// Names used when persisting external provider tokens in the authentication properties.
    /// </summary>
    public static class Tokens
    {
        public const string AccessToken = "access_token";
        public const string RefreshToken = "refresh_token";
        public const string IdToken = "id_token";
        public const string TokenType = "token_type";
        public const string ExpiresAt = "expires_at";
    }

    /// <summary>
    /// Error codes surfaced by the Kyrolus auth endpoints and external login pipeline.
    /// </summary>
    public static class Errors
    {
        public const string InvalidCredentials = "auth.invalid_credentials";
        public const string UserNotFound = "auth.user_not_found";
        public const string UserInactive = "auth.user_inactive";
        public const string UserLockedOut = "auth.user_locked_out";
        public const string EmailNotConfirmed = "auth.email_not_confirmed";
        public const string ExternalLoginFailed = "auth.external_login_failed";
        public const string ExternalLoginDenied = "auth.external_login_denied";
        public const string ProviderNotConfigured = "auth.provider_not_configured";
        public const string UserCreationFailed = "auth.user_creation_failed";
    }
}
