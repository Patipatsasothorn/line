using Microsoft.EntityFrameworkCore;

namespace line.Models
{
    public class oatablecontext: DbContext
    {
        public oatablecontext(DbContextOptions<oatablecontext> options) : base(options) { }

        public virtual DbSet<oatablemodel> oatablemodels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<oatablemodel>().HasNoKey().ToView("oatable");
        }
    }
}
