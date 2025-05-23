using Microsoft.EntityFrameworkCore;

namespace line.Models
{
    public class ClientidContext : DbContext
    {
        public ClientidContext(DbContextOptions<ClientidContext> options) : base(options) { }

        public virtual DbSet<KewLiveModel> KewLiveModels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<KewLiveModel>().HasNoKey().ToView("Clientid");
        }
    }
}
