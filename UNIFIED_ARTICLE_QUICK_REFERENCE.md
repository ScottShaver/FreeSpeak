# UnifiedArticle Quick Reference

## Basic Usage

### User Feed Post
```razor
<UnifiedArticle 
    ArticlePostType="PostType.UserPost"
    PostId="@postId"
    AuthorId="@authorId"
    AuthorName="@authorName"
    AuthorImageUrl="@imageUrl"
    CreatedAt="@createdAt"
    AudienceType="@audienceType"
    LikeCount="@likeCount"
    CommentCount="@commentCount"
    ShareCount="@shareCount"
    CurrentUserId="@currentUserId"
    CurrentUserImageUrl="@currentUserImageUrl"
    CurrentUserName="@currentUserName"
    ReactionBreakdown="@reactionBreakdown"
    UserReaction="@userReaction"
    IsPinned="@isPinned"
    Images="@images"
    OnReactionChanged="@HandleReactionChanged"
    OnRemoveReaction="@HandleRemoveReaction"
    OnPinPost="@HandlePinPost"
    OnUnpinPost="@HandleUnpinPost"
    OnAudienceTypeChanged="@HandleAudienceChanged"
    OnPostDeleted="@HandlePostDeleted"
    OnEditPost="@HandleEditPost"
    OnCommentAdded="@OnCommentAdded"
    OnShowPostDetail="@ShowPostDetail"
    OnShowComments="@ShowComments">
    <ArticleContent>
        @((MarkupString)FormatContent(post.Content))
    </ArticleContent>
</UnifiedArticle>
```

### Group Post
```razor
<UnifiedArticle 
    ArticlePostType="PostType.GroupPost"
    PostId="@postId"
    GroupId="@groupId"
    GroupName="@groupName"
    AuthorGroupPoints="@authorPoints"
    AuthorId="@authorId"
    AuthorName="@authorName"
    AuthorImageUrl="@imageUrl"
    CreatedAt="@createdAt"
    LikeCount="@likeCount"
    CommentCount="@commentCount"
    ShareCount="@shareCount"
    CurrentUserId="@currentUserId"
    CurrentUserImageUrl="@currentUserImageUrl"
    CurrentUserName="@currentUserName"
    ReactionBreakdown="@reactionBreakdown"
    UserReaction="@userReaction"
    IsPinned="@isPinned"
    Images="@images"
    OnReactionChanged="@HandleReactionChanged"
    OnRemoveReaction="@HandleRemoveReaction"
    OnPinPost="@HandlePinPost"
    OnUnpinPost="@HandleUnpinPost"
    OnPostDeleted="@HandlePostDeleted"
    OnEditPost="@HandleEditPost"
    OnCommentAdded="@OnCommentAdded"
    OnShowPostDetail="@ShowPostDetail"
    OnShowComments="@ShowComments">
    <ArticleContent>
        @((MarkupString)FormatContent(post.Content))
    </ArticleContent>
</UnifiedArticle>
```

## Key Parameters

### Required Parameters (All Post Types)
- `ArticlePostType` - Must be `PostType.UserPost` or `PostType.GroupPost`
- `PostId` - The post's unique identifier
- `AuthorId` - Post author's user ID
- `AuthorName` - Display name of the author
- `CreatedAt` - When the post was created

### UserPost-Specific Parameters
- `AudienceType` - Post visibility (Public, FriendsOnly, MeOnly)
- `OnAudienceTypeChanged` - Event callback when user changes audience

### GroupPost-Specific Parameters
- `GroupId` - The group's unique identifier
- `GroupName` - Display name of the group
- `AuthorGroupPoints` - Author's point count in the group (optional)

### Common Optional Parameters
- `CurrentUserId` - Currently logged-in user (null if not authenticated)
- `LikeCount`, `CommentCount`, `ShareCount` - Engagement metrics
- `ReactionBreakdown` - Dictionary of reaction types to counts
- `UserReaction` - Current user's reaction to the post
- `IsPinned` - Whether post is pinned
- `Images` - List of PostImage objects
- `LoadCommentsInternally` - Load comments from service (default: false)
- `CommentsToShow` - Number of comments to load (default: 3)
- `IsModalView` - Render as modal/detail view (default: false)
- `IsReadOnly` - Disable interactions (default: false)

## Event Callbacks

### Post Events
- `OnReactionChanged` - User adds/changes reaction
- `OnRemoveReaction` - User removes their reaction
- `OnPinPost` - User pins the post
- `OnUnpinPost` - User unpins the post
- `OnPostDeleted` - Post is deleted
- `OnEditPost` - User requests to edit
- `OnShowPostDetail` - User clicks to view full post
- `OnShowComments` - User clicks to view comments
- `OnAudienceTypeChanged` - User changes post visibility (UserPost only)

### Comment Events
- `OnCommentAdded` - New comment added
- `OnReplySubmitted` - Reply to comment submitted
- `OnCommentReactionChanged` - Reaction added to comment
- `OnRemoveCommentReaction` - Reaction removed from comment
- `OnDeleteComment` - Comment deleted
- `OnEditComment` - Comment edit requested
- `OnCommentUpdated` - Comment content updated
- `OnReportComment` - Comment reported

## Important Notes

1. **CSS Styling**: The component uses scoped CSS from `UnifiedArticle.razor.css`
2. **Post Type**: Always specify `ArticlePostType` - it determines which features are available
3. **Group Parameters**: Only required when `ArticlePostType="PostType.GroupPost"`
4. **Audience Type**: Only used when `ArticlePostType="PostType.UserPost"`
5. **Article Content**: Must be provided as a RenderFragment in `<ArticleContent>` tags

## Common Patterns

### Loading Comments Internally
```razor
<UnifiedArticle 
    ArticlePostType="PostType.UserPost"
    PostId="@postId"
    LoadCommentsInternally="true"
    CommentsToShow="3"
    RefreshTrigger="@refreshTrigger"
    ... />
```

### Read-Only Mode (Public View)
```razor
<UnifiedArticle 
    ArticlePostType="PostType.UserPost"
    PostId="@postId"
    CurrentUserId="@null"
    IsReadOnly="true"
    ... />
```

### Modal/Detail View
```razor
<UnifiedArticle 
    ArticlePostType="PostType.GroupPost"
    PostId="@postId"
    GroupId="@groupId"
    IsModalView="true"
    CommentsToShow="100"
    ... />
```
