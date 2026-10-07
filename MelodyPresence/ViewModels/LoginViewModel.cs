using System.Windows.Input;
using MelodyPresence.Helpers;
using MelodyPresence.Services;

namespace MelodyPresence.ViewModels;

public class LoginViewModel : ObservableObject
{
    private bool _modeCreation;
    private string _identifiant = "";
    private string _nomComplet = "";
    private string _message = "";
    private bool _messageErreur;

    public LoginViewModel()
    {
        _modeCreation = !AuthService.ADesComptes();
        Message = _modeCreation
            ? "Créez votre compte pour démarrer LT Présence."
            : "Connectez-vous à votre compte.";
        BasculerModeCommand = new RelayCommand(_ => BasculerMode());
    }

    public ICommand BasculerModeCommand { get; }

    public bool ModeCreation
    {
        get => _modeCreation;
        set
        {
            if (!SetProperty(ref _modeCreation, value)) return;
            OnPropertyChanged(nameof(ModeConnexion));
            OnPropertyChanged(nameof(TitreEcran));
            OnPropertyChanged(nameof(SousTitreEcran));
            OnPropertyChanged(nameof(TexteBoutonPrincipal));
            OnPropertyChanged(nameof(TexteLienBasculer));
            Message = value
                ? "Créez votre compte personnel (identifiant + mot de passe)."
                : "Entrez vos identifiants pour accéder à l’application.";
            MessageErreur = false;
        }
    }

    public bool ModeConnexion => !ModeCreation;

    public string TitreEcran => ModeCreation ? "Créer mon compte" : "Connexion";
    public string SousTitreEcran => ModeCreation
        ? "Chaque utilisateur crée son propre accès sécurisé."
        : "LT Services — Présence";
    public string TexteBoutonPrincipal => ModeCreation ? "Créer le compte" : "Se connecter";
    public string TexteLienBasculer => ModeCreation
        ? "J’ai déjà un compte — Se connecter"
        : "Créer un nouveau compte";

    public string Identifiant
    {
        get => _identifiant;
        set => SetProperty(ref _identifiant, value);
    }

    public string NomComplet
    {
        get => _nomComplet;
        set => SetProperty(ref _nomComplet, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public bool MessageErreur
    {
        get => _messageErreur;
        set => SetProperty(ref _messageErreur, value);
    }

    private void BasculerMode()
    {
        // Interdit de basculer vers connexion s’il n’y a encore aucun compte
        if (ModeCreation && !AuthService.ADesComptes())
        {
            Message = "Créez d’abord votre compte pour continuer.";
            MessageErreur = true;
            return;
        }

        ModeCreation = !ModeCreation;
    }

    public bool Valider(string motDePasse, string confirmation, out string erreur)
    {
        erreur = "";
        if (ModeCreation)
        {
            var (ok, msg) = AuthService.CreerCompte(Identifiant, NomComplet, motDePasse, confirmation);
            Message = msg;
            MessageErreur = !ok;
            if (!ok)
            {
                erreur = msg;
                return false;
            }

            // Après création → connexion auto
            var login = AuthService.Connexion(Identifiant, motDePasse);
            Message = login.Message;
            MessageErreur = !login.Ok;
            erreur = login.Message;
            return login.Ok;
        }

        var res = AuthService.Connexion(Identifiant, motDePasse);
        Message = res.Message;
        MessageErreur = !res.Ok;
        erreur = res.Message;
        return res.Ok;
    }
}
