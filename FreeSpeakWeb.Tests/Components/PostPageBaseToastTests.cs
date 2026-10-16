using System.Globalization;
using Bunit;
using FluentAssertions;
using FreeSpeakWeb.Components.Pages.Base;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FreeSpeakWeb.Tests.Components;

/// <summary>
/// Renders a minimal <see cref="PostPageBase{TPost, TComment}"/> descendant and verifies the toasts
/// it raises are localized for the current UI culture, while service-supplied error messages pass through.
/// </summary>
public class PostPageBaseToastTests : BunitContext
{
    private sealed class Harness : PostPageBase<object, object>
    {
        public (bool Success, string? ErrorMessage) Result { get; set; } = (false, null);
        public bool Throw { get; set; }

        protected override string? CurrentUserId => "user";
        protected override string? CurrentUserImageUrl => null;
        protected override string? CurrentUserName => "User";

        private Task<(bool, string?)> Call() =>
            Throw ? throw new InvalidOperationException("boom") : Task.FromResult(Result);

        public Task AddComment() => HandleCommentAdded((1, "x"));
        public Task AddReaction() => HandlePostReactionChanged(1, LikeType.Like);
        public Task RemoveReaction() => HandleRemovePostReaction(1);

        protected override Task<(bool Success, string? ErrorMessage)> AddCommentToPostAsync(int postId, string userId, string content) => Call();
        protected override Task<(bool Success, string? ErrorMessage)> AddReplyToPostAsync(int postId, string userId, string content, int? imageId, int parentCommentId) => Call();
        protected override Task<object?> GetCommentByIdAsync(int commentId) => Task.FromResult<object?>(null);
        protected override int GetPostIdFromComment(object comment) => 1;
        protected override void IncrementPostCommentCount(int postId) { }
        protected override Task<(bool Success, string? ErrorMessage)> AddOrUpdatePostReactionAsync(int postId, string userId, LikeType reactionType) => Call();
        protected override Task<(bool Success, string? ErrorMessage)> RemovePostReactionAsync(int postId, string userId) => Call();
        protected override Task<Dictionary<LikeType, int>> GetPostReactionBreakdownAsync(int postId) => Task.FromResult(new Dictionary<LikeType, int>());
        protected override void IncrementPostLikeCount(int postId) { }
        protected override void DecrementPostLikeCount(int postId) { }
        protected override Task<(bool Success, string? ErrorMessage)> AddOrUpdateCommentReactionAsync(int commentId, string userId, LikeType reactionType) => Call();
        protected override Task<(bool Success, string? ErrorMessage)> RemoveCommentReactionAsync(int commentId, string userId) => Call();
        protected override Task<int?> FindPostIdForCommentAsync(int commentId) => Task.FromResult<int?>(1);
        protected override Task<(bool Success, string? ErrorMessage, int DeletedCount)> DeleteCommentAsync(int commentId, string userId) =>
            Throw ? throw new InvalidOperationException("boom") : Task.FromResult((Result.Success, Result.ErrorMessage, 0));
        protected override Task<(bool Success, string? ErrorMessage)> UpdateCommentAsync(int commentId, string userId, string newContent) => Call();
        protected override Task<(bool Success, string? ErrorMessage)> ReportCommentAsync(int commentId, string userId) => Call();
        protected override void DecrementPostCommentCount(int postId, int decrementBy = 1) { }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) { }
    }

    private readonly AlertService _alerts = new();

    private Harness Create()
    {
        Services.AddLocalization();
        Services.AddSingleton(_alerts);
        Services.AddSingleton(new Mock<IAuditLogRepository>().Object);
        JSInterop.Mode = JSRuntimeMode.Loose;
        return Render<Harness>().Instance;
    }

    private string? LastError() => _alerts.GetAlerts().LastOrDefault()?.Message;

    /// <summary>The generic failure toast is localized for the current UI culture.</summary>
    [Theory]
    [InlineData("en", "Failed to add reaction.")]
    [InlineData("de", "Reaktion konnte nicht hinzugefügt werden.")]
    [InlineData("ja", null)]
    public async Task FailureToast_UsesCurrentUiCulture(string culture, string? expected)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            var page = Create();
            await page.AddReaction();

            var message = LastError();
            message.Should().NotBeNullOrWhiteSpace();
            if (expected != null) message.Should().Be(expected);
            else message.Should().NotBe("Failed to add reaction.");
        }
        finally { CultureInfo.CurrentUICulture = original; }
    }

    /// <summary>The exception toast is localized and not English for non-English cultures.</summary>
    [Theory]
    [InlineData("de")]
    [InlineData("ja")]
    public async Task ErrorToast_IsLocalized(string culture)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            var page = Create();
            page.Throw = true;
            await page.AddComment();

            LastError().Should().NotBeNullOrWhiteSpace().And.NotBe("An error occurred while adding your comment.");
        }
        finally { CultureInfo.CurrentUICulture = original; }
    }

    /// <summary>A service-supplied error message is displayed unchanged.</summary>
    [Fact]
    public async Task ServiceErrorMessage_IsPassedThroughUnchanged()
    {
        var page = Create();
        page.Result = (false, "Service says no");
        await page.AddComment();

        LastError().Should().Be("Service says no");
    }
}
