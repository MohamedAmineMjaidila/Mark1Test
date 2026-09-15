using UnityEngine;
namespace GrassFlow
{
    [CreateAssetMenu(menuName = "GrassFlow/Full Configuration")]
    public sealed class GrassConfiguration : ScriptableObject
    {
        [TextArea] public string description;
        public GrassExperimentProfile profileTemplate;
        public GrassFieldData field;
    }
}
