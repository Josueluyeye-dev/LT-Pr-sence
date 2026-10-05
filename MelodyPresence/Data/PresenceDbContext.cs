using System.IO;
using MelodyPresence.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPresence.Data;

public class PresenceDbContext : DbContext
{
    public DbSet<Employe> Employes => Set<Employe>();
    public DbSet<Pointage> Pointages => Set<Pointage>();
    public DbSet<ParametresApplication> Parametres => Set<ParametresApplication>();

    public static string CheminBaseDeDonnees
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MelodyPresence");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "presence.db");
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite($"Data Source={CheminBaseDeDonnees}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employe>(e =>
        {
            e.HasIndex(x => x.Matricule).IsUnique();
            e.Property(x => x.Matricule).HasMaxLength(64).IsRequired();
            e.Property(x => x.Nom).HasMaxLength(120).IsRequired();
            e.Property(x => x.Prenom).HasMaxLength(120);
            e.Property(x => x.CodePinZk).HasMaxLength(64);
        });

        modelBuilder.Entity<Pointage>(e =>
        {
            e.HasIndex(x => new { x.EmployeId, x.Horodatage, x.Source });
            e.HasOne(x => x.Employe).WithMany().HasForeignKey(x => x.EmployeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParametresApplication>(e =>
        {
            e.HasData(new ParametresApplication { Id = ParametresApplication.SingletonId });
        });
    }

    public static void Initialiser()
    {
        using var db = new PresenceDbContext();
        db.Database.EnsureCreated();
        if (!db.Parametres.Any(x => x.Id == ParametresApplication.SingletonId))
        {
            db.Parametres.Add(new ParametresApplication());
            db.SaveChanges();
        }
    }
}
