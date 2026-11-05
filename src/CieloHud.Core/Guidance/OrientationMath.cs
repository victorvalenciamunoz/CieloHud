using System.Numerics;

namespace CieloHud.Core.Guidance;

/// <summary>
/// Turns the device rotation reported by Android's rotation-vector sensor into where the back of the phone points.
/// Conventions (Android): device axes X = right, Y = top of the screen, Z = out of the screen towards the user;
/// world axes X = east, Y = north, Z = up. The quaternion rotates device vectors into world vectors.
/// The back camera looks along device -Z; that is the direction we guide with.
/// </summary>
public static class OrientationMath
{
    private static readonly Vector3 BackOfDevice = new(0, 0, -1);
    private static readonly Vector3 TopOfDevice = new(0, 1, 0);

    /// <summary>Direction the back of the device points, from a device-to-world rotation quaternion.</summary>
    public static PointingDirection ToPointing(Quaternion deviceToWorld)
    {
        var world = Vector3.Transform(BackOfDevice, Quaternion.Normalize(deviceToWorld));
        return ToPointing(east: world.X, north: world.Y, up: world.Z);
    }

    /// <summary>Azimuth/altitude of a world-frame direction vector (east, north, up); need not be normalized.</summary>
    public static PointingDirection ToPointing(double east, double north, double up)
    {
        var length = Math.Sqrt(east * east + north * north + up * up);
        if (length == 0 || double.IsNaN(length))
            throw new ArgumentException("Direction vector must be non-zero.");

        var altitude = Math.Asin(Math.Clamp(up / length, -1, 1)) * 180 / Math.PI;
        // Straight up or down: azimuth is undefined; report north rather than noise.
        var azimuth = Math.Abs(east) < 1e-9 && Math.Abs(north) < 1e-9 ? 0 : Math.Atan2(east, north) * 180 / Math.PI;
        return new PointingDirection(azimuth, altitude);
    }

    /// <summary>
    /// Roll of the screen around the pointing axis, in degrees: 0 when the top of the device is "up" on the sky,
    /// positive when the device is rotated clockwise as seen by the user. Useful to keep HUD cues upright.
    /// </summary>
    public static double RollDegrees(Quaternion deviceToWorld)
    {
        var q = Quaternion.Normalize(deviceToWorld);
        var forward = Vector3.Transform(BackOfDevice, q);
        var top = Vector3.Transform(TopOfDevice, q);

        // Project world-up onto the plane perpendicular to the pointing direction; compare with the device top.
        var up = Vector3.UnitZ - forward * Vector3.Dot(Vector3.UnitZ, forward);
        if (up.LengthSquared() < 1e-9)
            return 0; // pointing straight up/down: roll is undefined
        up = Vector3.Normalize(up);
        var right = Vector3.Cross(forward, up);
        return Math.Atan2(Vector3.Dot(top, right), Vector3.Dot(top, up)) * 180 / Math.PI;
    }
}
