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
        public DbSet<User> Users { get; set; }         // belek...platform_users

        
        public DbSet<Community> Communities { get; set; }
        public DbSet<CommunityMember> CommunityMembers { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<CommunityRole> CommunityRoles { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Varsayılan şeman senin şeman
            modelBuilder.HasDefaultSchema("belek_student_community");

            // --- 1. LOGIN TABLOSU (PUBLIC) ---
            modelBuilder.Entity<MainUser>()
                .ToTable("users", "public"); // Şema: public, Tablo: users

            // --- 2. SENİN TABLOLARIN ---
            modelBuilder.Entity<User>().ToTable("platform_users");
            modelBuilder.Entity<Community>().ToTable("communities");
            modelBuilder.Entity<CommunityMember>().ToTable("community_members");
            modelBuilder.Entity<Event>().ToTable("events");
            modelBuilder.Entity<Announcement>().ToTable("announcements");
            modelBuilder.Entity<CommunityRole>().ToTable("community_roles");
            modelBuilder.Entity<Notification>().ToTable("notifications");
            // Global Filtreler
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            // Diğer filtreler...
        }
    }
}