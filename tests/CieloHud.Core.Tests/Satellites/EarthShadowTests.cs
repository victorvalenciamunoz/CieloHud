namespace CieloHud.Core.Tests.Satellites;

public class EarthShadowTests
{
    private const double R = EarthShadow.EarthRadiusKm;
    private static readonly EciPosition SunOnPlusX = new(149_600_000, 0, 0);

    [Fact]
    public void DaySide_IsSunlit()
    {
        Assert.False(EarthShadow.IsInUmbra(new EciPosition(R + 400, 0, 0), SunOnPlusX));
    }

    [Fact]
    public void DirectlyBehindEarth_IsInUmbra()
    {
        Assert.True(EarthShadow.IsInUmbra(new EciPosition(-(R + 400), 0, 0), SunOnPlusX));
    }

    [Fact]
    public void Terminator_IsSunlit()
    {
        // Perpendicular to the Sun direction, above the surface: sees the Sun grazing the limb.
        Assert.False(EarthShadow.IsInUmbra(new EciPosition(0, R + 400, 0), SunOnPlusX));
    }

    [Fact]
    public void NightSide_OutsideCylinder_IsSunlit()
    {
        Assert.False(EarthShadow.IsInUmbra(new EciPosition(-7000, R + 200, 0), SunOnPlusX));
    }

    [Fact]
    public void NightSide_WellInsideCylinder_IsInUmbra()
    {
        Assert.True(EarthShadow.IsInUmbra(new EciPosition(-7000, R - 500, 0), SunOnPlusX));
    }

    [Fact]
    public void Umbra_NarrowsWithDistance()
    {
        // 7000 km behind the Earth the umbra radius is about R - 7000·tan(0.264°) ≈ R - 32 km.
        // A point 20 km inside the cylinder is therefore outside the cone.
        Assert.False(EarthShadow.IsInUmbra(new EciPosition(-7000, R - 20, 0), SunOnPlusX));
        Assert.True(EarthShadow.IsInUmbra(new EciPosition(-7000, R - 45, 0), SunOnPlusX));
    }

    [Fact]
    public void SunDirection_IsNormalized()
    {
        var farSun = new EciPosition(0, 0, 1e9);
        var nearSun = new EciPosition(0, 0, 1e8);
        var behind = new EciPosition(0, 0, -(R + 400));

        Assert.True(EarthShadow.IsInUmbra(behind, farSun));
        Assert.True(EarthShadow.IsInUmbra(behind, nearSun));
    }

    [Fact]
    public void SunAtOrigin_Throws()
    {
        Assert.Throws<ArgumentException>(() => EarthShadow.IsInUmbra(new EciPosition(7000, 0, 0), new EciPosition(0, 0, 0)));
    }
}
