using Microsoft.EntityFrameworkCore;
using line.Models; // ชี้ไปที่ namespace ของ User

namespace line.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
    }
}
