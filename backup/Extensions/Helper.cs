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

		public static IEnumerable<T> Concat<T>(this IEnumerable<T> aItems, IEnumerable<T> bItems) {
			foreach (T item in aItems) {
				yield return item;
			}
			foreach (T item in bItems) {
				yield return item;
			}
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

		public static void CopyStream(this Stream input, Stream output, int bufferSize) {
			int num;
			byte[] buffer = new byte[bufferSize];
			while ((num = input.Read(buffer, 0, buffer.Length)) != 0) {
				output.Write(buffer, 0, num);
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

		//public static IEnumerable<string> SkipIfNoAccess(this IEnumerable<string> source) {
		//  var enumerator = source.GetEnumerator();
		//  bool finish = false;

		//  do {
		//    bool access;
		//    try {
		//      finish = !enumerator.MoveNext();
		//      access = true;
		//    } catch (UnauthorizedAccessException exc) {
		//      LOGGER.Error("Unable to access:" + exc.Message + ". Skipping...");
		//      access = false;
		//    } catch (IOException exc) {
		//      LOGGER.Error("Error on access:" + exc.Message + ". Skipping...");
		//      access = false;
		//    }

		//    if (!finish && access)
		//      yield return enumerator.Current;
				
		//  } while (!finish);
		//}

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

		//public static string CombinePath(string path1, string path2) {
		//  if (path1.Length == 0)
		//    return path2;
		//  if (path2.Length == 0)
		//    return path1;

		//  if (path1[path1.Length - 1] == Path.DirectorySeparatorChar) {
		//    return path1 + path2.TrimStart(Path.DirectorySeparatorChar);
		//  } else {
		//    if (path2[0] == Path.DirectorySeparatorChar) {
		//      return path1 + path2;
		//    } else {
		//      return path1 + Path.DirectorySeparatorChar + path2;
		//    }
		//  }
		//}

		public static T GetValue<T>(this System.Data.IDataReader rdr, int index) {
			var dbVal = rdr.GetValue(index);
			var csVal = (T)System.Convert.ChangeType(dbVal, typeof(T));
			return csVal;
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

