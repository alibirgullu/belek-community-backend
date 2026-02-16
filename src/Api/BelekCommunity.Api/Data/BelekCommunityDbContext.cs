using BelekCommunity.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace BelekCommunity.Api.Data
{
    // Hatanın sebebi bu satırın veya süslü parantezlerin silinmesiydi
    public class BelekCommunityDbContext : DbContext
    {
        public BelekCommunityDbContext(DbContextOptions<BelekCommunityDbContext> options)
            : base(options)
        {
        }

        // Tablo temsilleri
        public DbSet<User> Users { get; set; }
        public DbSet<Community> Communities { get; set; }
        public DbSet<CommunityMember> CommunityMembers { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Announcement> Announcements { get; set; }

        // Veritabanı ayarları
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            
            modelBuilder.HasDefaultSchema("belek_student_community");

            // 2. Tablo eşleştirmeleri
            modelBuilder.Entity<User>().ToTable("platform_users");
            modelBuilder.Entity<Community>().ToTable("communities");
            modelBuilder.Entity<CommunityMember>().ToTable("community_members");
            modelBuilder.Entity<Event>().ToTable("events");
            modelBuilder.Entity<Announcement>().ToTable("announcements");
            

            // 3. Global Filtreler (Silinmiş kayıtları getirmeme)
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<Community>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Event>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Announcement>().HasQueryFilter(a => !a.IsDeleted);
        }
    }
}