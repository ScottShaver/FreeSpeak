using FluentAssertions;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    public class ProfilePictureServiceTests : TestBase
    {
        private Mock<IWebHostEnvironment> CreateMockWebHostEnvironment()
        {
            var mockEnv = new Mock<IWebHostEnvironment>();
            var tempPath = Path.Combine(Path.GetTempPath(), "ProfilePictureTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempPath);
            mockEnv.Setup(e => e.ContentRootPath).Returns(tempPath); // Changed from WebRootPath to ContentRootPath
            return mockEnv;
        }

        [Fact]
        public async Task SaveProfilePictureAsync_WithValidImage_ShouldSaveSuccessfully()
        {
            // Arrange
            var mockEnv = CreateMockWebHostEnvironment();
            var logger = CreateMockLogger<ProfilePictureService>();
            var service = new ProfilePictureService(mockEnv.Object, logger);

            // Create a simple 1x1 pixel PNG image
            var imageBytes = CreateSimpleTestImage();
            var imageStream = new MemoryStream(imageBytes);

            try
            {
                // Act
                var (success, errorMessage, relativeUrl) = await service.SaveProfilePictureAsync(imageStream, "testuser");

                // Assert
                success.Should().BeTrue();
                errorMessage.Should().BeNull();
                relativeUrl.Should().Be("/api/secure-files/profile-picture/testuser"); // Updated to match actual URL format

                // Verify file was created
                var expectedPath = Path.Combine(mockEnv.Object.ContentRootPath, "AppData", "images", "profiles", "testuser.jpg"); // Updated path
                File.Exists(expectedPath).Should().BeTrue();
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(mockEnv.Object.ContentRootPath))
                {
                    Directory.Delete(mockEnv.Object.ContentRootPath, true);
                }
            }
        }

        [Fact]
        public async Task SaveProfilePictureAsync_WithExcessiveFileSize_ShouldReturnError()
        {
            // Arrange
            var mockEnv = CreateMockWebHostEnvironment();
            var logger = CreateMockLogger<ProfilePictureService>();
            var service = new ProfilePictureService(mockEnv.Object, logger);

            // Create a stream that exceeds 5MB
            var largeData = new byte[6 * 1024 * 1024]; // 6MB
            var largeStream = new MemoryStream(largeData);

            try
            {
                // Act
                var (success, errorMessage, relativeUrl) = await service.SaveProfilePictureAsync(largeStream, "testuser");

                // Assert
                success.Should().BeFalse();
                errorMessage.Should().Contain("5MB");
                relativeUrl.Should().BeNull();
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(mockEnv.Object.ContentRootPath))
                {
                    Directory.Delete(mockEnv.Object.ContentRootPath, true);
                }
            }
        }

        [Fact]
        public void ProfilePictureExists_WhenFileExists_ShouldReturnTrue()
        {
            // Arrange
            var mockEnv = CreateMockWebHostEnvironment();
            var logger = CreateMockLogger<ProfilePictureService>();
            var service = new ProfilePictureService(mockEnv.Object, logger);

            var profilesPath = Path.Combine(mockEnv.Object.ContentRootPath, "AppData", "images", "profiles");
            Directory.CreateDirectory(profilesPath);
            var filePath = Path.Combine(profilesPath, "testuser.jpg");
            File.WriteAllText(filePath, "test content");

            try
            {
                // Act
                var exists = service.ProfilePictureExists("testuser");

                // Assert
                exists.Should().BeTrue();
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(mockEnv.Object.ContentRootPath))
                {
                    Directory.Delete(mockEnv.Object.ContentRootPath, true);
                }
            }
        }

        [Fact]
        public void ProfilePictureExists_WhenFileDoesNotExist_ShouldReturnFalse()
        {
            // Arrange
            var mockEnv = CreateMockWebHostEnvironment();
            var logger = CreateMockLogger<ProfilePictureService>();
            var service = new ProfilePictureService(mockEnv.Object, logger);

            try
            {
                // Act
                var exists = service.ProfilePictureExists("nonexistent");

                // Assert
                exists.Should().BeFalse();
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(mockEnv.Object.ContentRootPath))
                {
                    Directory.Delete(mockEnv.Object.ContentRootPath, true);
                }
            }
        }

        [Fact]
        public void DeleteProfilePicture_WhenFileExists_ShouldDeleteFile()
        {
            // Arrange
            var mockEnv = CreateMockWebHostEnvironment();
            var logger = CreateMockLogger<ProfilePictureService>();
            var service = new ProfilePictureService(mockEnv.Object, logger);

            var profilesPath = Path.Combine(mockEnv.Object.ContentRootPath, "AppData", "images", "profiles");
            Directory.CreateDirectory(profilesPath);
            var filePath = Path.Combine(profilesPath, "testuser.jpg");
            File.WriteAllText(filePath, "test content");

            try
            {
                // Act
                service.DeleteProfilePicture("testuser");

                // Assert
                File.Exists(filePath).Should().BeFalse();
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(mockEnv.Object.ContentRootPath))
                {
                    Directory.Delete(mockEnv.Object.ContentRootPath, true);
                }
            }
        }

        [Fact]
        public async Task GetProfilePictureAsync_WhenFileExists_ShouldReturnBytes()
        {
            // Arrange
            var mockEnv = CreateMockWebHostEnvironment();
            var logger = CreateMockLogger<ProfilePictureService>();
            var service = new ProfilePictureService(mockEnv.Object, logger);

            var profilesPath = Path.Combine(mockEnv.Object.ContentRootPath, "AppData", "images", "profiles");
            Directory.CreateDirectory(profilesPath);
            var filePath = Path.Combine(profilesPath, "testuser.jpg");
            var testData = new byte[] { 1, 2, 3, 4, 5 };
            await File.WriteAllBytesAsync(filePath, testData);

            try
            {
                // Act
                var result = await service.GetProfilePictureAsync("testuser");

                // Assert
                result.Should().NotBeNull();
                result.Should().Equal(testData);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(mockEnv.Object.ContentRootPath))
                {
                    Directory.Delete(mockEnv.Object.ContentRootPath, true);
                }
            }
        }

        [Fact]
        public async Task GetProfilePictureAsync_WhenFileDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var mockEnv = CreateMockWebHostEnvironment();
            var logger = CreateMockLogger<ProfilePictureService>();
            var service = new ProfilePictureService(mockEnv.Object, logger);

            try
            {
                // Act
                var result = await service.GetProfilePictureAsync("nonexistent");

                // Assert
                result.Should().BeNull();
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(mockEnv.Object.ContentRootPath))
                {
                    Directory.Delete(mockEnv.Object.ContentRootPath, true);
                }
            }
        }

        /// <summary>
        /// Creates a simple valid image for testing
        /// </summary>
        private byte[] CreateSimpleTestImage()
        {
            // Create a minimal valid PNG file (1x1 transparent pixel)
            return new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1 dimensions
                0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
                0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, // IDAT chunk
                0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
                0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
                0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, // IEND chunk
                0x42, 0x60, 0x82
            };
        }
    }
}
