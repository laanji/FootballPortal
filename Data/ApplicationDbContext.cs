using Microsoft.EntityFrameworkCore;
using FootballPortal.Models;

namespace FootballPortal.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<AppUser> Users => Set<AppUser>();

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }
    }
}
