using UnityEngine;
using UnityEditor;

namespace Sigtrap.Editors.ShaderStripper {
	public class ShaderStripperEditor : EditorWindow {

		[MenuItem("Tools/Sigtrap/Shader Stripper")]

		public static void Launch() {
			if (_i == null) {
				_i = ScriptableObject.CreateInstance<ShaderStripperEditor>();
			}
			_i.Show();
		}

		static ShaderStripperEditor _i;

		static ShaderStripperUtility _u;

		Vector2 _scroll;

		#region GUI


		void OnEnable(){
			titleContent = new GUIContent("Shader Stripper");

			if (_u == null) _u = new ShaderStripperUtility();

			ShaderStripperUtility.RefreshSettings();
			ShaderStripperUtility._logPath = EditorPrefs.GetString(ShaderStripperUtility.KEY_LOG);
			ShaderStripperUtility._enabled = ShaderStripperUtility.GetEnabled();
			ShaderStripperUtility._deepLogs = EditorPrefs.GetBool(ShaderStripperUtility.KEY_DEEP_LOG);
		}
		void OnGUI(){
			Color gbc = GUI.backgroundColor;

			EditorGUILayout.Space();
			if (!ShaderStripperUtility._enabled)
			{
				GUI.backgroundColor = Color.magenta;
			}
			EditorGUILayout.BeginVertical(EditorStyles.helpBox); {
				GUI.backgroundColor = gbc;

				// Title
				EditorGUILayout.BeginHorizontal(); {
					EditorGUILayout.LabelField(new GUIContent("Shader Stripping","Any checked settings are applied at build time."), EditorStyles.largeLabel, GUILayout.Height(25));
					GUILayout.FlexibleSpace();
					
					GUI.backgroundColor = Color.blue;
					if (GUILayout.Button("Refresh Settings", GUILayout.Width(125))){
						ShaderStripperUtility.RefreshSettings();
					}
					GUI.backgroundColor = gbc;
				} EditorGUILayout.EndHorizontal();

				// Toggle stripping
				EditorGUI.BeginChangeCheck(); {
					ShaderStripperUtility._enabled = EditorGUILayout.ToggleLeft("Enable Stripping", ShaderStripperUtility._enabled);
				} if (EditorGUI.EndChangeCheck()){
					EditorPrefs.SetBool(ShaderStripperUtility.KEY_ENABLE, ShaderStripperUtility._enabled);
					Repaint();
				}

				// Log folder
				EditorGUILayout.Space();
				EditorGUI.BeginChangeCheck(); {
					EditorGUILayout.BeginHorizontal(); {
						ShaderStripperUtility._logPath = EditorGUILayout.TextField("Log output file folder", ShaderStripperUtility._logPath);
						if (GUILayout.Button("...", GUILayout.Width(25))){
							string path = EditorUtility.OpenFolderPanel("Select log output folder", ShaderStripperUtility._logPath, "");
							if (!string.IsNullOrEmpty(path)){
								ShaderStripperUtility._logPath = path;
							}
						}
					} EditorGUILayout.EndHorizontal();
					ShaderStripperUtility._deepLogs = EditorGUILayout.ToggleLeft("Deep logs", ShaderStripperUtility._deepLogs);
				} if (EditorGUI.EndChangeCheck()){
					EditorPrefs.SetString(ShaderStripperUtility.KEY_LOG, ShaderStripperUtility._logPath);
					EditorPrefs.SetBool(ShaderStripperUtility.KEY_DEEP_LOG, ShaderStripperUtility._deepLogs);
					Repaint();
				}
				
				// Strippers
				EditorGUILayout.Space();
				bool reSort = false;
				_scroll = EditorGUILayout.BeginScrollView(_scroll, EditorStyles.helpBox); {
					for (int i=0; i< ShaderStripperUtility._strippers.Count; ++i){
						var s = ShaderStripperUtility._strippers[i];
						if (s == null){
							ShaderStripperUtility.RefreshSettings();
							break;
						}
						var so = new SerializedObject(s);
						var active = so.FindProperty("_active");
						GUI.backgroundColor = Color.Lerp(Color.grey, Color.red, active.boolValue ? 0 : 1);
						EditorGUILayout.BeginVertical(EditorStyles.helpBox); {
							GUI.backgroundColor = gbc;
							var expanded = so.FindProperty("_expanded");
							EditorGUILayout.BeginHorizontal(); {
								// Info
								EditorGUILayout.BeginHorizontal(); {
									active.boolValue = EditorGUILayout.Toggle(active.boolValue, GUILayout.Width(25));
									expanded.boolValue = EditorGUILayout.Foldout(expanded.boolValue, s.name + (active.boolValue ? "" : " (inactive)"));
									GUILayout.FlexibleSpace();
									GUILayout.Label(new GUIContent(s.description, "Class: "+s.GetType().Name));

									// Buttons
									GUILayout.FlexibleSpace();
									GUI.enabled = i > 0;
									if (GUILayout.Button("UP")){
										--so.FindProperty("_order").intValue;
										var soPrev = new SerializedObject(ShaderStripperUtility._strippers[i-1]);
										++soPrev.FindProperty("_order").intValue;
										soPrev.ApplyModifiedProperties();
										reSort = true;
									}
									GUI.enabled = i < (ShaderStripperUtility._strippers.Count-1);
									if (GUILayout.Button("DOWN")){
										++so.FindProperty("_order").intValue;
										var soNext = new SerializedObject(ShaderStripperUtility._strippers[i+1]);
										--soNext.FindProperty("_order").intValue;
										soNext.ApplyModifiedProperties();
										reSort = true;
									}
									GUI.enabled = true;
									if (GUILayout.Button("Select")){
										EditorGUIUtility.PingObject(s);
									}
								} EditorGUILayout.EndHorizontal();
							} EditorGUILayout.EndHorizontal();
							if (expanded.boolValue){
								string help = s.help;
								if (!string.IsNullOrEmpty(help)){
									EditorGUILayout.HelpBox(help, MessageType.Info);
								}
								// Settings
								var sp = so.GetIterator();
								sp.NextVisible(true);
								while (sp.NextVisible(false)){
									if ((sp.name == "_active") || (sp.name == "_expanded")) continue;
									EditorGUILayout.PropertyField(sp, true);
								}
							}

							s.OnGUI();
						} EditorGUILayout.EndVertical();
						EditorGUILayout.Space();

						so.ApplyModifiedProperties();
					}
				} EditorGUILayout.EndScrollView();
				
				if (reSort){
					ShaderStripperUtility.SortSettings();
				}
			} EditorGUILayout.EndVertical();
			GUI.backgroundColor = gbc;
		}

		#endregion
	}
}