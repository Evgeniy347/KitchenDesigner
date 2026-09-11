using UnityEngine;

/// <summary>Управляемая замена Quaternion.Euler / Quaternion.AngleAxis.
/// Под CoreCLR эти вызовы падают SecurityException (ECall), поэтому
/// синус и косинус берём из System.Math.</summary>
public static class ManagedRotation
{
    public static Quaternion RotX(float degrees)
    {
        double half = degrees * System.Math.PI / 360.0;
        return new Quaternion((float)System.Math.Sin(half), 0f, 0f, (float)System.Math.Cos(half));
    }

    public static Quaternion RotY(float degrees)
    {
        double half = degrees * System.Math.PI / 360.0;
        return new Quaternion(0f, (float)System.Math.Sin(half), 0f, (float)System.Math.Cos(half));
    }

    public static Quaternion RotZ(float degrees)
    {
        double half = degrees * System.Math.PI / 360.0;
        return new Quaternion(0f, 0f, (float)System.Math.Sin(half), (float)System.Math.Cos(half));
    }

    /// <summary>Порядок Z→X→Y, как у Unity Quaternion.Euler.</summary>
    public static Quaternion Euler(float x, float y, float z)
        => RotY(y) * RotX(x) * RotZ(z);
}
