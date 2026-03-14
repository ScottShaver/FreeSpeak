using Bunit;
using FreeSpeakWeb.Components.SocialFeed;
using FreeSpeakWeb.Components.Shared;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

namespace FreeSpeakWeb.Tests.Components;

public class PostEditModalTests : TestContext
{
    public PostEditModalTests()
    {
        // Setup JSRuntime to return a mock module
        var mockJsRuntime = new Mock<IJSRuntime>();
        var mockModule = new Mock<IJSObjectReference>();
        mockJsRuntime.Setup(js => js.InvokeAsync<IJSObjectReference>("import", It.IsAny<object[]>()))
            .ReturnsAsync(mockModule.Object);

        // Setup real ImageUploadService with in-memory dependencies
        var mockContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        var mockLogger = new Mock<ILogger<ImageUploadService>>();
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

        // Setup security services
        var mockFileSignatureValidator = new Mock<IFileSignatureValidator>();
        mockFileSignatureValidator.Setup(v => v.ValidateFileSignature(It.IsAny<byte[]>(), It.IsAny<string>()))
            .Returns((true, (string?)null));

        var mockVirusScanService = new Mock<IVirusScanService>();
        mockVirusScanService.Setup(v => v.ScanAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(VirusScanResult.Skipped());

        var imageUploadService = new ImageUploadService(
            mockContextFactory.Object, 
            mockLogger.Object, 
            mockEnvironment.Object,
            mockFileSignatureValidator.Object,
            mockVirusScanService.Object);

        Services.AddSingleton<IJSRuntime>(mockJsRuntime.Object);
        Services.AddSingleton(imageUploadService);
    }

    [Fact]
    public void PostEditModal_Initializes_WithProvidedContent()
    {
        // Arrange
        var initialContent = "Test post content";
        var postId = 123;
        var authorName = "Test User";

        // Act
        var cut = RenderComponent<PostEditModal>(parameters => parameters
            .Add(p => p.PostId, postId)
            .Add(p => p.AuthorName, authorName)
            .Add(p => p.InitialContent, initialContent)
            .Add(p => p.CurrentUserId, "user123")
            .Add(p => p.OnSave, EventCallback.Factory.Create<(int, string, List<string>, List<int>)>(this, _ => { }))
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () => { }))
        );

