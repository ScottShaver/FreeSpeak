using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FreeSpeakWeb.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<CommentLike> CommentLikes { get; set; }
        public DbSet<PostImage> PostImages { get; set; }
        public DbSet<PinnedPost> PinnedPosts { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
        public DbSet<PostNotificationMute> PostNotificationMutes { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupRule> GroupRules { get; set; }
        public DbSet<GroupJoinRequest> GroupJoinRequests { get; set; }
        public DbSet<GroupUser> GroupUsers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Friendship entity
            modelBuilder.Entity<Friendship>(entity =>
            {
                entity.HasKey(f => f.Id);

                // Configure relationships
                entity.HasOne(f => f.Requester)
                    .WithMany()
                    .HasForeignKey(f => f.RequesterId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.Addressee)
                    .WithMany()
                    .HasForeignKey(f => f.AddresseeId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Create indexes for better query performance
                entity.HasIndex(f => f.RequesterId);
                entity.HasIndex(f => f.AddresseeId);
                entity.HasIndex(f => f.Status);
                entity.HasIndex(f => new { f.RequesterId, f.AddresseeId }).IsUnique();
            });

            // Configure Post entity
            modelBuilder.Entity<Post>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.HasOne(p => p.Author)
                    .WithMany()
                    .HasForeignKey(p => p.AuthorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(p => p.Content)
                    .IsRequired();

                // Create indexes
                entity.HasIndex(p => p.AuthorId);
                entity.HasIndex(p => p.CreatedAt);
            });

            // Configure Comment entity
            modelBuilder.Entity<Comment>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.HasOne(c => c.Post)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(c => c.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Author)
                    .WithMany()
                    .HasForeignKey(c => c.AuthorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.ParentComment)
                    .WithMany(c => c.Replies)
                    .HasForeignKey(c => c.ParentCommentId)
                    .OnDelete(DeleteBehavior.Cascade); // Added cascade delete for nested comments

                entity.Property(c => c.Content)
                    .IsRequired();

                // Create indexes
                entity.HasIndex(c => c.PostId);
                entity.HasIndex(c => c.AuthorId);
                entity.HasIndex(c => c.ParentCommentId);
                entity.HasIndex(c => c.CreatedAt);
            });

            // Configure Like entity
            modelBuilder.Entity<Like>(entity =>
            {
                entity.HasKey(l => l.Id);

                entity.HasOne(l => l.Post)
                    .WithMany(p => p.Likes)
                    .HasForeignKey(l => l.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.User)
                    .WithMany()
                    .HasForeignKey(l => l.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Ensure a user can only like a post once
                entity.HasIndex(l => new { l.PostId, l.UserId }).IsUnique();

                // Create additional indexes
                entity.HasIndex(l => l.PostId);
                entity.HasIndex(l => l.UserId);
            });

            // Configure CommentLike entity
            modelBuilder.Entity<CommentLike>(entity =>
            {
                entity.HasKey(cl => cl.Id);

                entity.HasOne(cl => cl.Comment)
                    .WithMany()
                    .HasForeignKey(cl => cl.CommentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(cl => cl.User)
                    .WithMany()
                    .HasForeignKey(cl => cl.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Ensure a user can only like a comment once
                entity.HasIndex(cl => new { cl.CommentId, cl.UserId }).IsUnique();

                // Create additional indexes
                entity.HasIndex(cl => cl.CommentId);
                entity.HasIndex(cl => cl.UserId);
            });

            // Configure PostImage entity
            modelBuilder.Entity<PostImage>(entity =>
            {
                entity.HasKey(pi => pi.Id);

                entity.HasOne(pi => pi.Post)
                    .WithMany(p => p.Images)
                    .HasForeignKey(pi => pi.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(pi => pi.ImageUrl)
                    .IsRequired();

                // Create indexes
                entity.HasIndex(pi => pi.PostId);
                entity.HasIndex(pi => new { pi.PostId, pi.DisplayOrder });
            });

            // Configure PinnedPost entity
            modelBuilder.Entity<PinnedPost>(entity =>
            {
                entity.HasKey(pp => pp.Id);

                entity.HasOne(pp => pp.User)
                    .WithMany()
                    .HasForeignKey(pp => pp.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(pp => pp.Post)
                    .WithMany()
                    .HasForeignKey(pp => pp.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Ensure a user can only pin a post once
                entity.HasIndex(pp => new { pp.UserId, pp.PostId }).IsUnique();

                // Create additional indexes
                entity.HasIndex(pp => pp.UserId);
                entity.HasIndex(pp => pp.PostId);
                entity.HasIndex(pp => pp.PinnedAt);
            });

            // Configure UserNotification entity
            modelBuilder.Entity<UserNotification>(entity =>
            {
                entity.HasKey(n => n.Id);

                entity.HasOne(n => n.User)
                    .WithMany()
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(n => n.Message)
                    .IsRequired();

                // Create indexes for efficient querying
                entity.HasIndex(n => n.UserId);
                entity.HasIndex(n => n.CreatedAt);
                entity.HasIndex(n => n.IsRead);
                entity.HasIndex(n => n.Type);
                entity.HasIndex(n => n.ExpiresAt);
                entity.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
            });

            // Configure UserPreference entity
            modelBuilder.Entity<UserPreference>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.HasOne(p => p.User)
                    .WithMany()
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Create indexes for better query performance
                entity.HasIndex(p => p.UserId);
                entity.HasIndex(p => p.PreferenceType);
                entity.HasIndex(p => new { p.UserId, p.PreferenceType }).IsUnique();
            });

            // Configure PostNotificationMute entity
            modelBuilder.Entity<PostNotificationMute>(entity =>
            {
                entity.HasKey(m => m.Id);

                entity.HasOne(m => m.Post)
                    .WithMany()
                    .HasForeignKey(m => m.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.User)
                    .WithMany()
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Create indexes for better query performance
                entity.HasIndex(m => m.PostId);
                entity.HasIndex(m => m.UserId);
                // Unique constraint: a user can only mute a post once
                entity.HasIndex(m => new { m.PostId, m.UserId }).IsUnique();
            });

            // Configure Group entity
            modelBuilder.Entity<Group>(entity =>
            {
                entity.HasKey(g => g.Id);

                entity.HasOne(g => g.Creator)
                    .WithMany()
                    .HasForeignKey(g => g.CreatorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(g => g.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(g => g.Description)
                    .IsRequired()
                    .HasMaxLength(2000);

                entity.Property(g => g.HeaderImageUrl)
                    .HasMaxLength(500);

                entity.Property(g => g.VerticalHeaderImageUrl)
                    .HasMaxLength(500);

                entity.Property(g => g.WebsiteUrl)
                    .HasMaxLength(500);

                // Create indexes for better query performance
                entity.HasIndex(g => g.CreatorId);
                entity.HasIndex(g => g.CreatedAt);
                entity.HasIndex(g => g.LastActiveAt);
                entity.HasIndex(g => g.IsPublic);
                entity.HasIndex(g => g.IsHidden);
                entity.HasIndex(g => g.Name);
                // Composite index for discovering active public groups
                entity.HasIndex(g => new { g.IsPublic, g.IsHidden, g.LastActiveAt });
            });

            // Configure GroupRule entity
            modelBuilder.Entity<GroupRule>(entity =>
            {
                entity.HasKey(gr => gr.Id);

                entity.HasOne(gr => gr.Group)
                    .WithMany()
                    .HasForeignKey(gr => gr.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(gr => gr.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(gr => gr.Description)
                    .IsRequired()
                    .HasMaxLength(1000);

                // Create indexes for better query performance
                entity.HasIndex(gr => gr.GroupId);
                entity.HasIndex(gr => new { gr.GroupId, gr.Order });
            });

            // Configure GroupJoinRequest entity
            modelBuilder.Entity<GroupJoinRequest>(entity =>
            {
                entity.HasKey(gjr => gjr.Id);

                entity.HasOne(gjr => gjr.Group)
                    .WithMany()
                    .HasForeignKey(gjr => gjr.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(gjr => gjr.User)
                    .WithMany()
                    .HasForeignKey(gjr => gjr.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Create indexes for better query performance
                entity.HasIndex(gjr => gjr.GroupId);
                entity.HasIndex(gjr => gjr.UserId);
                entity.HasIndex(gjr => gjr.RequestedAt);
                // Unique constraint: a user can only have one pending request per group
                entity.HasIndex(gjr => new { gjr.GroupId, gjr.UserId }).IsUnique();
            });

            // Configure GroupUser entity
            modelBuilder.Entity<GroupUser>(entity =>
            {
                entity.HasKey(gu => gu.Id);

                entity.HasOne(gu => gu.Group)
                    .WithMany()
                    .HasForeignKey(gu => gu.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(gu => gu.User)
                    .WithMany()
                    .HasForeignKey(gu => gu.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Create indexes for better query performance
                entity.HasIndex(gu => gu.GroupId);
                entity.HasIndex(gu => gu.UserId);
                entity.HasIndex(gu => gu.JoinedAt);
                entity.HasIndex(gu => gu.LastActiveAt);
                entity.HasIndex(gu => gu.IsAdmin);
                entity.HasIndex(gu => gu.IsModerator);
                // Unique constraint: a user can only be a member of a group once
                entity.HasIndex(gu => new { gu.GroupId, gu.UserId }).IsUnique();
                // Composite index for finding group admins/moderators
                entity.HasIndex(gu => new { gu.GroupId, gu.IsAdmin, gu.IsModerator });
            });
        }
    }
}
