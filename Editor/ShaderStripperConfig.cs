using UnityEngine;


namespace AimLab.ShaderStripper
{
    [CreateAssetMenu(menuName = "ShaderStripper/Shader Stripper Config")]
    public class ShaderStripperConfig : ScriptableObject
    {
        public bool Enable_Stripping;
        public bool Log_Kept;
        public bool Log_Stripped;
        public bool Deeplog_Kept;
        public bool Deeplog_Stripped;
    }
}
