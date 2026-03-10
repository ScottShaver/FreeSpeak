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
        }
    }
}
