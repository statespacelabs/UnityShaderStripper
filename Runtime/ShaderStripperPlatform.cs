using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Rendering;
#endif

namespace Sigtrap.Editors.ShaderStripper {
    /// <summary>
	/// Strips shaders by shader compiler platform.
	/// </summary>
	[CreateAssetMenu(menuName="Sigtrap/Shader Stripper Platform")]
    public class ShaderStripperPlatform : ShaderStripperBase {
        [SerializeField][Tooltip("If checked, use as whitelist. Otherwise, blacklist.")]
        bool _whitelist;
        #if UNITY_EDITOR
        [SerializeField]
        List<ShaderCompilerPlatform> _platforms;
        #endif

        #if UNITY_EDITOR
        protected override bool _checkPass {get {return false;}}
        protected override bool _checkVariants {get {return true;}}
        protected override bool _checkShader {get {return false;}}

        protected override bool MatchVariant(ShaderCompilerData variantData){
            bool contains = _platforms.Contains(variantData.shaderCompilerPlatform);
            return _whitelist ? !contains : contains;
        }
        #endif
    }
}