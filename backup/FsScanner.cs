using NLog;
using NLog.Targets;
using System;
using System.IO;
using System.Collections.Generic;

namespace backup {
	public class FsScanner: IDisposable {
		static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		int _scanId = -1;
		string _server;

		public int ScanId {
			get {
				return _scanId;
			}
		}

		public FsScanner(string server) {
			_server = server;

			_scanId = DAL.Instance.CreateNewScan(_server);
			ChageLog(_scanId);
		}

		public IEnumerable<FsInfo> GetItems(string folder) {
			//foreach (string folder in rootCollection) {
			foreach (string path in EnumeratePath(folder)) {	
				//FileAttributes attr = IOHelper.GetAttributes(path);
				FsInfo result;
				try {
					result = new FsInfo(_server, folder, path);
					MainClass.Stat.IncrementScannedFilesCount();
					MainClass.Stat.IncrementScannedFilesLength(result.Length);

					//if ((attr & FileAttributes.Directory) == FileAttributes.Directory) {
					//  result = new DirectoryInfo(path);
					//} else {
					//  result = new FileInfo(path);
					//}
				} catch (Exception exc) {
					if (exc is UnauthorizedAccessException || exc is IOException) {
						exc.WriteToLog("Unable to access: " + path + ": ");
						result = null;
					} else {
						throw exc;
					}
				}
				if (result != null) {
					yield return result;
				}
			}
			//}
		}

		private static IEnumerable<string> EnumeratePath(string path) {
			if (!DirectoryIsLink(path) && !ExcludeByFilter(path)) {
				int result = 0;
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
					//string dirRes = path[path.Length - 1] == Path.DirectorySeparatorChar ? path : path + Path.DirectorySeparatorChar;
					//if (!ExcludeByFilter(dirRes)) { No need to check because it was done in the head of this procedure
					//yield return dirRes;
					yield return path;
					//}
				}
			}
		}

		private static bool ExcludeByFilter(string path) {
			string patternMatched;
			bool result = MainClass.Settings.Filters.Exclude(path, out patternMatched);
			if (result) {
				LOGGER.Trace("Path '" + path + "' will be ignored because of the exclusion '" + patternMatched + "'");
			}
			return result;
		}

		private static bool DirectoryIsLink(string fullPath) {
			return (IOHelper.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0;
		}

		/*public IEnumerable<BackupItem> GetItems(RootPathCollection rootCollection) {
			foreach (string folder in rootCollection) {

				foreach (string path in Helper.EnumeratePath(folder)) {	
					BackupItem item = null;
					try {

						if (path[path.Length - 1] == Path.DirectorySeparatorChar) {
							item = new BackupFolder(_server, path);
						} else {
							item = new BackupFile(_server, path);
							MainClass.Stat.IncrementScannedFilesCount();
							MainClass.Stat.IncrementScannedFilesLength(item.Length);
						}


					} catch (Exception exc) {
						if (exc is UnauthorizedAccessException || exc is IOException) {
							exc.WriteToLog("Unable to access: " + path + ": ");
						} else {
							throw exc;
						}
					}

					if (item != null) {
						yield return item;
					}
				}

			}
		}*/

		public void Dispose() {
			if (_scanId != -1) {
				try {
					DAL.Instance.EndScan(_scanId);
				} catch (Exception exc) {
					exc.WriteToLog("Exception on EndScan: ");
				}
			}
			WriteScanReport();
		}

		private static void ChageLog(int scanId) {
			foreach (FileTarget target in LogManager.Configuration.AllTargets) {
				string fileName = target.FileName.ToString().Trim(new char[] {'\''});
				int indexOfStart = fileName.Length - 1;
				char indexChar = fileName[indexOfStart];
				while (indexOfStart > -1 && indexChar != Path.DirectorySeparatorChar && indexChar != '}') {
					indexChar = fileName[--indexOfStart];
				}
				if (indexOfStart > -1) {
					target.FileName = fileName.Insert(indexOfStart + 1, scanId + ".");
				} else {
					target.FileName = scanId + "." + fileName;
				}
			}
		}

		private static void WriteScanReport() {
			string time = MainClass.Stat.ElapsedTimeString;
			LOGGER.Info(string.IsNullOrEmpty(time) ? "Done instantly" : "Done in " + time);

			LOGGER.Info("Total scanned files: " + MainClass.Stat.ScannedFilesCount);
			LOGGER.Info("Total send files: " + MainClass.Stat.SendFilesCount);
			LOGGER.Info("Total scanned files size: " + MainClass.Stat.ScannedFilesLengthString);
			LOGGER.Info("Total hashed files: " + MainClass.Stat.TotalFilesHashed);
      LOGGER.Info("Average hashing speed: " + (MainClass.Stat.TotalFilesHashed == 0 ? 0 : MainClass.Stat.TotalMillisecondsOnHashing / MainClass.Stat.TotalFilesHashed) + "ms/file");
			LOGGER.Info("Total unencrypted files size to remote server could be send: " + MainClass.Stat.SendUnencryptedFilesLengthString);
			LOGGER.Info("Total encrypted files size to remote server were send: " + MainClass.Stat.SendEncryptedFilesLengthString);
		}

	}
}

