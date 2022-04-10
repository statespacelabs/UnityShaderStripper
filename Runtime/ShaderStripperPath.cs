using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sigtrap.Editors.ShaderStripper {
    /// <summary>
	/// Strips shaders by shader asset path.
	/// </summary>
	[CreateAssetMenu(menuName="Sigtrap/Shader Stripper Path")]
    public class ShaderStripperPath : ShaderStripperBase {
        [SerializeField]
        StringMatch[] _pathBlacklist;

        #if UNITY_EDITOR
        protected override bool _checkPass {get {return false;}}
        protected override bool _checkVariants {get {return false;}}
        protected override bool _checkShader {get {return true;}}

        protected override bool MatchShader(Shader shader){
            string path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var p in _pathBlacklist){
                if (p.Evaluate(path)) return true;
            }
            return true;
        }
        #endif
    }
}