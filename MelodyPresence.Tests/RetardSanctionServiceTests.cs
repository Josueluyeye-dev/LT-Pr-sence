using MelodyPresence.Services;
using Xunit;

namespace MelodyPresence.Tests;

public class RetardSanctionServiceTests
{
    [Fact]
    public void Trois_retards_sans_retenue()
    {
        Assert.Equal(0, RetardSanctionService.NbRetardsSanctionnes(3));
        Assert.Equal(0m, RetardSanctionService.CalculerRetenue(2600m, 3));
    }

    [Fact]
    public void Quatrieme_retard_vaut_demi_journee()
    {
        // salaire 2600 / 26 = 100 / jour → demi = 50
        Assert.Equal(1, RetardSanctionService.NbRetardsSanctionnes(4));
        Assert.Equal(50m, RetardSanctionService.CalculerRetenue(2600m, 4));
    }

    [Fact]
    public void Six_retards_trois_sanctions()
    {
        Assert.Equal(3, RetardSanctionService.NbRetardsSanctionnes(6));
        Assert.Equal(150m, RetardSanctionService.CalculerRetenue(2600m, 6));
    }
}
