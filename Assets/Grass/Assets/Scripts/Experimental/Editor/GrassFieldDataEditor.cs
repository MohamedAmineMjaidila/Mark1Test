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
            if (!field.IsValid(out string error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
    }
}
