using FluentAssertions;
using FreeSpeakWeb.Data;
using FreeSpeakWeb.DTOs;
using FreeSpeakWeb.Repositories.Abstractions;
using FreeSpeakWeb.Services;
using FreeSpeakWeb.Services.Abstractions;
using FreeSpeakWeb.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FreeSpeakWeb.Tests.Services
{
    /// <summary>
    /// Unit tests for GroupFileService.
    /// Tests file upload, download, approval workflow, permissions, virus scanning, and storage management.
    /// </summary>
    public class GroupFileServiceTests : TestBase
    {
        private static IWebHostEnvironment CreateMockWebHostEnvironment()
        {
            var mock = new Mock<IWebHostEnvironment>();
            mock.Setup(m => m.ContentRootPath).Returns(Path.GetTempPath());
            return mock.Object;
        }

        /// <summary>
        /// Creates a UserPreferenceService with a real database context factory.
        /// </summary>
        private static UserPreferenceService CreateUserPreferenceService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            var logger = new Mock<ILogger<UserPreferenceService>>();
            return new UserPreferenceService(contextFactory, logger.Object);
        }

        /// <summary>
        /// Creates a GroupMemberService with real repositories using an in-memory database.
        /// </summary>
        private static GroupMemberService CreateGroupMemberService(TestRepositoryFactory repoFactory)
        {
            var logger = new Mock<ILogger<GroupMemberService>>();
            var auditLogRepo = MockRepositories.CreateMockAuditLogRepository();
            var notificationLogger = new Mock<ILogger<NotificationService>>();
            var scopeFactory = new Mock<IServiceScopeFactory>();
            var notificationRepo = repoFactory.CreateNotificationRepository();
            var notificationService = new NotificationService(notificationRepo, repoFactory.ContextFactory, notificationLogger.Object, scopeFactory.Object, auditLogRepo.Object);
            var roleService = new Mock<IRoleService>();
            roleService.Setup(x => x.IsSystemAdministratorAsync(It.IsAny<string>())).ReturnsAsync(false);

            return new GroupMemberService(
                repoFactory.ContextFactory,
                repoFactory.CreateGroupMemberRepository(),
                repoFactory.CreateGroupRepository(),
                logger.Object,
                auditLogRepo.Object,
                notificationService,
                roleService.Object);
        }

        /// <summary>
        /// Creates a mock file signature validator.
        /// </summary>
        private static IFileSignatureValidator CreateMockFileSignatureValidator(bool isValid = true, string? errorMessage = null)
        {
            var mock = new Mock<IFileSignatureValidator>();
            mock.Setup(m => m.ValidateFileSignature(It.IsAny<byte[]>(), It.IsAny<string>()))
                .Returns((isValid, errorMessage));
            return mock.Object;
        }

        /// <summary>
        /// Creates a mock virus scan service.
        /// </summary>
        private static IVirusScanService CreateMockVirusScanService(
            bool isAvailable = true,
            bool isClean = true,
            string? virusName = null,
            string? errorMessage = null)
        {
            var mock = new Mock<IVirusScanService>();
            mock.Setup(m => m.IsAvailableAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(isAvailable);

            VirusScanResult result;
            if (!string.IsNullOrEmpty(errorMessage))
            {
                result = VirusScanResult.Error(errorMessage);
            }
            else if (!isClean && !string.IsNullOrEmpty(virusName))
            {
                result = VirusScanResult.Infected(virusName);
            }
            else if (isClean)
            {
                result = VirusScanResult.Clean();
            }
            else
            {
                result = VirusScanResult.Skipped();
            }

            mock.Setup(m => m.ScanAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
            return mock.Object;
        }

        /// <summary>
        /// Creates a GroupFileService with real repositories using an in-memory database.
        /// </summary>
        private GroupFileService CreateGroupFileService(
            TestRepositoryFactory repoFactory,
            IFileSignatureValidator? fileSignatureValidator = null,
            IVirusScanService? virusScanService = null)
        {
            var logger = CreateMockLogger<GroupFileService>();
            fileSignatureValidator ??= CreateMockFileSignatureValidator();
            virusScanService ??= CreateMockVirusScanService();
            var userPreferenceService = CreateUserPreferenceService(repoFactory.ContextFactory);
            var groupMemberService = CreateGroupMemberService(repoFactory);

            return new GroupFileService(
                repoFactory.CreateGroupFileRepository(),
                repoFactory.CreateGroupRepository(),
                repoFactory.ContextFactory,
                logger,
                CreateMockWebHostEnvironment(),
                fileSignatureValidator,
                virusScanService,
                userPreferenceService,
                groupMemberService);
        }

        #region Upload Tests

        [Fact]
        public async Task UploadFileAsync_WithValidData_ShouldSucceed()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload1");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            // Act
            var result = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                "Test description",
                fileStream,
                "text/plain",
                fileContent.Length);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
            result.File.Should().NotBeNull();
            result.File!.OriginalFileName.Should().Be("testfile.txt");
            result.File.Description.Should().Be("Test description");
            result.File.FileSize.Should().Be(fileContent.Length);
            result.File.Status.Should().Be(GroupFileStatus.Approved);
            result.IsPendingApproval.Should().BeFalse();
            result.IsPendingVirusScan.Should().BeFalse();
        }

        [Fact]
        public async Task UploadFileAsync_WithInvalidUserId_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload2");
            var service = CreateGroupFileService(repoFactory);

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            // Act
            var result = await service.UploadFileAsync(
                1,
                "invalid-user-id",
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                fileContent.Length);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Invalid user ID format");
            result.File.Should().BeNull();
        }

        [Fact]
        public async Task UploadFileAsync_WithEmptyFileName_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload3");
            var service = CreateGroupFileService(repoFactory);

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            // Act
            var result = await service.UploadFileAsync(
                1,
                Guid.NewGuid().ToString(),
                "",
                null,
                fileStream,
                "text/plain",
                fileContent.Length);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Filename is required");
            result.File.Should().BeNull();
        }

        [Fact]
        public async Task UploadFileAsync_WithZeroFileSize_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload4");
            var service = CreateGroupFileService(repoFactory);

            var fileStream = new MemoryStream();

            // Act
            var result = await service.UploadFileAsync(
                1,
                Guid.NewGuid().ToString(),
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                0);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Invalid file size");
            result.File.Should().BeNull();
        }

        [Fact]
        public async Task UploadFileAsync_WithFileSizeExceedingLimit_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload5");
            var service = CreateGroupFileService(repoFactory);

            var fileStream = new MemoryStream();
            var maxSize = 50 * 1024 * 1024;

            // Act
            var result = await service.UploadFileAsync(
                1,
                Guid.NewGuid().ToString(),
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                maxSize + 1);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("exceeds the maximum size");
            result.File.Should().BeNull();
        }

        [Fact]
        public async Task UploadFileAsync_NonMember_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload6");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user2");
            group.EnableFileUploads = true;

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            // Act
            var result = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                fileContent.Length);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("do not have permission");
            result.File.Should().BeNull();
        }

        [Fact]
        public async Task UploadFileAsync_GroupNotFound_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload7");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            // Act
            var result = await service.UploadFileAsync(
                999,
                "user1",
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                fileContent.Length);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Group not found");
            result.File.Should().BeNull();
        }

        [Fact]
        public async Task UploadFileAsync_FileUploadsDisabled_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload8");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = false;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            // Act
            var result = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                fileContent.Length);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("not enabled");
            result.File.Should().BeNull();
        }

        [Fact]
        public async Task UploadFileAsync_WithInvalidFileSignature_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload9");
            var fileSignatureValidator = CreateMockFileSignatureValidator(false, "Invalid file signature");
            var service = CreateGroupFileService(repoFactory, fileSignatureValidator);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            // Act
            var result = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                fileContent.Length);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("validation failed");
            result.File.Should().BeNull();
        }

        [Fact]
        public async Task UploadFileAsync_WithVirusDetected_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload10");
            var virusScanService = CreateMockVirusScanService(isAvailable: true, isClean: false, virusName: "EICAR-Test-File");
            var service = CreateGroupFileService(repoFactory, virusScanService: virusScanService);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            // Act
            var result = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                fileContent.Length);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Malware detected");
            result.File.Should().BeNull();
        }

        [Fact]
        public async Task UploadFileAsync_WithVirusScanUnavailable_ShouldMarkForLaterScan()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload11");
            var virusScanService = CreateMockVirusScanService(isAvailable: false);
            var service = CreateGroupFileService(repoFactory, virusScanService: virusScanService);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            // Act
            var result = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                fileContent.Length);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.File.Should().NotBeNull();
            result.IsPendingVirusScan.Should().BeTrue();
            result.File!.VirusScanCompleted.Should().BeFalse();
        }

        [Fact]
        public async Task UploadFileAsync_WithApprovalRequired_ShouldSetStatusToPending()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Upload12");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            group.RequiresFileApproval = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            // Act
            var result = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                fileContent.Length);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.File.Should().NotBeNull();
            result.File!.Status.Should().Be(GroupFileStatus.Pending);
            result.IsPendingApproval.Should().BeTrue();
        }

        #endregion

        #region Download Tests

        [Fact]
        public async Task GetFileForDownloadAsync_WithValidFile_ShouldSucceed()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Download1");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            var uploadResult = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                fileContent.Length);

            // Act
            var result = await service.GetFileForDownloadAsync(uploadResult.File!.Id, "user1");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
            result.FileStream.Should().NotBeNull();
            result.FileName.Should().Be("testfile.txt");
            result.ContentType.Should().Be("text/plain");
            result.FileSize.Should().Be(fileContent.Length);

            // Clean up
            result.FileStream?.Dispose();
        }

        [Fact]
        public async Task GetFileForDownloadAsync_FileNotFound_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Download2");
            var service = CreateGroupFileService(repoFactory);

            // Act
            var result = await service.GetFileForDownloadAsync(999, "user1");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("File not found");
            result.FileStream.Should().BeNull();
        }

        [Fact]
        public async Task GetFileForDownloadAsync_NonMember_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Download3");
            var service = CreateGroupFileService(repoFactory);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            var groupUser1 = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser1.GroupId = group.Id;
                context.GroupUsers.Add(groupUser1);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file content"u8.ToArray();
            var fileStream = new MemoryStream(fileContent);

            var uploadResult = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                fileStream,
                "text/plain",
                fileContent.Length);

            // Act
            var result = await service.GetFileForDownloadAsync(uploadResult.File!.Id, "user2");

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("do not have permission");
            result.FileStream.Should().BeNull();
        }

        #endregion

        #region File Listing Tests

        [Fact]
        public async Task GetGroupFilesAsync_ShouldReturnApprovedFilesOnly()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_List1");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            group.RequiresFileApproval = false;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Upload approved file
            var fileContent1 = "Test file 1"u8.ToArray();
            await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile1.txt",
                null,
                new MemoryStream(fileContent1),
                "text/plain",
                fileContent1.Length);

            var fileContent2 = "Test file 2"u8.ToArray();
            await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile2.txt",
                null,
                new MemoryStream(fileContent2),
                "text/plain",
                fileContent2.Length);

            // Act
            var files = await service.GetGroupFilesAsync(group.Id);

            // Assert
            files.Should().NotBeNull();
            files.Should().HaveCount(2);
            files.All(f => f.Status == GroupFileStatus.Approved).Should().BeTrue();
        }

        [Fact]
        public async Task SearchFilesAsync_ShouldReturnMatchingFiles()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Search1");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Upload files with different names
            var fileContent1 = "Test file 1"u8.ToArray();
            await service.UploadFileAsync(
                group.Id,
                "user1",
                "document.txt",
                null,
                new MemoryStream(fileContent1),
                "text/plain",
                fileContent1.Length);

            var fileContent2 = "Test file 2"u8.ToArray();
            await service.UploadFileAsync(
                group.Id,
                "user1",
                "image.jpg",
                null,
                new MemoryStream(fileContent2),
                "image/jpeg",
                fileContent2.Length);

            // Act
            var files = await service.SearchFilesAsync(group.Id, "document");

            // Assert
            files.Should().NotBeNull();
            files.Should().HaveCount(1);
            files[0].OriginalFileName.Should().Be("document.txt");
        }

        [Fact]
        public async Task GetFileCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Count1");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Upload multiple files
            for (int i = 0; i < 3; i++)
            {
                var fileContent = System.Text.Encoding.UTF8.GetBytes($"Test file {i}");
                await service.UploadFileAsync(
                    group.Id,
                    "user1",
                    $"testfile{i}.txt",
                    null,
                    new MemoryStream(fileContent),
                    "text/plain",
                    fileContent.Length);
            }

            // Act
            var count = await service.GetFileCountAsync(group.Id);

            // Assert
            count.Should().Be(3);
        }

        #endregion

        #region Approval Workflow Tests

        [Fact]
        public async Task GetPendingFilesAsync_ShouldReturnPendingFilesOnly()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Approval1");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            group.RequiresFileApproval = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Upload file (will be pending)
            var fileContent = "Test file"u8.ToArray();
            await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                new MemoryStream(fileContent),
                "text/plain",
                fileContent.Length);

            // Act
            var files = await service.GetPendingFilesAsync(group.Id);

            // Assert
            files.Should().NotBeNull();
            files.Should().HaveCount(1);
            files[0].Status.Should().Be(GroupFileStatus.Pending);
        }

        [Fact]
        public async Task ApproveFileAsync_WithPermission_ShouldSucceed()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Approval2");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var group = TestDataFactory.CreateTestGroup("admin1");
            group.EnableFileUploads = true;
            group.RequiresFileApproval = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var groupAdmin = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, admin);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                groupAdmin.GroupId = group.Id;
                context.GroupUsers.AddRange(groupUser, groupAdmin);
                await context.SaveChangesAsync();
            }

            // Upload file (will be pending)
            var fileContent = "Test file"u8.ToArray();
            var uploadResult = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                new MemoryStream(fileContent),
                "text/plain",
                fileContent.Length);

            // Act
            var (success, errorMessage) = await service.ApproveFileAsync(uploadResult.File!.Id, "admin1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            // Verify status changed
            var file = await service.GetFileByIdAsync(uploadResult.File.Id);
            file.Should().NotBeNull();
            file!.Status.Should().Be(GroupFileStatus.Approved);
        }

        [Fact]
        public async Task ApproveFileAsync_WithoutPermission_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Approval3");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            group.RequiresFileApproval = true;
            var groupUser1 = TestDataFactory.CreateTestGroupUser(1, "user1");
            var groupUser2 = TestDataFactory.CreateTestGroupUser(1, "user2");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser1.GroupId = group.Id;
                groupUser2.GroupId = group.Id;
                context.GroupUsers.AddRange(groupUser1, groupUser2);
                await context.SaveChangesAsync();
            }

            // Upload file (will be pending)
            var fileContent = "Test file"u8.ToArray();
            var uploadResult = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                new MemoryStream(fileContent),
                "text/plain",
                fileContent.Length);

            // Act - user2 tries to approve without permission
            var (success, errorMessage) = await service.ApproveFileAsync(uploadResult.File!.Id, "user2");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("do not have permission");
        }

        [Fact]
        public async Task DeclineFileAsync_WithPermission_ShouldSucceed()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Approval4");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var group = TestDataFactory.CreateTestGroup("admin1");
            group.EnableFileUploads = true;
            group.RequiresFileApproval = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var groupAdmin = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, admin);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                groupAdmin.GroupId = group.Id;
                context.GroupUsers.AddRange(groupUser, groupAdmin);
                await context.SaveChangesAsync();
            }

            // Upload file (will be pending)
            var fileContent = "Test file"u8.ToArray();
            var uploadResult = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                new MemoryStream(fileContent),
                "text/plain",
                fileContent.Length);

            // Act
            var (success, errorMessage) = await service.DeclineFileAsync(uploadResult.File!.Id, "admin1", "Not appropriate");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            // Verify file is declined
            var file = await service.GetFileByIdAsync(uploadResult.File.Id);
            file.Should().NotBeNull();
            file!.Status.Should().Be(GroupFileStatus.Declined);
        }

        #endregion

        #region Permission Tests

        [Fact]
        public async Task CanManageFilesAsync_AsAdmin_ShouldReturnTrue()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Perm1");
            var service = CreateGroupFileService(repoFactory);

            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var group = TestDataFactory.CreateTestGroup("admin1");
            var groupAdmin = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(admin);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupAdmin.GroupId = group.Id;
                context.GroupUsers.Add(groupAdmin);
                await context.SaveChangesAsync();
            }

            // Act
            var canManage = await service.CanManageFilesAsync(group.Id, "admin1");

            // Assert
            canManage.Should().BeTrue();
        }

        [Fact]
        public async Task CanManageFilesAsync_AsModerator_ShouldReturnTrue()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Perm2");
            var service = CreateGroupFileService(repoFactory);

            var mod = TestDataFactory.CreateTestUser(id: "mod1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var groupMod = TestDataFactory.CreateTestGroupUser(1, "mod1", isModerator: true);

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(mod);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupMod.GroupId = group.Id;
                context.GroupUsers.Add(groupMod);
                await context.SaveChangesAsync();
            }

            // Act
            var canManage = await service.CanManageFilesAsync(group.Id, "mod1");

            // Assert
            canManage.Should().BeTrue();
        }

        [Fact]
        public async Task CanManageFilesAsync_AsCreator_ShouldReturnTrue()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Perm3");
            var service = CreateGroupFileService(repoFactory);

            var creator = TestDataFactory.CreateTestUser(id: "creator1");
            var group = TestDataFactory.CreateTestGroup("creator1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(creator);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
            }

            // Act
            var canManage = await service.CanManageFilesAsync(group.Id, "creator1");

            // Assert
            canManage.Should().BeTrue();
        }

        [Fact]
        public async Task CanManageFilesAsync_AsRegularMember_ShouldReturnFalse()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Perm4");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var canManage = await service.CanManageFilesAsync(group.Id, "user1");

            // Assert
            canManage.Should().BeFalse();
        }

        [Fact]
        public async Task CanUploadFilesAsync_AsMember_ShouldReturnTrue()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Perm5");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var canUpload = await service.CanUploadFilesAsync(group.Id, "user1");

            // Assert
            canUpload.Should().BeTrue();
        }

        [Fact]
        public async Task CanUploadFilesAsync_NonMember_ShouldReturnFalse()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Perm6");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            group.EnableFileUploads = true;

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
            }

            // Act
            var canUpload = await service.CanUploadFilesAsync(group.Id, "user1");

            // Assert
            canUpload.Should().BeFalse();
        }

        [Fact]
        public async Task CanUploadFilesAsync_UploadsDisabled_ShouldReturnFalse()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Perm7");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            group.EnableFileUploads = false;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Act
            var canUpload = await service.CanUploadFilesAsync(group.Id, "user1");

            // Assert
            canUpload.Should().BeFalse();
        }

        #endregion

        #region Delete Tests

        [Fact]
        public async Task DeleteFileAsync_AsUploader_ShouldSucceed()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Delete1");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("creator1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file"u8.ToArray();
            var uploadResult = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                new MemoryStream(fileContent),
                "text/plain",
                fileContent.Length);

            // Act
            var (success, errorMessage) = await service.DeleteFileAsync(uploadResult.File!.Id, "user1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();

            // Verify file is deleted
            var file = await service.GetFileByIdAsync(uploadResult.File.Id);
            file.Should().BeNull();
        }

        [Fact]
        public async Task DeleteFileAsync_AsAdmin_ShouldSucceed()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Delete2");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var admin = TestDataFactory.CreateTestUser(id: "admin1");
            var group = TestDataFactory.CreateTestGroup("admin1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");
            var groupAdmin = TestDataFactory.CreateTestGroupUser(1, "admin1", isAdmin: true);

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user, admin);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                groupAdmin.GroupId = group.Id;
                context.GroupUsers.AddRange(groupUser, groupAdmin);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file"u8.ToArray();
            var uploadResult = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                new MemoryStream(fileContent),
                "text/plain",
                fileContent.Length);

            // Act
            var (success, errorMessage) = await service.DeleteFileAsync(uploadResult.File!.Id, "admin1");

            // Assert
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [Fact]
        public async Task DeleteFileAsync_WithoutPermission_ShouldFail()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Delete3");
            var service = CreateGroupFileService(repoFactory);

            var user1 = TestDataFactory.CreateTestUser(id: "user1");
            var user2 = TestDataFactory.CreateTestUser(id: "user2");
            var group = TestDataFactory.CreateTestGroup("creator1");
            group.EnableFileUploads = true;
            var groupUser1 = TestDataFactory.CreateTestGroupUser(1, "user1");
            var groupUser2 = TestDataFactory.CreateTestGroupUser(1, "user2");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.AddRange(user1, user2);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser1.GroupId = group.Id;
                groupUser2.GroupId = group.Id;
                context.GroupUsers.AddRange(groupUser1, groupUser2);
                await context.SaveChangesAsync();
            }

            var fileContent = "Test file"u8.ToArray();
            var uploadResult = await service.UploadFileAsync(
                group.Id,
                "user1",
                "testfile.txt",
                null,
                new MemoryStream(fileContent),
                "text/plain",
                fileContent.Length);

            // Act - user2 tries to delete user1's file
            var (success, errorMessage) = await service.DeleteFileAsync(uploadResult.File!.Id, "user2");

            // Assert
            success.Should().BeFalse();
            errorMessage.Should().Contain("do not have permission");
        }

        #endregion

        #region Storage Statistics Tests

        [Fact]
        public async Task GetStorageStatsAsync_ShouldReturnCorrectStatistics()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Stats1");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Upload multiple files
            var file1Content = "Small file"u8.ToArray();
            await service.UploadFileAsync(
                group.Id,
                "user1",
                "file1.txt",
                null,
                new MemoryStream(file1Content),
                "text/plain",
                file1Content.Length);

            var file2Content = "Larger file content with more data"u8.ToArray();
            await service.UploadFileAsync(
                group.Id,
                "user1",
                "file2.txt",
                null,
                new MemoryStream(file2Content),
                "text/plain",
                file2Content.Length);

            // Act
            var stats = await service.GetStorageStatsAsync(group.Id);

            // Assert
            stats.Should().NotBeNull();
            stats.TotalFileCount.Should().Be(2);
            stats.ApprovedFileCount.Should().Be(2);
            stats.PendingFileCount.Should().Be(0);
            stats.TotalStorageBytes.Should().Be(file1Content.Length + file2Content.Length);
        }

        [Fact]
        public async Task GetUserStorageUsedAsync_ShouldReturnCorrectAmount()
        {
            // Arrange
            var repoFactory = CreateTestRepositoryFactory("GroupFileTest_Stats2");
            var service = CreateGroupFileService(repoFactory);

            var user = TestDataFactory.CreateTestUser(id: "user1");
            var group = TestDataFactory.CreateTestGroup("user1");
            group.EnableFileUploads = true;
            var groupUser = TestDataFactory.CreateTestGroupUser(1, "user1");

            using (var context = await repoFactory.ContextFactory.CreateDbContextAsync())
            {
                context.Users.Add(user);
                context.Groups.Add(group);
                await context.SaveChangesAsync();
                groupUser.GroupId = group.Id;
                context.GroupUsers.Add(groupUser);
                await context.SaveChangesAsync();
            }

            // Upload files
            var fileContent = "Test file content"u8.ToArray();
            await service.UploadFileAsync(
                group.Id,
                "user1",
                "file1.txt",
                null,
                new MemoryStream(fileContent),
                "text/plain",
                fileContent.Length);

            await service.UploadFileAsync(
                group.Id,
                "user1",
                "file2.txt",
                null,
                new MemoryStream(fileContent),
                "text/plain",
                fileContent.Length);

            // Act
            var storageUsed = await service.GetUserStorageUsedAsync(group.Id, "user1");

            // Assert
            storageUsed.Should().Be(fileContent.Length * 2);
        }

        #endregion
    }
}
