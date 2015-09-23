//using NLog;
using System;
using System.Text.RegularExpressions;

namespace backup {
	public class Filter {
		//private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		Regex[] _filters;

		public Filter() {
		}

		public override int GetHashCode() {
			int result = 0;
			foreach (Regex f in _filters) {
				result = result ^ f.GetHashCode();
			}
			return result;
		}

		/*public static implicit operator Filter(string str) {
			Filter result = new Filter();
			result.Set(str);
			return result;
		}

		public static implicit operator string(Filter filter) {
			string result = string.Empty;
			string separator = string.Empty;
			foreach (Regex filterR in filter._filters) {
				result += separator + filterR.ToString();
			}
			return result;
		}*/

		public void Set(string filters) {
			if (!string.IsNullOrEmpty(filters)) {
				string[] filtersStr = filters.Split('|');
				_filters = new Regex[filtersStr.Length];
				for (int i=0; i<_filters.Length; i++) {
					_filters[i] = new Regex(filtersStr[i], RegexOptions.IgnoreCase | RegexOptions.Compiled);
				}
				//_filters = filters.Split('|');
				/*foreach (string f in _filters) {
					LOGGER.Debug("Filter '" + f + "' will be used.");
				}*/
			}
		}

		public bool Exclude(string path, out string patternMatched) {
			if (_filters != null && _filters.Length > 0) {
				foreach (Regex filter in _filters) {
					//if (Regex.IsMatch(path, filter, RegexOptions.IgnoreCase | RegexOptions.Compiled)) {
					if (filter.IsMatch(path)) {
						patternMatched = filter.ToString();
						return true;
					}
				}
			}
			patternMatched = null;
			return false;
		}

	}
}

