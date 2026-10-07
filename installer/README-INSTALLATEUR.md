# Installateur LT Services Présence

Livraison professionnelle pour **LT Services**, logiciel conçu par **[IMPACT Entreprises](https://impact-entreprises.net/)**.

## Créer l'installateur

```powershell
.\installer\CreerInstallateur.ps1
```

Sortie : `installer\output\LT_Presence_Setup_1.0.0.exe`

## Contenu

- Application WPF self-contained (.NET 8, win-x64)
- Worker ZKTeco (`ZktecoPullWorker`)
- Branding assistant : logos IMPACT Entreprises + LT Services
- Textes d'accueil, licence et fin d'installation
- Mot de passe technique fournisseur (défaut : `Impact2026`)

## Build interne sans mot de passe

```powershell
$env:SKIP_MDP_INSTALL = "1"
.\installer\CreerInstallateur.ps1
```

## Dépôt et mises à jour

- GitHub : https://github.com/Josueluyeye-dev/LT-Pr-sence
- Publication : `.\installer\PublierRelease.ps1 -Version "1.0.1" -Notes "..."`

## Contact éditeur

- Site : https://impact-entreprises.net/
- E-mail : contact@impact-entreprises.net
- Tél. : +243 898 923 309
- Adresse : 9, Avenue Père Boka, Kinshasa – Gombe
