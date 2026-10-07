using System.IO;
using MelodyPresence.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPresence.Data;

public class PresenceDbContext : DbContext
{
    public DbSet<Employe> Employes => Set<Employe>();
    public DbSet<Pointage> Pointages => Set<Pointage>();
    public DbSet<ParametresApplication> Parametres => Set<ParametresApplication>();
    public DbSet<BulletinPaie> Bulletins => Set<BulletinPaie>();
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();

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
            e.Property(x => x.SalaireMensuel).HasPrecision(18, 2);
            e.Property(x => x.TauxSalaireBase).HasPrecision(18, 4);
            e.Property(x => x.TauxAnciennete).HasPrecision(18, 4);
            e.Property(x => x.TauxTransport).HasPrecision(18, 4);
            e.Property(x => x.TauxLogement).HasPrecision(18, 4);
            e.Property(x => x.TauxAllocFamiliales).HasPrecision(18, 4);
            e.Property(x => x.TauxIndemniteKm).HasPrecision(18, 4);
            e.Property(x => x.TauxPrimeAssiduite).HasPrecision(18, 4);
            e.Property(x => x.TauxJourMaladie).HasPrecision(18, 4);
            e.Property(x => x.TauxJourFerie).HasPrecision(18, 4);
            e.Property(x => x.TauxComplementTransport).HasPrecision(18, 4);
        });

        modelBuilder.Entity<Pointage>(e =>
        {
            e.HasIndex(x => new { x.EmployeId, x.Horodatage, x.Source });
            e.HasOne(x => x.Employe).WithMany().HasForeignKey(x => x.EmployeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Ignore<LigneBulletinAPayer>();

        modelBuilder.Entity<BulletinPaie>(e =>
        {
            e.HasIndex(x => new { x.EmployeId, x.Annee, x.Mois }).IsUnique();
            e.Property(x => x.Numero).HasMaxLength(40);
            e.Property(x => x.SalaireMensuel).HasPrecision(18, 2);
            e.Property(x => x.SalaireJournalier).HasPrecision(18, 2);
            e.Property(x => x.RetenueRetards).HasPrecision(18, 2);
            e.Property(x => x.TotalAPayer).HasPrecision(18, 2);
            e.Property(x => x.NetAPayer).HasPrecision(18, 2);
            e.Property(x => x.DetailAPayerJson).HasMaxLength(8000);
            e.Ignore(x => x.LignesAPayer);
            e.Ignore(x => x.PeriodeLibelle);
            e.HasOne(x => x.Employe).WithMany().HasForeignKey(x => x.EmployeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParametresApplication>(e =>
        {
            e.HasData(new ParametresApplication { Id = ParametresApplication.SingletonId });
        });

        modelBuilder.Entity<Utilisateur>(e =>
        {
            e.HasIndex(x => x.Identifiant).IsUnique();
            e.Property(x => x.Identifiant).HasMaxLength(80).IsRequired();
            e.Property(x => x.NomComplet).HasMaxLength(160).IsRequired();
            e.Property(x => x.MotDePasseHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.MotDePasseSel).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasMaxLength(40);
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
        AjouterColonneSiAbsente(db, "Parametres", "CheminMelodyPaie", "TEXT NULL");
        AjouterColonneSiAbsente(db, "Employes", "SalaireMensuel", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Employes", "TauxSalaireBase", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Employes", "TauxAnciennete", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Employes", "TauxTransport", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Employes", "TauxLogement", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Employes", "TauxAllocFamiliales", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Employes", "TauxIndemniteKm", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Employes", "TauxPrimeAssiduite", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Employes", "TauxJourMaladie", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Employes", "TauxJourFerie", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Employes", "TauxComplementTransport", "REAL NOT NULL DEFAULT 0");
        // EnsureCreated ne crée Bulletins que sur DB neuve — table dédiée si absente
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Bulletins (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EmployeId INTEGER NOT NULL,
                Annee INTEGER NOT NULL,
                Mois INTEGER NOT NULL,
                Numero TEXT NOT NULL,
                DateGeneration TEXT NOT NULL,
                SalaireMensuel REAL NOT NULL,
                SalaireJournalier REAL NOT NULL,
                JoursPresents INTEGER NOT NULL,
                Absences INTEGER NOT NULL,
                NbRetards INTEGER NOT NULL,
                NbRetardsSanctionnes INTEGER NOT NULL,
                RetenueRetards REAL NOT NULL,
                TotalAPayer REAL NOT NULL DEFAULT 0,
                NetAPayer REAL NOT NULL,
                DetailAPayerJson TEXT NOT NULL DEFAULT '[]',
                FOREIGN KEY (EmployeId) REFERENCES Employes(Id) ON DELETE CASCADE
            );
            """);
        AjouterColonneSiAbsente(db, "Bulletins", "TotalAPayer", "REAL NOT NULL DEFAULT 0");
        AjouterColonneSiAbsente(db, "Bulletins", "DetailAPayerJson", "TEXT NOT NULL DEFAULT '[]'");
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Utilisateurs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Identifiant TEXT NOT NULL,
                NomComplet TEXT NOT NULL,
                MotDePasseHash TEXT NOT NULL,
                MotDePasseSel TEXT NOT NULL,
                Role TEXT NOT NULL DEFAULT 'Administrateur',
                DateCreation TEXT NOT NULL,
                DerniereConnexion TEXT NULL,
                Actif INTEGER NOT NULL DEFAULT 1
            );
            """);
        try
        {
            db.Database.ExecuteSqlRaw(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_Utilisateurs_Identifiant ON Utilisateurs(Identifiant);");
        }
        catch
        {
            // index déjà présent
        }
        try
        {
            db.Database.ExecuteSqlRaw(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_Bulletins_EmployeId_Annee_Mois ON Bulletins(EmployeId, Annee, Mois);");
        }
        catch
        {
            // index déjà présent
        }

        // Taux A PAYER absents après import salaire seul → dériver ÷ 26
        try
        {
            Services.CalculBulletinService.CompleterTauxManquants(db);
        }
        catch
        {
            // non bloquant au démarrage
        }

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
