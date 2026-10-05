# Melody Présence

Logiciel simple de **gestion de présence** (employés, pointage manuel, pointeuse ZKTeco, rapports jour/mois, export PDF/Excel).

Indépendant de **Melody Paie RDC**. Les modules métier (congés, paie, etc.) seront ajoutés progressivement.

## Prérequis

- Windows 10/11
- .NET 8 SDK
- .NET Framework 4.8 (pour `ZktecoPullWorker` / SDK ZKTeco)
- Pointeuse ZKTeco sur le même réseau local (optionnel en V1 manuelle)

## Lancer en développement

```powershell
cd c:\Users\luyey\Music\MelodyPresence
dotnet build MelodyPresence.sln -c Release
dotnet run --project MelodyPresence\MelodyPresence.csproj -c Release
```

Tests :

```powershell
dotnet test MelodyPresence.sln -c Release
```

## Fonctionnalités V1

| Module | Contenu |
|--------|---------|
| Employés | Matricule, nom, prénom, PIN pointeuse, actif/inactif |
| Présence | Entrée/sortie manuelle, grille du jour |
| ZKTeco | Sync TCP (port 4370), sync auto configurable |
| Rapports | Résumé mensuel (jours présents, heures, absences) |
| Exports | PDF et Excel (jour + mois) |

## Schéma de connexion pointeuse

```text
Pointeuse ZKTeco  --TCP/IP:4370-->  ZktecoPullWorker.exe  -->  MelodyPrésence  -->  SQLite
```

Paramètres : menu **Paramètres** (IP, port, n° machine, mot de passe communication PC).

Associez le **Code PIN pointeuse** de chaque employé au numéro enregistré sur le terminal.

## Données locales

Base SQLite :

`%LocalAppData%\MelodyPresence\presence.db`

## Roadmap modules

| Phase | Module |
|-------|--------|
| **V1** (actuelle) | Présence manuelle + ZKTeco + rapports + exports |
| **V1.1** | Retards / horaires de travail (plages) |
| **V1.2** | Congés / permissions |
| **V2** | Export / lien optionnel vers Melody Paie |
| **V2+** | Multi-sites / plusieurs pointeuses |
| Plus tard | Paie légère (si besoin métier) |

## Structure

```text
MelodyPresence/
  MelodyPresence/          # App WPF .NET 8
  ZktecoPullWorker/        # Lecteur terminal (.NET Framework)
  MelodyPresence.Tests/
  MelodyPresence.sln
```

## Licence / éditeur

Impact Entreprises — Melody Présence 1.0.0
