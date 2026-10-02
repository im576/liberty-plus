using System;
using LibertyFramework.Core.Math3;

namespace LibertyFramework.Gunplay.Spread
{
    // Measures how far a real bullet trace deviated from the screen-centre aim ray.
    // The game fires from the muzzle toward the point under the crosshair and then offsets the
    // end point (CWeapon accuracy). We project the recorded end point onto the camera ray to
    // recover that aim point and report the angle between the two rays as seen from the muzzle.
    internal static class ShotGeometry
    {
        internal static double DeviationDegrees(Vec3 cameraPosition, Vec3 cameraForward, Vec3 bulletStart, Vec3 bulletEnd)
        {
            Vec3 forward = cameraForward.Normalized();
            double along = Vec3.Dot(bulletEnd - cameraPosition, forward);
            if (along <= 0.5) { return double.NaN; }
            Vec3 aimPoint = cameraPosition + forward * along;
            Vec3 intended = aimPoint - bulletStart;
            Vec3 actual = bulletEnd - bulletStart;
            double intendedLength = intended.Length;
            double actualLength = actual.Length;
            if (intendedLength < 0.5 || actualLength < 0.5) { return double.NaN; }
            double cosine = Vec3.Dot(intended, actual) / (intendedLength * actualLength);
            cosine = Math.Max(-1.0, Math.Min(1.0, cosine));
            return Math.Acos(cosine) * 180.0 / Math.PI;
        }

        internal static double DegreesToTangent(double degrees)
        {
            return Math.Tan(degrees * Math.PI / 180.0);
        }

        internal static double TangentToDegrees(double tangent)
        {
            return Math.Atan(tangent) * 180.0 / Math.PI;
        }
    }
}
