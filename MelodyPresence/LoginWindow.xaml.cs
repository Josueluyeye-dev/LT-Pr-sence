using System.Windows;
using System.Windows.Input;
using MelodyPresence.ViewModels;

namespace MelodyPresence;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Tentative();
                e.Handled = true;
            }
        };
    }

    private LoginViewModel Vm => (LoginViewModel)DataContext;

    private void BtnPrincipal_Click(object sender, RoutedEventArgs e) => Tentative();

    private void Tentative()
    {
        var ok = Vm.Valider(Pwd.Password, PwdConfirm.Password, out _);
        if (!ok)
            return;

        DialogResult = true;
        Close();
    }
}
