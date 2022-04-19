using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace AimLab.ShaderStripper
{
	public class ShaderVariantCollectionMergeTool : EditorWindow
	{
		
		[Tooltip("Only shader variants in these collections will NOT be stripped (except built-in shaders).")]
		[SerializeField]
		private List<ShaderVariantCollection> mergeCollections;
		[SerializeField]
		[Tooltip("Set a path like Assets/.../<name> (no extension) to merge whitelisted collections into a new collection asset.\nPath to a whitelisted collection (to overwrite) IS allowed.")]
		private string mergeToFile = null;

		private static ShaderVariantCollectionMergeTool _i;

		SerializedObject so;

		[MenuItem("Tools/ShaderStripper/Merge ShaderVariantCollection Tool")]
		public static void Launch()
		{
			if (_i == null)
			{
				_i = ScriptableObject.CreateInstance<ShaderVariantCollectionMergeTool>();
			}
			_i.Show();
		}

		void OnEnable()
		{
			titleContent = new GUIContent("Merge ShaderVariantCollection Tool");
			ScriptableObject target = this;
			so = new SerializedObject(target);
		}

		void OnGUI()
		{
			so.Update();

			SerializedProperty mergeToFileProperty = so.FindProperty("mergeToFile");

			EditorGUILayout.PropertyField(mergeToFileProperty, false);

			
			SerializedProperty mergeCollectionsProperty = so.FindProperty("mergeCollections");

			EditorGUILayout.PropertyField(mergeCollectionsProperty, true);


			so.ApplyModifiedProperties();

			if (GUILayout.Button("Merge"))
			{
				MergeShaderVariantCollections();
			}
		}


		void MergeShaderVariantCollections()
		{
			// Merge collections
			if (!string.IsNullOrEmpty(mergeToFile) && mergeCollections.Count > 1)
			{
				var _variantsByShader = ShaderStripperUtility.ParseShaderVariantCollections(mergeCollections);

				var svc = new ShaderVariantCollection();
				foreach (var a in _variantsByShader)
				{
					if (a.Value != null) 
					{ 
						foreach (var b in a.Value)
						{
							if (b.Value != null)
							{
								foreach (var s in b.Value)
								{
									svc.Add(s);
								}
							}
						}
					}
				}
				try
				{
					string file = mergeToFile + ".shadervariants";
					string log = string.Format("Merged following ShaderVariantCollections into {0}:\n", file);
					foreach (var s in mergeCollections)
					{
						log += "    " + s.name + "\n";
					}

					if (AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(file) != null)
					{
						AssetDatabase.DeleteAsset(file);
					}
					AssetDatabase.SaveAssets();
					AssetDatabase.Refresh();
					AssetDatabase.CreateAsset(svc, file);
					AssetDatabase.SaveAssets();
					AssetDatabase.Refresh();

					Debug.Log(log, AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(file));
				}
				catch (System.Exception ex)
				{
					Debug.LogError("Error merging ShaderVariantCollections. Exception follows: " + ex);
					throw;
				}
			}
		}

	}
}
