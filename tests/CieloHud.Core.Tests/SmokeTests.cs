using System.Reflection;

namespace CieloHud.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreAssemblyLoads()
    {
        var assembly = Assembly.Load("CieloHud.Core");

        Assert.Equal("CieloHud.Core", assembly.GetName().Name);
    }
}
