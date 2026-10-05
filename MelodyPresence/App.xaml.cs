using System.Windows;
using MelodyPresence.Data;

namespace MelodyPresence;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        PresenceDbContext.Initialiser();
    }
}
