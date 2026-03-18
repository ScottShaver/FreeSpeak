using FreeSpeakWeb.Data;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace FreeSpeakWeb.Services;

/// <summary>
/// Service for rendering notification messages using localized templates.
/// Supports runtime template rendering based on the recipient's language preference.
/// </summary>
public class NotificationTemplateService
{
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly UserPreferenceService _userPreferenceService;
    private readonly ILogger<NotificationTemplateService> _logger;

    public NotificationTemplateService(
        IStringLocalizerFactory localizerFactory,
        UserPreferenceService userPreferenceService,
        ILogger<NotificationTemplateService> logger)
    {
        _localizerFactory = localizerFactory;
        _userPreferenceService = userPreferenceService;
        _logger = logger;
    }

    /// <summary>
    /// Renders a notification message using the recipient's language preference.
    /// Falls back to the stored Message field for backward compatibility if TemplateKey is null.
    /// </summary>
    /// <param name="notification">The notification to render.</param>
    /// <returns>The rendered message in the recipient's preferred language.</returns>
    public async Task<string> RenderNotificationAsync(UserNotification notification)
    {
        // Backward compatibility: if no template key, use the stored message
        if (string.IsNullOrEmpty(notification.TemplateKey))
        {
            return notification.Message ?? string.Empty;
        }

        try
        {
            // Get user's language preference
            var culture = await _userPreferenceService.GetPreferenceAsync(notification.UserId, PreferenceType.Culture);

            // Create localizer with user's culture
            var cultureInfo = string.IsNullOrEmpty(culture) ? CultureInfo.CurrentCulture : new CultureInfo(culture);
            var localizer = _localizerFactory.Create(typeof(Resources.Notifications.NotificationTemplates));

            // Get the localized template
            var template = localizer[notification.TemplateKey];

            // If template not found, fall back to message or template key
            if (template.ResourceNotFound)
            {
                _logger.LogWarning("Template key '{TemplateKey}' not found for notification {NotificationId}", 
                    notification.TemplateKey, notification.Id);
                return notification.Message ?? notification.TemplateKey;
            }

            // Parse template parameters from Data field (stored as JSON)
            var parameters = ParseTemplateParameters(notification);

            // Format the template with parameters
            return string.Format(cultureInfo, template.Value, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering notification template for notification {NotificationId}", notification.Id);
            return notification.Message ?? notification.TemplateKey ?? "Notification";
        }
    }

    /// <summary>
    /// Extracts template parameters from the notification's Data field.
    /// Returns an array of objects suitable for string.Format.
    /// </summary>
    /// <param name="notification">The notification containing the data.</param>
    /// <returns>Array of template parameter values.</returns>
    private object[] ParseTemplateParameters(UserNotification notification)
    {
        var templateKey = notification.TemplateKey ?? string.Empty;

        // Determine expected parameters based on template key
        return templateKey switch
        {
            "FriendRequest" => ExtractParameters(notification, "RequesterName"),
            "FriendAccepted" => ExtractParameters(notification, "RequesterName"),
            "PostComment" => ExtractParameters(notification, "CommenterName"),
            "GroupPostComment" => ExtractParameters(notification, "CommenterName"),
            "CommentReply" => ExtractParameters(notification, "CommenterName"),
            "GroupCommentReply" => ExtractParameters(notification, "CommenterName"),
            "PostReaction" => ExtractParameters(notification, "ReactorName", "ReactionType"),
            "GroupPostReaction" => ExtractParameters(notification, "ReactorName", "ReactionType"),
            "CommentReaction" => ExtractParameters(notification, "ReactorName", "ReactionType"),
            "GroupCommentReaction" => ExtractParameters(notification, "ReactorName", "ReactionType"),
            _ => Array.Empty<object>()
        };
    }

    /// <summary>
    /// Extracts specific fields from the notification's JSON Data field.
    /// </summary>
    /// <param name="notification">The notification containing the data.</param>
    /// <param name="fieldNames">The field names to extract.</param>
    /// <returns>Array of extracted values.</returns>
    private object[] ExtractParameters(UserNotification notification, params string[] fieldNames)
    {
        if (string.IsNullOrEmpty(notification.Data))
            return fieldNames.Select(f => (object)string.Empty).ToArray();

        try
        {
            var data = System.Text.Json.JsonDocument.Parse(notification.Data);
            var result = new List<object>();

            foreach (var fieldName in fieldNames)
            {
                if (data.RootElement.TryGetProperty(fieldName, out var element))
                {
                    var value = element.GetString() ?? string.Empty;

                    // Special handling for ReactionType - translate it
                    if (fieldName == "ReactionType")
                    {
                        value = TranslateReactionType(value);
                    }

                    result.Add(value);
                }
                else
                {
                    result.Add(string.Empty);
                }
            }

            return result.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing notification data for notification {NotificationId}", notification.Id);
            return fieldNames.Select(f => (object)string.Empty).ToArray();
        }
    }

    /// <summary>
    /// Translates a reaction type enum value to its localized string.
    /// </summary>
    /// <param name="reactionType">The reaction type (e.g., "Like", "Love").</param>
    /// <returns>The localized reaction type string.</returns>
    private string TranslateReactionType(string reactionType)
    {
        var localizer = _localizerFactory.Create(typeof(Resources.Notifications.NotificationTemplates));

        var key = reactionType switch
        {
            "Like" => "ReactionLike",
            "Love" => "ReactionLove",
            "Care" => "ReactionCare",
            "Haha" => "ReactionHaha",
            "Wow" => "ReactionWow",
            "Sad" => "ReactionSad",
            "Angry" => "ReactionAngry",
            _ => null
        };

        if (key == null)
            return reactionType.ToLower();

        var translated = localizer[key];
        return translated.ResourceNotFound ? reactionType.ToLower() : translated.Value;
    }
}
