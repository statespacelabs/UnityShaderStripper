using System.Collections.Generic;
using System.Text;

namespace AimLab.ShaderStripper
{
	public static class YamlHelper
	{
		static List<string> _tempCompareShaderVariants = new List<string>();
		public static bool ShaderVariantsEqual(UnityEngine.ShaderVariantCollection.ShaderVariant a, UnityEngine.ShaderVariantCollection.ShaderVariant b)
		{
			if (a.shader != b.shader || a.passType != b.passType) return false;
			if ((a.keywords == null) != (b.keywords == null)) return false;
			///if (a.keywords == null || b.keywords == null) return false;
			if (a.keywords.Length != b.keywords.Length) return false;
			_tempCompareShaderVariants.Clear();
			_tempCompareShaderVariants.AddRange(a.keywords);
			for (int i = 0; i < b.keywords.Length; ++i)
			{
				if (!_tempCompareShaderVariants.Contains(b.keywords[i]))
				{
					return false;
				}
			}
			return true;
		}

		public static int GetYamlIndent(string line)
		{
			for (int i = 0; i < line.Length; ++i)
			{
				if (line[i] != ' ' && line[i] != '-') return i;
			}
			return 0;
		}
		public static bool IsYamlLineNewEntry(string line)
		{
			foreach (var c in line)
			{
				// If a dash (before a not-space appears) this is a new entry
				if (c == '-') return true;
				// If not a dash, must be a space or indent has ended
				if (c != ' ') return false;
			}
			return false;
		}

		public static int GetIndexOfYamlValue(string line, string key)
		{
			int i = line.IndexOf(key + ":", System.StringComparison.Ordinal);
			if (i >= 0)
			{
				// Skip to value
				i += key.Length + 2;
			}
			return i;
		}

		public static bool YamlLineHasKey(string line, string key)
		{
			return GetIndexOfYamlValue(line, key) >= 0;
		}

		public static string GetValueFromYaml(string line, string key)
		{
			int i = GetIndexOfYamlValue(line, key);
			if (i < 0)
			{
				return "";
				//throw new System.Exception((string.Format("Value not found for key {0} in YAML line {1}", key, line)));
			}
			StringBuilder sb = new StringBuilder();
			for (; i < line.Length; ++i)
			{
				char c = line[i];
				if (c == ',' || c == ' ') break;
				sb.Append(c);
			}
			return sb.ToString();
		}

		public static string[] GetValuesFromYaml(string line, string key, List<string> exclude = null)
		{
			int i = GetIndexOfYamlValue(line, key);
			if (i < 0)
			{
				throw new System.Exception((string.Format("Value not found for key {0} in YAML line {1}", key, line)));
			}
			List<string> result = new List<string>();
			StringBuilder sb = new StringBuilder();
			for (; i < line.Length; ++i)
			{
				char c = line[i];
				bool end = false;
				bool brk = false;
				if (c == ',')
				{
					// Comma delimits keys
					// Add the current entry and stop parsing
					end = brk = true;
				}
				if (c == ' ')
				{
					// Space delimits entries
					// Add current entry, move to next
					end = true;
				}
				if (end)
				{
					result.Add(sb.ToString());
					sb.Length = 0;
					if (brk) break;
				}
				else
				{
					sb.Append(c);
				}
			}
			// Catch last entry if line ends
			if (sb.Length > 0)
			{
				var s = sb.ToString();
				if (exclude == null || exclude.Count == 0 || !exclude.Contains(s))
				{
					result.Add(sb.ToString());
				}
			}
			return result.ToArray();
		}
	}
}
