using System.Text;
namespace GrassFlow.Editor
{
    internal static class GrassLegacyShaderSource
    {
        public static string Build(string hash, int lod)
        {
            var s = new StringBuilder();
            s.AppendLine($"Shader \"GrassFlow/Generated/{hash}/LOD{lod}\" {{ SubShader {{ Tags {{ \"RenderType\"=\"Opaque\" \"Queue\"=\"Geometry\" \"RenderPipeline\"=\"UniversalPipeline\" }}");
            Pass("ForwardLit", "UniversalForward", "GrassVertex", "GrassFragment", false, false);
            Pass("ShadowCaster", "ShadowCaster", "GrassShadowVertex", "GrassDepthFragment", true, true);
            Pass("DepthOnly", "DepthOnly", "GrassDepthVertex", "GrassDepthFragment", true, false);
            s.AppendLine("} }");
            return s.ToString();
            void Pass(string name, string lightMode, string vertex, string fragment, bool depth, bool shadow)
            {
                s.AppendLine($"Pass {{ Name \"{name}\" Tags {{ \"LightMode\"=\"{lightMode}\" }} Cull Off ZWrite On ZTest LEqual");
                if (depth) s.AppendLine("ColorMask 0");
                s.AppendLine($"HLSLPROGRAM\n#pragma target 4.5\n#pragma vertex {vertex}\n#pragma fragment {fragment}\n#pragma multi_compile_local _ GRASS_DIRECT_GRID");
                if (!depth) s.AppendLine("#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN\n#pragma multi_compile _ _SHADOWS_SOFT");
                if (shadow) s.AppendLine("#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW");
                s.AppendLine($"#define GRASS_LOD {lod}\n#define GRASS_TEST_LEGACYMODEL 1\n#define GRASS_TEST_LEGACYFIELD 1\n#define GRASS_TEST_LEGACYSELECTION 1");
                s.AppendLine("#include \"Assets/Grass/Assets/Shaders/Experimental/Includes/GrassLegacyProgram.hlsl\"\nENDHLSL\n}");
            }
        }
    }
}
