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

        public DbSet<User> Users { get; set; }
        public DbSet<Community> Communities { get; set; }
        public DbSet<CommunityMember> CommunityMembers { get; set; }
        public DbSet<Event> Events { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // PostgreSQL'de tablo isimlerini küçük harf ve çoğul yapalım (Best practice)
            modelBuilder.Entity<User>().ToTable("platform_users");
            modelBuilder.Entity<Community>().ToTable("communities");
            modelBuilder.Entity<CommunityMember>().ToTable("community_members");

            
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<Community>().HasQueryFilter(c => !c.IsDeleted);
        }
    }
}