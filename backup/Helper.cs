using NLog;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace backup {
	public static class Helper {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

		[System.Runtime.InteropServices.DllImport("symlink")]
		private static extern int is_link(string filename);

		public static bool PathIsLink(string filename) {
			return is_link(filename) == 1;
		}

		public static IEnumerable<string> EnumeratePath(string path) {
			if (!PathIsLink(path)) {
				int result = 0;
				foreach (string dir in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly).SkipIfNoAccess()) {
					foreach (string res in EnumeratePath(dir)) {
						yield return res;
					}
					result++;
				}
				foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).SkipIfNoAccess()) {
					yield return file;
					result++;
				}
				if (result == 0) {
					string dirRes = path[path.Length - 1] == Path.DirectorySeparatorChar ? path : path + Path.DirectorySeparatorChar;
					yield return dirRes;
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
				} catch(UnauthorizedAccessException exc) {
					LOGGER.Error("Unable to access:"+exc.Message+". Skipping...");
					access = false;
				} catch (IOException exc) {
					LOGGER.Error("Error on access:"+exc.Message+". Skipping...");
					access = false;
				}
				
				if (!finish && access)
					yield return enumerator.Current;
				
			} while (!finish);
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

