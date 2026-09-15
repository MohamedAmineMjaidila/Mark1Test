using System.Runtime.InteropServices;
using UnityEngine;
namespace GrassFlow
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GrassLegacyBlade
    {
        public Vector3 position;
        public uint facing, heightWidth, windStiffness;
        public const int Stride = 24;
    }
}
