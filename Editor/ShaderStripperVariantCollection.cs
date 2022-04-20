using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Rendering;

namespace AimLab.ShaderStripper {
    /// <summary>
    /// Strips ALL shaders and variants except those in the supplied ShaderVariantCollection assets.
    /// Does not strip built-in shaders.
    /// </summary>
    [CreateAssetMenu(menuName="ShaderStripper/Shader Stripper Variant Collection")]
    public class ShaderStripperVariantCollection : ShaderStripperBase {

        [SerializeField][Tooltip("Only shader variants in these collections will NOT be stripped (except built-in shaders).")]
        List<ShaderVariantCollection> _whitelistedCollections;

        [SerializeField][Tooltip("Strip Hidden shaders. Be careful - shaders in Resources might get stripped.\nHidden shaders in collections will always have their variants stripped.")]
        bool _stripHidden = false;
		[SerializeField][Tooltip("Allow VR versions of variants in collection even when VR keywords not in collection.")]
		bool _allowVrVariants;
		[SerializeField][Tooltip("Allow GPU instanced versions of variants in collection even when instancing keywords not in collection.")]
		bool _allowInstancedVariants;

		[SerializeField][Tooltip("Shaders matching these names will be ignored (not stripped)")]
		StringMatch[] _ignoreShadersByName;
		[SerializeField][Tooltip("These passtypes will be ignored (not stripped)")]
		List<PassType> _ignorePassTypes;

		List<long> builtinShadersWithNoKeywords = new List<long>();
		List<string> customShadersWithNoKeywords = new List<string>();

		bool _valid = false;

		static readonly string[] VR_KEYWORDS = new string[]{
			"UNITY_SINGLE_PASS_STEREO", "STEREO_INSTANCING_ON", "STEREO_MULTIVIEW_ON"
		};
		static readonly string[] INSTANCING_KEYWORDS = new string[]{
			"INSTANCING_ON"
		};
		static List<string> _tempExcludes = new List<string>();
        Dictionary<Shader, Dictionary<PassType, List<ShaderVariantCollection.ShaderVariant>>> _variantsByShader = new Dictionary<Shader, Dictionary<PassType, List<ShaderVariantCollection.ShaderVariant>>>();

		
        #region Parse YAML - thanks Unity for not having a simple ShaderVariantCollection.GetVariants or something

        public override void Initialize(){

			builtinShadersWithNoKeywords.Clear();
			customShadersWithNoKeywords.Clear();

			//ReplaceOverwrittenCollections();

			_tempExcludes.Clear();
			if (_allowVrVariants){
				_tempExcludes.AddRange(VR_KEYWORDS);
			}
			if (_allowInstancedVariants){
				_tempExcludes.AddRange(INSTANCING_KEYWORDS);
			}

			_variantsByShader = ShaderStripperUtility.ParseShaderVariantCollections(_whitelistedCollections, _tempExcludes, builtinShadersWithNoKeywords, customShadersWithNoKeywords);

			if (ShaderStripperUtility.Config.Log_Stripped)
			{
				// Loop over shaders
				foreach (var s in _variantsByShader)
				{
					string log = "Shader: " + s.Key.name;
					// Loop over passes
					foreach (var p in s.Value)
					{
						log += string.Format("\n   Pass: ({1:00}){0}", p.Key, (int)p.Key);
						// Loop over variants
						for (int v = 0; v < p.Value.Count; ++v)
						{
							log += string.Format("\n      Variant [{0}]:\t", v);
							// Loop over keywords
							var ks = p.Value[v].keywords;
							if (ks != null && ks.Length != 0)
							{
								bool first = true;
								foreach (var k in ks)
								{
									if (!first) log += ", ";
									log += k;
									first = false;
								}
							}
							else
							{
								log += "<no keywords>";
							}
						}
					}
					LogMessage(this, log);
				}
			}
			_valid = (_variantsByShader != null && _variantsByShader.Count > 0);
		}

		#endregion

		static List<string> _tempRequestedKeywordsToMatch = new List<string>();
        static List<string> _tempRequestedKeywordsToMatchCached = new List<string>();
		static List<string> _tempCollectedKeywordsSorted = new List<string>();
		protected override bool StripCustom(Shader shader, ShaderSnippetData passData, IList<ShaderCompilerData> variantData) {
			// Don't strip anything if no collections present
			if (!_valid) return true;
			// Always ignore built-in shaders
			if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(shader))) return true;

