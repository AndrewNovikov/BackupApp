using NLog;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace backup {
	public static class Helper {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

		//[System.Runtime.InteropServices.DllImport("symlink")]
		//private static extern int is_link(string filename);

		public static bool DirectoryIsLink(string fullPath) {
			return (IOHelper.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0;
		}

		public static string Join(this char[] characters, string separator) {
			string invalidStr = string.Empty;
			string intSeparator = string.Empty;
			foreach (char ch in characters) {
				invalidStr += intSeparator + ch;
				if (intSeparator == string.Empty) {
					intSeparator = separator;
				}
			}
			return invalidStr;
		}

		private static bool ExcludeByFilter(string path) {
			string patternMatched;
			bool result = MainClass.ExcludeFilter.Exclude(path, out patternMatched);
			if (result) {
				LOGGER.Trace("Path '" + path + "' will be ignored because of the exclusion '" + patternMatched + "'");
			}
			return result;
		}

		public static IEnumerable<string> EnumeratePath(string path) {
			if (!DirectoryIsLink(path) && !ExcludeByFilter(path)) {
				int result = 0;
				//foreach (string dir in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly).SkipIfNoAccess()) {
				//IEnumerable<string> dirs = SafeIOGetter(Directory.GetDirectories, path, "*", SearchOption.TopDirectoryOnly);
        IEnumerable<string> dirs = IOHelper.GetDirectories(path, "*", SearchOption.TopDirectoryOnly, ((exc) => {
          exc.WriteToLog("Unable to access: " + path + ". Skipping...: ");
        }));
				if (dirs != null) {
					foreach (string dir in dirs) {
						foreach (string res in EnumeratePath(dir)) {
							//if (!ExcludeByFilter(res)) { No need to check because it was done in parent call
							yield return res;
							//}
						}
						result++;
					}
				}
				//foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).SkipIfNoAccess()) {
				//IEnumerable<string> files = SafeIOGetter(Directory.GetFiles, path, "*", SearchOption.TopDirectoryOnly);
        IEnumerable<string> files = IOHelper.GetFiles(path, "*", SearchOption.TopDirectoryOnly, ((exc) => {
          exc.WriteToLog("Unable to access: " + path + ". Skipping...: ");
        }));
				if (files != null) {
					foreach (string file in files) {
						if (!ExcludeByFilter(file)) {
							yield return file;
							result++;
						}
					}
				}
				if (result == 0) {
					string dirRes = path[path.Length - 1] == Path.DirectorySeparatorChar ? path : path + Path.DirectorySeparatorChar;
					//if (!ExcludeByFilter(dirRes)) { No need to check because it was don in the head of this procedure
					yield return dirRes;
					//}
				}
			}
		}

		public static IEnumerable<string> SafeIOGetter(Func<string, string, SearchOption, string[]> getter, string path, string searchPattern, SearchOption option) {
			try {
				return getter(path, searchPattern, option);
			} catch (Exception exc) {
				if (exc is UnauthorizedAccessException || exc is IOException) {
					exc.WriteToLog("Unable to access: " + path + ". Skipping...: ");
					return null;
				} else {
					throw exc;
				}
			}
		}

		public static IEnumerable<string> SkipIfNoAccess(this IEnumerable<string> source) {
			var enumerator = source.GetEnumerator();
			bool finish = false;

			do {
				bool access;
				try {
					finish = !enumerator.MoveNext();
					access = true;
				} catch (UnauthorizedAccessException exc) {
					LOGGER.Error("Unable to access:" + exc.Message + ". Skipping...");
					access = false;
				} catch (IOException exc) {
					LOGGER.Error("Error on access:" + exc.Message + ". Skipping...");
					access = false;
				}

				if (!finish && access)
					yield return enumerator.Current;
				
			} while (!finish);
		}

		public static DateTime Truncate(this DateTime dateTime, TimeSpan timeSpan) {
			if (timeSpan == TimeSpan.Zero) return dateTime; // Or could throw an ArgumentException
			return dateTime.AddTicks(-(dateTime.Ticks % timeSpan.Ticks));
		}

		public static string DirectorySeparatorCharToLocal(string path) {
			switch (Path.DirectorySeparatorChar) {
			case '\\':
				return path.Replace('/', '\\');
			case '/':
				return path.Replace('\\', '/');
			default:
				throw new ApplicationException("Directory separator char '" + Path.DirectorySeparatorChar + "' is uknown.");
			}
		}

		public static string CombinePath(string path1, string path2) {
			if (path1.Length == 0)
				return path2;
			if (path2.Length == 0)
				return path1;

			if (path1[path1.Length - 1] == Path.DirectorySeparatorChar) {
				return path1 + path2.TrimStart(Path.DirectorySeparatorChar);
			} else {
				if (path2[0] == Path.DirectorySeparatorChar) {
					return path1 + path2;
				} else {
					return path1 + Path.DirectorySeparatorChar + path2;
				}
			}
		}

		public static void WriteToLog(this Exception exc, string prependText = "") {
			Exception current = exc;
			do {
				StringBuilder message = new StringBuilder();
				if (current == exc) {
					message.Append(prependText);
				}
				message.AppendLine(current.GetType().Name + " - " + current.Message);
				message.AppendLine(current.StackTrace);

				LOGGER.Error(message.ToString());

				current = current.InnerException;
			} while (current != null);
		}


	}
}

