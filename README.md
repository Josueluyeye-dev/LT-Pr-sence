# LT Services Présence

Logiciel simple de **gestion de présence** (employés, pointage manuel, pointeuse ZKTeco, rapports jour/mois, export PDF/Excel).

Produit **LT Services** — Présence. Indépendant de **Melody Paie RDC**. Les modules métier (congés, paie, etc.) seront ajoutés progressivement.

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
Pointeuse ZKTeco  --TCP/IP:4370-->  ZktecoPullWorker.exe  -->  LT Services Présence  -->  SQLite
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
  MelodyPresence/          # App WPF .NET 8 (namespace technique)
  ZktecoPullWorker/        # Lecteur terminal (.NET Framework)
  MelodyPresence.Tests/
  MelodyPresence.sln
```

## Licence / éditeur

**LT Services** — client  
Logiciel conçu par **[IMPACT Entreprises](https://impact-entreprises.net/)**

## Dépôt GitHub

Code source et releases : [Josueluyeye-dev/LT-Pr-sence](https://github.com/Josueluyeye-dev/LT-Pr-sence)

## Installateur

```powershell
.\installer\CreerInstallateur.ps1
```

Sortie : `installer\output\LT_Presence_Setup_*.exe`

## Publication d'une version

```powershell
.\installer\PublierRelease.ps1 -Version "1.0.1" -Notes "Description des changements"
```

GitHub Actions compile l'installateur, crée la Release et met à jour `installer/updates/version.json` (SHA256 + URL).

## Mise à jour (clients)

Dans **Paramètres**, bouton **Vérifier les mises à jour**.

Manifeste : `https://raw.githubusercontent.com/Josueluyeye-dev/LT-Pr-sence/main/installer/updates/version.json`