			// Ignore shaders by name
			foreach (var s in _ignoreShadersByName) {
				if (s.Evaluate(shader.name)) return true;
			}

			// Ignore passes by type
			if (_ignorePassTypes.Contains(passData.passType)) return true;

			string guid;
			long fileID;
			AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out guid, out fileID);

			bool whitelistedWithNoKeywords = false;
			if (string.Equals(guid, "0000000000000000f000000000000000"))
			{
				if (builtinShadersWithNoKeywords.Contains(fileID))
				{
					whitelistedWithNoKeywords = true;
				}
			}
			else if (customShadersWithNoKeywords.Contains(guid))
			{
				whitelistedWithNoKeywords = true;
			}

			if (whitelistedWithNoKeywords)
			{
				return true;
			}

			// Try to match shader
			Dictionary<PassType, List<ShaderVariantCollection.ShaderVariant>> collectedVariantsByPass = null;
            if (_variantsByShader.TryGetValue(shader, out collectedVariantsByPass)){
                // Try to match pass
                List<ShaderVariantCollection.ShaderVariant> collectedPassVariants = null;
                if (collectedVariantsByPass.TryGetValue(passData.passType, out collectedPassVariants)){
                    // Loop over supplied variants
                    // Iterate backwards over supplied variants to allow index-based removal
                    int count = variantData.Count;
                    for (int i=count-1; i>=0; --i){

                        // Fill temp buffer to fill OTHER temp buffer each time SIGH
                        _tempRequestedKeywordsToMatchCached.Clear();
						var sks = variantData[i].shaderKeywordSet.GetShaderKeywords();
						bool variantMatched = false;
						
						foreach (var sk in sks){
							string n = GetKeywordName(sk);
							bool add = true;
							// Don't look for VR or instanced variants
							if (_tempExcludes.Count > 0){
								if (_tempExcludes.Contains(n)){
									add = false;
								}
							}
							if (add){
                            	_tempRequestedKeywordsToMatchCached.Add(n);
							}
						}
						
						

                        // Loop over cached variants
                        foreach (var collectedVariant in collectedPassVariants){
                            // Must match ALL keywords
                            _tempRequestedKeywordsToMatch.Clear();
                            _tempRequestedKeywordsToMatch.AddRange(_tempRequestedKeywordsToMatchCached);

                            // Early out (no match) if keyword counts don't match
                            if (_tempRequestedKeywordsToMatch.Count != collectedVariant.keywords.Length) continue;

                            // Early out (match) if both have no keywords
                            if (_tempRequestedKeywordsToMatch.Count == 0 && collectedVariant.keywords.Length == 0){
                                variantMatched = true;
                                break;
                            }

                            // Check all keywords
							_tempCollectedKeywordsSorted.Clear();
							_tempCollectedKeywordsSorted.AddRange(collectedVariant.keywords);
							_tempCollectedKeywordsSorted.Sort((a,b)=>{return string.CompareOrdinal(a,b);});
                            foreach (var k in _tempCollectedKeywordsSorted){
                                bool keywordMatched = _tempRequestedKeywordsToMatch.Remove(k);
                                if (!keywordMatched) break;
                            }
                            // If all keywords removed, all keywords matched
                            if (_tempRequestedKeywordsToMatch.Count == 0){
                                variantMatched = true;
								break;
                            }
                        }

                        // Strip this variant
                        if (!variantMatched){
                            LogRemoval(this, shader, passData, i, count, variantData[i]);
                            variantData.RemoveAt(i);
                        }
                    }
                } else {
                    // If not matched pass, clear all variants
                    LogRemoval(this, shader, passData);
                    variantData.Clear();
                }
            } else {
                // If not matched shader, clear all
                // Check if shader is hidden
                if (_stripHidden || !shader.name.StartsWith("Hidden/")){
                    LogRemoval(this, shader, passData);
                    variantData.Clear();
                }
            }

            return true;
        }
        
        public override string description {get {return "Strips ALL (non-built-in) shaders not in selected ShaderVariantCollection assets.";}}
        public override string help {
            get {
                string result = _stripHidden ? "WILL strip Hidden shaders." : "Will NOT strip Hidden shaders.";
                result += " Will NOT strip built-in shaders. Use other strippers to remove these.";
                return result;
            }
        }

        protected override bool _checkShader {get {return false;}}
        protected override bool _checkPass {get {return false;}}
        protected override bool _checkVariants {get {return false;}}

    }
}