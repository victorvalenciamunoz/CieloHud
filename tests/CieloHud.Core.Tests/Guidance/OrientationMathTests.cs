using System.Numerics;

namespace CieloHud.Core.Tests.Guidance;

/// <summary>
/// Known poses built from axis-angle rotations in Android's frames (device X right, Y top, Z out of screen;
/// world X east, Y north, Z up). The quaternion maps device vectors to world vectors.
/// </summary>
public class OrientationMathTests
{
    private const float Deg = MathF.PI / 180;

    [Fact]
    public void FlatOnTable_ScreenUp_PointsStraightDown()
    {
        var pointing = OrientationMath.ToPointing(Quaternion.Identity);

        Assert.Equal(-90, pointing.AltitudeDegrees, precision: 5);
    }

    [Fact]
    public void Upright_ScreenFacingSouth_BackPointsNorthAtHorizon()
    {
        // Lift the top of the phone: rotate +90° about the device X axis.
        var pose = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 90 * Deg);

        var pointing = OrientationMath.ToPointing(pose);

        Assert.Equal(0, pointing.AzimuthDegrees, precision: 4);
        Assert.Equal(0, pointing.AltitudeDegrees, precision: 4);
        Assert.Equal(0, OrientationMath.RollDegrees(pose), precision: 4);
    }

    [Theory]
    [InlineData(-90, 90)]   // turn clockwise (seen from above) a quarter: now pointing east
    [InlineData(-180, 180)] // pointing south
    [InlineData(90, 270)]   // counter-clockwise: west
    [InlineData(-45, 45)]
    public void Upright_ThenYawed_AzimuthFollows(float yawAboutWorldUp, double expectedAzimuth)
    {
        var upright = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 90 * Deg);
        var yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yawAboutWorldUp * Deg);
        var pose = Quaternion.Concatenate(upright, yaw); // apply upright first, then yaw in world

        var pointing = OrientationMath.ToPointing(pose);

        Assert.Equal(expectedAzimuth, pointing.AzimuthDegrees, precision: 4);
        Assert.Equal(0, pointing.AltitudeDegrees, precision: 4);
    }

    [Theory]
    [InlineData(120, 30)]   // tilt back beyond upright: looking 30° above the horizon
    [InlineData(60, -30)]   // not quite upright: looking 30° below
    [InlineData(180, 90)]   // screen facing down: back points at the zenith
    public void TiltAboutDeviceX_SetsAltitude(float tiltDegrees, double expectedAltitude)
    {
        var pose = Quaternion.CreateFromAxisAngle(Vector3.UnitX, tiltDegrees * Deg);

        var pointing = OrientationMath.ToPointing(pose);

        Assert.Equal(expectedAltitude, pointing.AltitudeDegrees, precision: 4);
    }

    [Fact]
    public void Upright_ThenRolledClockwise_RollIsPositive_PointingUnchanged()
    {
        var upright = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 90 * Deg);
        // Roll around the pointing axis (device -Z => world north). Clockwise for the user looking at the screen
        // is a negative rotation about the device Z axis (which points towards the user).
        var roll = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -20 * Deg);
        var pose = Quaternion.Concatenate(roll, upright); // roll in device frame first, then stand upright

        Assert.Equal(20, OrientationMath.RollDegrees(pose), precision: 4);
        var pointing = OrientationMath.ToPointing(pose);
        Assert.Equal(0, pointing.AzimuthDegrees, precision: 4);
        Assert.Equal(0, pointing.AltitudeDegrees, precision: 4);
    }

    [Fact]
    public void UnnormalizedQuaternion_IsAccepted()
    {
        var pose = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 90 * Deg) * 3f;

        var pointing = OrientationMath.ToPointing(pose);

        Assert.Equal(0, pointing.AltitudeDegrees, precision: 4);
    }

    [Theory]
    [InlineData(1, 0, 0, 90, 0)]
    [InlineData(0, 1, 0, 0, 0)]
    [InlineData(-1, 0, 0, 270, 0)]
    [InlineData(0, 0, 1, 0, 90)]
    [InlineData(1, 1, 0, 45, 0)]
    [InlineData(0, 1, 1, 0, 45)]
    public void WorldVector_ToPointing(double east, double north, double up, double expectedAzimuth, double expectedAltitude)
    {
        var pointing = OrientationMath.ToPointing(east, north, up);

        Assert.Equal(expectedAzimuth, pointing.AzimuthDegrees, precision: 6);
        Assert.Equal(expectedAltitude, pointing.AltitudeDegrees, precision: 6);
    }

    [Fact]
    public void ZeroVector_Throws()
    {
        Assert.Throws<ArgumentException>(() => OrientationMath.ToPointing(0, 0, 0));
    }
}
