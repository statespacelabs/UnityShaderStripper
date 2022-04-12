using UnityEngine;
using UnityEditor;
namespace Sigtrap.Editors.ShaderStripper
{
	public class MaterialTool : EditorWindow
	{
		static MaterialTool _i;

		[MenuItem("Tools/Sigtrap/Material Tool")]
		public static void Launch()
		{
			if (_i == null)
			{
				_i = ScriptableObject.CreateInstance<MaterialTool>();
			}
			_i.Show();
		}

		void OnEnable()
		{
			titleContent = new GUIContent("Material Tool");
		}

		void OnGUI()
		{
			if (GUILayout.Button("Disable GPU Instancing on ALL materials"))
			{
				string[] guids = AssetDatabase.FindAssets("t:Material", null);
				foreach (string guid in guids)
				{
					var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
					mat.enableInstancing = false;
				}
			}
		}

	}
}
