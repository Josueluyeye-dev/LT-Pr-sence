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
        AjouterColonneSiAbsente(db, "Parametres", "HeureDebutTravail", "TEXT NOT NULL DEFAULT '07:30'");
        AjouterColonneSiAbsente(db, "Parametres", "HeureLimiteTolerance", "TEXT NOT NULL DEFAULT '07:40'");
        AjouterColonneSiAbsente(db, "Parametres", "HeureFinTravail", "TEXT NOT NULL DEFAULT '17:00'");
        AjouterColonneSiAbsente(db, "Parametres", "NotificationsWindowsActives", "INTEGER NOT NULL DEFAULT 1");
        AjouterColonneSiAbsente(db, "Parametres", "DemarrerAvecWindows", "INTEGER NOT NULL DEFAULT 1");

        if (!db.Parametres.Any(x => x.Id == ParametresApplication.SingletonId))
        {
            db.Parametres.Add(new ParametresApplication());
            db.SaveChanges();
            return;
        }

        var p = db.Parametres.First(x => x.Id == ParametresApplication.SingletonId);
        var changed = false;
        if (string.IsNullOrWhiteSpace(p.HeureDebutTravail))
        {
            p.HeureDebutTravail = Services.PresenceCalculService.FormatHhMm(Services.PresenceCalculService.HeureDebutDefaut);
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(p.HeureLimiteTolerance))
        {
            p.HeureLimiteTolerance = Services.PresenceCalculService.FormatHhMm(Services.PresenceCalculService.HeureLimiteDefaut);
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(p.HeureFinTravail))
        {
            p.HeureFinTravail = Services.PresenceCalculService.FormatHhMm(Services.PresenceCalculService.HeureFinDefaut);
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(p.NomEntreprise) ||
            string.Equals(p.NomEntreprise.Trim(), "Mon entreprise", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.NomEntreprise.Trim(), "Mon Entreprise", StringComparison.OrdinalIgnoreCase))
        {
            p.NomEntreprise = "LT Services";
            changed = true;
        }

        if (changed)
            db.SaveChanges();
    }

    private static void AjouterColonneSiAbsente(PresenceDbContext db, string table, string colonne, string definition)
    {
        var existe = false;
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA table_info({table});";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(Convert.ToString(reader["name"]), colonne, StringComparison.OrdinalIgnoreCase))
                {
                    existe = true;
                    break;
                }
            }
        }

        if (!existe)
            db.Database.ExecuteSqlRaw($"ALTER TABLE {table} ADD COLUMN {colonne} {definition};");
    }
}
