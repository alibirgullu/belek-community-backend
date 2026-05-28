using BelekCommunity.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BelekCommunity.Api.Data
{
    public class BelekCommunityDbContext : DbContext
    {
        public BelekCommunityDbContext(DbContextOptions<BelekCommunityDbContext> options)
            : base(options)
        {
        }

        public DbSet<MainUser> MainUsers { get; set; } // public.users
        public DbSet<UserAuth> UserAuths { get; set; } // public.user_auth
        public DbSet<User> Users { get; set; }         // belek...platform_users

        public DbSet<Community> Communities { get; set; }
        public DbSet<CommunityMember> CommunityMembers { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<CommunityRole> CommunityRoles { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<CommunityCategory> CommunityCategories { get; set; }
        public DbSet<EventParticipant> EventParticipants { get; set; }
        public DbSet<EventFeedback> EventFeedbacks { get; set; }
        public DbSet<PlatformUserDetail> PlatformUserDetails { get; set; }
        public DbSet<UserDevice> UserDevices { get; set; }
        public DbSet<UserRefreshToken> UserRefreshTokens { get; set; }
        public DbSet<SystemLog> SystemLogs { get; set; }
        public DbSet<AiChatLog> AiChatLogs { get; set; }
        public DbSet<CommunityMessage> CommunityMessages { get; set; }
        public DbSet<CommunityMessageRead> CommunityMessageReads { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("belek_student_community");

            modelBuilder.Entity<MainUser>().ToTable("users", "public");
            modelBuilder.Entity<UserAuth>().ToTable("user_auth", "public");

            modelBuilder.Entity<MainUser>()
                .HasOne(u => u.UserAuth)
                .WithOne(ua => ua.MainUser)
                .HasForeignKey<UserAuth>(ua => ua.UserId);

            modelBuilder.Entity<User>().ToTable("platform_users");
            modelBuilder.Entity<Community>().ToTable("communities");
            modelBuilder.Entity<CommunityMember>().ToTable("community_members");
            modelBuilder.Entity<Event>().ToTable("events");
            modelBuilder.Entity<Announcement>().ToTable("announcements");
            modelBuilder.Entity<CommunityRole>().ToTable("community_roles");
            modelBuilder.Entity<Notification>().ToTable("notifications");

            
            modelBuilder.Entity<CommunityCategory>().ToTable("community_categories");
            modelBuilder.Entity<EventParticipant>().ToTable("event_participants");
            modelBuilder.Entity<EventFeedback>().ToTable("event_feedbacks");
            modelBuilder.Entity<PlatformUserDetail>().ToTable("platform_user_details"); 
            modelBuilder.Entity<UserDevice>().ToTable("user_devices");
            modelBuilder.Entity<UserRefreshToken>().ToTable("user_refresh_tokens");
            modelBuilder.Entity<SystemLog>().ToTable("system_logs");
            modelBuilder.Entity<AiChatLog>().ToTable("ai_chat_logs");
            modelBuilder.Entity<CommunityMessage>().ToTable("community_messages");
            modelBuilder.Entity<CommunityMessageRead>().ToTable("community_message_reads");

            modelBuilder.Entity<CommunityMessageRead>()
                .HasKey(mr => new { mr.MessageId, mr.PlatformUserId });

            // Global Filtreler (Silinmiş verileri otomatik gizle)
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<Community>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Event>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<EventParticipant>().HasQueryFilter(ep => !ep.IsDeleted);
            modelBuilder.Entity<EventFeedback>().HasQueryFilter(ef => !ef.IsDeleted);
            modelBuilder.Entity<Announcement>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<CommunityMessage>().HasQueryFilter(m => !m.IsDeleted);
        }
    }
}