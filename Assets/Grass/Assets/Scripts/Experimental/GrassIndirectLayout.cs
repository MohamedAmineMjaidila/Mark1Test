using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GrassFlow
{
    /// <summary>Find the instance count in Unity's blittable argument struct without relying on field names or a fixed platform offset.</summary>
    public static class GrassIndirectLayout
    {
        public static readonly int InstanceCountOffset = FindInstanceCountOffset();
        private static int FindInstanceCountOffset()
        {
            int size = Marshal.SizeOf<GraphicsBuffer.IndirectDrawIndexedArgs>();
            if (size != GraphicsBuffer.IndirectDrawIndexedArgs.size)
                throw new NotSupportedException("Unity indirect argument managed and GPU sizes differ.");
            const uint marker = 0x67452301;
            var probe = new GraphicsBuffer.IndirectDrawIndexedArgs { instanceCount = marker };
            IntPtr memory = Marshal.AllocHGlobal(size);
            try
            {
                // instanceCount is an auto-property in Unity 6000.6; Marshal.OffsetOf(property name) throws.
                Marshal.StructureToPtr(probe, memory, false);
                for (int offset = 0; offset <= size - sizeof(uint); offset += sizeof(uint))
                    if (unchecked((uint)Marshal.ReadInt32(memory, offset)) == marker) return offset;
                throw new NotSupportedException("Cannot find the Unity indirect instance-count layout.");
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
    }
}