        // Assert
        Assert.Contains(initialContent, cut.Markup);
        Assert.Contains(authorName, cut.Markup);
    }

    [Fact]
    public void PostEditModal_Displays_InitialImages()
    {
        // Arrange
        var initialImages = new List<PostImage>
        {
            new() { Id = 1, PostId = 123, ImageUrl = "/images/test1.jpg" },
            new() { Id = 2, PostId = 123, ImageUrl = "/images/test2.jpg" }
        };

        // Act
        var cut = RenderComponent<PostEditModal>(parameters => parameters
            .Add(p => p.PostId, 123)
            .Add(p => p.AuthorName, "Test User")
            .Add(p => p.InitialContent, "Content")
            .Add(p => p.InitialImages, initialImages)
            .Add(p => p.CurrentUserId, "user123")
            .Add(p => p.OnSave, EventCallback.Factory.Create<(int, string, List<string>, List<int>)>(this, _ => { }))
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () => { }))
        );

        // Assert
        Assert.Contains("test1.jpg", cut.Markup);
        Assert.Contains("test2.jpg", cut.Markup);
    }

    [Fact]
    public void AddNewImages_AddsImagesToNewImagesList()
    {
        // Arrange
        var cut = RenderComponent<PostEditModal>(parameters => parameters
            .Add(p => p.PostId, 123)
            .Add(p => p.AuthorName, "Test User")
            .Add(p => p.InitialContent, "Content")
            .Add(p => p.CurrentUserId, "user123")
            .Add(p => p.OnSave, EventCallback.Factory.Create<(int, string, List<string>, List<int>)>(this, _ => { }))
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () => { }))
        );

        var newImages = new List<ImageUploadModal.SelectedImage>
        {
            new() { FileName = "test.jpg", DataUrl = "data:image/jpeg;base64,abc123", ContentType = "image/jpeg", Size = 1024 }
        };

        // Act - Use InvokeAsync to run on Blazor's dispatcher thread
        cut.InvokeAsync(() => cut.Instance.AddNewImages(newImages));

        // Assert - new images should be rendered in the markup
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("data:image/jpeg;base64,abc123", cut.Markup);
        });
    }

    [Fact]
    public void AddImages_Button_Text_Changes_Based_On_Images()
    {
        // Arrange - no images
        var cut = RenderComponent<PostEditModal>(parameters => parameters
            .Add(p => p.PostId, 123)
            .Add(p => p.AuthorName, "Test User")
            .Add(p => p.InitialContent, "Content")
            .Add(p => p.CurrentUserId, "user123")
            .Add(p => p.OnSave, EventCallback.Factory.Create<(int, string, List<string>, List<int>)>(this, _ => { }))
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () => { }))
            .Add(p => p.OnImageUploadStateChanged, EventCallback.Factory.Create<bool>(this, _ => { }))
            .Add(p => p.OnImagesSelected, EventCallback.Factory.Create<List<ImageUploadModal.SelectedImage>>(this, _ => { }))
        );

        // Assert - should say "Add Images" when no images
        Assert.Contains("Add Images", cut.Markup);

        // Act - add new images via the public method
        var newImages = new List<ImageUploadModal.SelectedImage>
        {
            new() { FileName = "test.jpg", DataUrl = "data:image/jpeg;base64,abc123", ContentType = "image/jpeg", Size = 1024 }
        };

        cut.InvokeAsync(() => cut.Instance.AddNewImages(newImages));

        // Assert - should say "Add More Images" when images exist
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Add More Images", cut.Markup);
        });
    }

    [Fact]
    public void RemoveImage_Adds_ImageId_To_RemovedList()
    {
        // Arrange
        var initialImages = new List<PostImage>
        {
            new() { Id = 1, PostId = 123, ImageUrl = "/images/test1.jpg" }
        };

        var saveWasCalled = false;
        var removedIds = new List<int>();

        var cut = RenderComponent<PostEditModal>(parameters => parameters
            .Add(p => p.PostId, 123)
            .Add(p => p.AuthorName, "Test User")
            .Add(p => p.InitialContent, "Content")
            .Add(p => p.InitialImages, initialImages)
            .Add(p => p.CurrentUserId, "user123")
            .Add(p => p.OnSave, EventCallback.Factory.Create<(int, string, List<string>, List<int>)>(this, data =>
            {
                saveWasCalled = true;
                removedIds = data.Item4;
            }))
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () => { }))
        );

        // Act - find and click the remove button
        var removeButton = cut.FindAll("button.remove-image-btn").FirstOrDefault();
        Assert.NotNull(removeButton);
        removeButton.Click();

        // Assert - image should be removed from display
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("test1.jpg", cut.Markup);
        });

        // Act - save changes
        var saveButton = cut.FindAll("button.btn-primary").FirstOrDefault();
        Assert.NotNull(saveButton);
        saveButton.Click();

        // Assert - removed image ID should be included in save data
        Assert.True(saveWasCalled);
        Assert.Contains(1, removedIds);
    }

    [Fact]
    public void SaveChanges_Does_Not_Save_If_Content_Is_Empty()
    {
        // Arrange
        var saveWasCalled = false;

        var cut = RenderComponent<PostEditModal>(parameters => parameters
            .Add(p => p.PostId, 123)
            .Add(p => p.AuthorName, "Test User")
            .Add(p => p.InitialContent, "   ")  // Whitespace only
            .Add(p => p.CurrentUserId, "user123")
            .Add(p => p.OnSave, EventCallback.Factory.Create<(int, string, List<string>, List<int>)>(this, _ =>
            {
                saveWasCalled = true;
            }))
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () => { }))
        );

        // Act - click save button
        var saveButton = cut.FindAll("button.btn-primary").FirstOrDefault();
        Assert.NotNull(saveButton);
        saveButton.Click();

        // Assert - save should not be called for empty content
        Assert.False(saveWasCalled);
    }

    [Fact]
    public void Cancel_Button_Calls_OnCancel_Callback()
    {
        // Arrange
        var cancelWasCalled = false;

        var cut = RenderComponent<PostEditModal>(parameters => parameters
            .Add(p => p.PostId, 123)
            .Add(p => p.AuthorName, "Test User")
            .Add(p => p.InitialContent, "Content")
            .Add(p => p.CurrentUserId, "user123")
            .Add(p => p.OnSave, EventCallback.Factory.Create<(int, string, List<string>, List<int>)>(this, _ => { }))
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () =>
            {
                cancelWasCalled = true;
            }))
        );

        // Act - click cancel button
        var cancelButton = cut.Find("button.btn-secondary");
        cancelButton.Click();

        // Assert
        Assert.True(cancelWasCalled);
    }
}
