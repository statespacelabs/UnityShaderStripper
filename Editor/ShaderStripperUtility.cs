using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine.Rendering;
using UnityEditor;
using System.Linq;
using Sigtrap.Editors.ShaderStripper;

public class ShaderStripperUtility : Object, IPreprocessShaders, IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
	public int callbackOrder { get { return 0; } }

	static System.Diagnostics.Stopwatch _swStrip = new System.Diagnostics.Stopwatch();
	static System.Diagnostics.Stopwatch _swBuild = new System.Diagnostics.Stopwatch();

	ShaderLog _keptLog = new ShaderLog("SHADERS-KEPT");
	ShaderLog _allKeywords = new ShaderLog("KEYWORDS");
	ShaderLog _keptKeywords = new ShaderLog("KEYWORDS-KEPT");
	ShaderLog _allPlatformKeywordNames = new ShaderLog("PLATFORM-KEYWORDS");
	ShaderLog _keptPlatformKeywordNames = new ShaderLog("PLATFORM-KEYWORDS-KEPT");
	List<BuiltinShaderDefine> _allPlatformKeywords = new List<BuiltinShaderDefine>();
	List<BuiltinShaderDefine> _keptPlatformKeywords = new List<BuiltinShaderDefine>();
	int _rawCount, _keptCount;

	private static bool _enabled = true;
	private static bool _deepLogs = true;


	public static List<ShaderStripperBase> _strippers = new List<ShaderStripperBase>();

	public static bool GetEnabled()
	{
		return true;
	}

	public static void RefreshSettings()
	{
		_strippers.Clear();

		foreach (var guid in AssetDatabase.FindAssets("t:ShaderStripperBase")){
			string path = AssetDatabase.GUIDToAssetPath(guid);
			_strippers.Add(AssetDatabase.LoadAssetAtPath<ShaderStripperBase>(path));
		}

		SortSettings();
	}

	public static void SortSettings()
	{
		_strippers = _strippers.OrderBy(x => new SerializedObject(x).FindProperty("_order").intValue).ToList();
		// Apply new sort orders
		for (int i = 0; i < _strippers.Count; ++i)
		{
			var so = new SerializedObject(_strippers[i]);
			so.FindProperty("_order").intValue = i;
			so.ApplyModifiedProperties();
		}
	}
	public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
	{
		_enabled = GetEnabled();

		if (_enabled)
		{
			Debug.Log("Initialising ShaderStrippers");

			_keptLog.Clear();
			_keptLog.Add("Unstripped Shaders:");
			RefreshSettings();
			ShaderStripperBase.OnPreBuild(_deepLogs);
			foreach (var s in _strippers)
			{
				if (s.active)
				{
					s.Initialize();
				}
			}
			_swStrip.Reset();
			_swBuild.Reset();
			_swBuild.Start();
		}
		else
		{
			Debug.Log("ShaderStripper DISABLED");
		}
	}

	public void OnPostprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
	{
		if (!_enabled) return;

		_swBuild.Stop();

		string header = string.Format(
			"Build Time: {0}ms\nStrip Time: {1}ms\nTotal shaders built: {2}\nTotal shaders stripped: {3}",
			_swBuild.ElapsedMilliseconds, _swStrip.ElapsedMilliseconds, _keptCount, _rawCount - _keptCount
		);
		Debug.Log(header);

		var strippedKeywords = new ShaderLog("KEYWORDS-STRIPPED");
		foreach (var k in _allKeywords.log)
		{
			if (!_keptKeywords.Contains(k))
			{
				strippedKeywords.Add(k);
			}
		}

		var strippedPlatformKeywords = new ShaderLog("PLATFORM-KEYWORDS-STRIPPED");
		foreach (var k in _allPlatformKeywordNames.log)
		{
			if (!_keptPlatformKeywordNames.Contains(k))
			{
				strippedPlatformKeywords.Add(k);
			}
		}

		ShaderStripperBase.OnPostBuild(
			header, _keptLog, _allKeywords, _keptKeywords,
			_allPlatformKeywordNames, _keptPlatformKeywordNames,
			strippedKeywords, strippedPlatformKeywords
		);

		_swStrip.Reset();
		_swBuild.Reset();
		_keptLog.Clear();
		_keptCount = 0;
		_allKeywords.Clear();
		_keptKeywords.Clear();
		_allPlatformKeywordNames.Clear();
		_allPlatformKeywords.Clear();
		_keptPlatformKeywordNames.Clear();
		_keptPlatformKeywords.Clear();
	}

	static readonly BuiltinShaderDefine[] _platformKeywords = (BuiltinShaderDefine[])System.Enum.GetValues(typeof(BuiltinShaderDefine));
	public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
	{
		if (!_enabled) return;
		_rawCount += data.Count;

		var builtins = (BuiltinShaderDefine[])System.Enum.GetValues(typeof(BuiltinShaderDefine));

		if (_deepLogs)
		{
			for (int i = 0; i < data.Count; ++i)
			{
				foreach (var k in data[i].shaderKeywordSet.GetShaderKeywords())
				{
					string sn = ShaderStripperBase.GetKeywordName(k);
					if (!_allKeywords.Contains(sn))
					{
						_allKeywords.Add(sn);
					}
				}
				var pks = data[i].platformKeywordSet;
				foreach (var b in builtins)
				{
					if (pks.IsEnabled(b))
					{
						if (!_allPlatformKeywords.Contains(b))
						{
							_allPlatformKeywords.Add(b);
							_allPlatformKeywordNames.Add(b.ToString());
						}
					}
				}
			}
		}

		_swStrip.Start();
		for (int i = 0; i < _strippers.Count; ++i)
		{
			var s = _strippers[i];
			if (!s.active) continue;
			s.Strip(shader, snippet, data);
			if (data.Count == 0) break;
		}
		_swStrip.Stop();
		if (data.Count > 0)
		{
			_keptCount += data.Count;
			_keptLog.Add(string.Format(
				"    {0}::[{1}]{2} [{3} variants]", shader.name,
				snippet.passType, snippet.passName, data.Count
			));

			if (_deepLogs)
			{
				foreach (var d in data)
				{
					string varLog = string.Format(
						"\t\t[{0}][{1}] ", d.graphicsTier, d.shaderCompilerPlatform
					);
					foreach (var k in d.shaderKeywordSet.GetShaderKeywords())
					{
						varLog += ShaderStripperBase.GetKeywordName(k) + " ";
					}

					varLog += "\n\t\t\t";
					foreach (var b in _platformKeywords)
					{
						if (d.platformKeywordSet.IsEnabled(b))
						{
							varLog += b.ToString() + " ";
						}
					}

					varLog += string.Format("\n\t\t\tREQ: {0}", d.shaderRequirements.ToString());
					_keptLog.Add(varLog);

					foreach (var k in d.shaderKeywordSet.GetShaderKeywords())
					{
						string sn = ShaderStripperBase.GetKeywordName(k);
						if (!_keptKeywords.Contains(sn))
						{
							_keptKeywords.Add(sn);
						}
					}

					var pks = d.platformKeywordSet;
					foreach (var b in builtins)
					{
						if (pks.IsEnabled(b))
						{
							if (!_keptPlatformKeywords.Contains(b))
							{
								_keptPlatformKeywords.Add(b);
								_keptPlatformKeywordNames.Add(b.ToString());
							}
						}
					}
				}
			}
		}
	}

	public static Dictionary<Shader, Dictionary<PassType, List<ShaderVariantCollection.ShaderVariant>>> ParseShaderVariantCollections(List<ShaderVariantCollection> _whitelistedCollections, List<string> _tempExcludes = null, List<long> builtinShadersWithNoKeywords = null, List<string> customShadersWithNoKeywords = null)
	{
		var _variantsByShader = new Dictionary<Shader, Dictionary<PassType, List<ShaderVariantCollection.ShaderVariant>>>();
		foreach (var c in _whitelistedCollections)
		{
			// Load asset YAML
			var file = new List<string>(System.IO.File.ReadAllLines(
				(Application.dataPath + AssetDatabase.GetAssetPath(c)).Replace("AssetsAssets", "Assets")
			));

			#region Pre-process to get rid of mid-list line breaks
			var yaml = new List<string>();

			// Find shaders list
			int i = 0;
			for (; i < file.Count; ++i)
			{
				if (YamlHelper.YamlLineHasKey(file[i], "m_Shaders")) break;
			}

			// Process and fill
			int indent = 0;
			for (; i < file.Count; ++i)
			{
				string f = file[i];
				int myIndent = YamlHelper.GetYamlIndent(f);
				if (myIndent > indent)
				{
					// If no "<key>: ", continuation of previous line
					if (!f.EndsWith(":") && !f.Contains(": "))
					{
						yaml[yaml.Count - 1] += " " + f.Trim();
						continue;
					}
				}

				yaml.Add(f);
				indent = myIndent;
			}
			#endregion

			#region Iterate over shaders
			var yamlCount = yaml.Count;
			for (i = 0; i < yamlCount; ++i)
			{
				string y = yaml[i];
				if (yaml[i].Contains("first:"))
				{
					string guid = YamlHelper.GetValueFromYaml(y, "guid");
					string fileID = YamlHelper.GetValueFromYaml(y, "fileID");
					Shader s = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
					if (s == null)
					{
						var fileIDint = int.Parse(fileID);
						if (fileIDint == 46)
						{
							s = Shader.Find("Standard");
						}
						else if (fileIDint == 45)
						{
							s = Shader.Find("Standard (Specular setup)");
						}
					}
					// Move to variants contents (skip current line, "second:" and "variants:")

					if (yaml[i + 2].Contains("[]"))
					{
						AddToShadersWithNoKeywords(guid, fileID, builtinShadersWithNoKeywords, customShadersWithNoKeywords);
						continue;
					}

					i += 3;

					if (i >= yamlCount)
					{
						break;
					}

					indent = YamlHelper.GetYamlIndent(yaml[i]);
					var sv = new ShaderVariantCollection.ShaderVariant();
					var variantNumber = 0;
					for (; i < yamlCount; ++i)
					{
						y = yaml[i];
						if (y == null) continue;

						// If indent changes, variants have ended
						if (YamlHelper.GetYamlIndent(y) != indent)
						{
							// Outer loop will increment, so counteract
							i -= 1;
							break;
						}

						if (YamlHelper.IsYamlLineNewEntry(y))
						{
							// First entry will be a new entry but no variant info present yet, so skip
							// Builtin shaders will also be null
							if (sv.shader != null)
							{
								UpdateVariantsList(_variantsByShader, sv);
							}

							sv.shader = s;
						}

						// Get contents
						if (YamlHelper.YamlLineHasKey(y, "passType"))
						{
							sv.passType = (PassType)int.Parse(YamlHelper.GetValueFromYaml(y, "passType"));
						}

						if (YamlHelper.YamlLineHasKey(y, "keywords"))
						{
							if (i < yamlCount - 2 && variantNumber == 0 && y.EndsWith(": "))
							{
								var banjo = yaml[i + 2];
								if (banjo != null && !banjo.Contains("keywords:")) 
								{ 
									AddToShadersWithNoKeywords(guid, fileID, builtinShadersWithNoKeywords, customShadersWithNoKeywords);
								}
							}

							sv.keywords = YamlHelper.GetValuesFromYaml(y, "keywords", _tempExcludes);

							variantNumber++;
						}
					}
					// Get final variant
					if (sv.shader != null)
					{
						UpdateVariantsList(_variantsByShader, sv);
					}
				}
			}
			#endregion
		}

		return _variantsByShader;
	}

	private static void UpdateVariantsList(Dictionary<Shader, Dictionary<PassType, List<ShaderVariantCollection.ShaderVariant>>> variantsByShader, ShaderVariantCollection.ShaderVariant sv)
    {
		Dictionary<PassType, List<ShaderVariantCollection.ShaderVariant>> variantsByPass = null;
		if (!variantsByShader.TryGetValue(sv.shader, out variantsByPass))
		{
			variantsByPass = new Dictionary<PassType, List<ShaderVariantCollection.ShaderVariant>>();
			variantsByShader.Add(sv.shader, variantsByPass);
		}
		List<ShaderVariantCollection.ShaderVariant> variants = null;
		if (!variantsByPass.TryGetValue(sv.passType, out variants))
		{
			variants = new List<ShaderVariantCollection.ShaderVariant>();
			variantsByPass.Add(sv.passType, variants);
		}
		bool dupe = false;
		foreach (var existing in variants)
		{
			if (YamlHelper.ShaderVariantsEqual(existing, sv))
			{
				dupe = true;
				break;
			}
		}
		if (!dupe)
		{ 
			Debug.Log("Shader = " + sv.shader.name + ", passType = " + sv.passType);
			variants.Add(sv);
		}
	}

	private static void AddToShadersWithNoKeywords(string guid, string fileID, List<long> builtinShadersWithNoKeywords, List<string> customShadersWithNoKeywords)
	{
		var fileIDlong = long.Parse(fileID);
		//is builtin
		if (string.Equals(guid, "0000000000000000f000000000000000"))
		{
			if(builtinShadersWithNoKeywords != null) builtinShadersWithNoKeywords.Add(fileIDlong);
		}
		//is custom
		else
		{
			if (customShadersWithNoKeywords != null) customShadersWithNoKeywords.Add(guid);
		}
	}
}
