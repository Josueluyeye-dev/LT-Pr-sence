using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MelodyPresence.Services;

/// <summary>Lecture des pointages via ZktecoPullWorker.exe (.NET Framework).</summary>
public static class ZktecoPointageReader
{
    private const string DossierWorkerRelatif = "ZktecoPullWorker";
    private const int TimeoutMs = 120_000;
    private const int TcpProbeMs = 12_000;

    public static IReadOnlyList<(string CodePin, DateTime Horodatage)> Lire(string ip, int port, int machineId,
        int commPassword = 0)
    {
        var exePath = ResoudreCheminExeWorker()
            ?? throw new FileNotFoundException(
                "ZktecoPullWorker.exe introuvable. Recompilez la solution LT Services Présence.");

        VerifierPortTcp(ip.Trim(), port);

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add(ip.Trim());
        psi.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(machineId.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(commPassword.ToString(CultureInfo.InvariantCulture));

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(TimeoutMs))
        {
            try { process.Kill(true); } catch { /* ignore */ }
            throw new TimeoutException("Lecture ZKTeco trop longue.");
        }

        if (process.ExitCode == 2)
            throw new InvalidOperationException("Arguments ZKTeco invalides.");

        if (process.ExitCode != 0)
        {
            var msg = string.IsNullOrWhiteSpace(stderr) ? "Échec lecture ZKTeco." : stderr.Trim();
            if (msg.StartsWith("CONN_FAIL", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Connexion SDK échouée vers {ip}:{port}. Vérifiez IP, port, n° machine et mot de passe PC.");
            throw new InvalidOperationException(msg);
        }

        stdout = stdout.Trim();
        if (string.IsNullOrEmpty(stdout) || stdout == "[]")
            return Array.Empty<(string, DateTime)>();

        var rows = JsonSerializer.Deserialize<List<ZkPullRowDto>>(stdout,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (rows == null || rows.Count == 0)
            return Array.Empty<(string, DateTime)>();

        var list = new List<(string, DateTime)>();
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.p) || string.IsNullOrWhiteSpace(r.t)) continue;
            if (!DateTime.TryParse(r.t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                continue;
            if (dt.Kind == DateTimeKind.Utc) dt = dt.ToLocalTime();
            else if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
            list.Add((r.p.Trim(), dt));
        }

        return list;
    }

    public sealed class ZkUserDto
    {
        public string Id { get; set; } = "";
        public string Nom { get; set; } = "";
        public int Privilege { get; set; }
        public bool Actif { get; set; }

        public string Affichage => string.IsNullOrWhiteSpace(Nom) ? Id : $"{Id} | {Nom}";
    }

    /// <summary>Liste des utilisateurs enrôlés sur le terminal (ID = CodePin / enroll number).</summary>
    public static IReadOnlyList<ZkUserDto> LireUtilisateurs(string ip, int port, int machineId, int commPassword = 0)
    {
        var exePath = ResoudreCheminExeWorker()
            ?? throw new FileNotFoundException(
                "ZktecoPullWorker.exe introuvable. Recompilez la solution LT Services Présence.");

        VerifierPortTcp(ip.Trim(), port);

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add("--users");
        psi.ArgumentList.Add(ip.Trim());
        psi.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(machineId.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(commPassword.ToString(CultureInfo.InvariantCulture));

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(TimeoutMs))
        {
            try { process.Kill(true); } catch { /* ignore */ }
            throw new TimeoutException("Lecture des utilisateurs ZKTeco trop longue.");
        }

        if (process.ExitCode != 0)
        {
            var msg = string.IsNullOrWhiteSpace(stderr) ? "Échec lecture utilisateurs terminal." : stderr.Trim();
            throw new InvalidOperationException(msg);
        }

        stdout = stdout.Trim();
        if (string.IsNullOrEmpty(stdout) || stdout == "[]")
            return Array.Empty<ZkUserDto>();

        var rows = JsonSerializer.Deserialize<List<ZkUserPullDto>>(stdout,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ZkUserPullDto>();
        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x.id))
            .Select(x => new ZkUserDto
            {
                Id = x.id!.Trim(),
                Nom = x.n?.Trim() ?? "",
                Privilege = x.p,
                Actif = x.e
            })
            .ToList();
    }

    private static string? ResoudreCheminExeWorker()
    {
        var nested = Path.Combine(AppContext.BaseDirectory, DossierWorkerRelatif, "ZktecoPullWorker.exe");
        if (File.Exists(nested)) return nested;
        var legacy = Path.Combine(AppContext.BaseDirectory, "ZktecoPullWorker.exe");
        return File.Exists(legacy) ? legacy : null;
    }

    private static void VerifierPortTcp(string ip, int port)
    {
        try
        {
            using var cts = new CancellationTokenSource(TcpProbeMs);
            using var client = new TcpClient();
            client.ConnectAsync(ip, port, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Aucune réponse TCP sur {ip}:{port}. Vérifiez le réseau et le port (souvent 4370).");
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException(
                $"Impossible d’ouvrir TCP vers {ip}:{port} ({ex.SocketErrorCode}).", ex);
        }
    }

    private sealed class ZkPullRowDto
    {
        public string? p { get; set; }
        public string? t { get; set; }
    }

    private sealed class ZkUserPullDto
    {
        public string? id { get; set; }
        public string? n { get; set; }
        public int p { get; set; }
        public bool e { get; set; }
    }
}
