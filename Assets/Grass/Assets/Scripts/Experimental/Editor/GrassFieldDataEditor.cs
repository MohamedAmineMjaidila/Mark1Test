using UnityEditor;
using UnityEngine;

namespace GrassFlow.Editor
{
    [CustomEditor(typeof(GrassFieldData))]
    public sealed class GrassFieldDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var field = (GrassFieldData)target;
            EditorGUILayout.HelpBox("Placement and terrain heights are baked together. Paint or sculpt through the GrassRenderer, then Bake Field to update this asset.", MessageType.Info);
            using (new EditorGUI.DisabledScope(true)) DrawDefaultInspector();
            EditorGUILayout.LabelField("Occupancy", $"{field.OccupiedCount:N0} / {field.CellCount:N0} cells");
            EditorGUILayout.LabelField("Occupancy storage", $"{field.OccupancyBytes:N0} bytes");
            if (GUILayout.Button("Export occupancy PNG (one pixel per cell)"))
            {
                string path = EditorUtility.SaveFilePanel("Export baked occupancy", "", field.name + " occupancy", "png");
                if (!string.IsNullOrEmpty(path)) ExportOccupancy(field, path);
            }
            if (!field.IsValid(out string error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
        public static void ExportOccupancy(GrassFieldData field, string path)
        {
            var image = new Texture2D(field.Columns, field.Rows, TextureFormat.RGB24, false, true);
            try
            {
                var pixels = new Color32[field.CellCount];
                for (int row = 0; row < field.Rows; row++)
                    for (int col = 0; col < field.Columns; col++)
                        pixels[row * field.Columns + col] = field.IsOccupied(row, col) ? new Color32(255,255,255,255) : new Color32(0,0,0,255);
                image.SetPixels32(pixels); image.Apply();
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { DestroyImmediate(image); }
        }
    }
}
