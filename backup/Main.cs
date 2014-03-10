using NLog;
using NLog.Targets;
using System;
using System.Collections.Generic;
//using System.Collections.Concurrent;
//using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Linq;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
//using log4net;
//using log4net.Config;
using System.Net.FtpClient;
using System.Threading;

namespace backup {
	class MainClass {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		private static readonly Logger NEW_FILES_LOGGER = LogManager.GetLogger("newfiles");
		private static Mutex mutex;


		/*private static DateTime startTime;
		private static long totalFilesLength;
		private static long totalFilesCount;*/
		//private static readonly object _totalFilesLengthLock = new object();

		public static string Password;
		public static string BackupStore;
		public static string FtpUser;
		public static string FtpPassword;
		public static string FtpMode;
		public static uint EncryptionThreadsCount;
		public static bool OnlyOneInstanceOnServer;
		public static string DbConnectionString;
		public static readonly Filter ExcludeFilter = new Filter();
		public static readonly Report Stat = new Report();

		public static void Main(string[] args) {
			try {
				string[] roots = LoadSettings();
				string server = System.Environment.MachineName;

				if (OnlyOneInstanceOnServer) {
					mutex = new Mutex(false, "vgfBackupApp");
					if (!mutex.WaitOne(TimeSpan.FromSeconds(2), false)) {
						LOGGER.Error("Application already started!");
						return;
					}
				}

				if (args.Length > 0) {
					int indexOfCleanOrphanPathsArg = Array.IndexOf(args, "-cleanOrphans");
					if (indexOfCleanOrphanPathsArg != -1) {
						LOGGER.Info("Clean orphans started");
						int orphanLimit;
						if (int.TryParse(args[indexOfCleanOrphanPathsArg + 1], out orphanLimit)) {
							uint cleanCount;
							if (uint.TryParse(args[indexOfCleanOrphanPathsArg + 2], out cleanCount)) {
								Clean(orphanLimit, cleanCount);
							} else {
								Clean(orphanLimit, 1);
							}
						} else {
							Clean(0, 1);
						}
						LOGGER.Info("Clean orphans finished");
					}
					int indexOfRestore = Array.LastIndexOf(args, "-restore");
					if (indexOfRestore != -1) {
						int scanId;
						if (int.TryParse(args[indexOfRestore + 1], out scanId)) {
							if (args.Length < indexOfRestore + 3) {
								throw new ApplicationException("'backup.exe -restore scanId targetPath <sourcePath>' should be the syntax");
							}
							string targetPath = args[indexOfRestore + 2];
							string sourcePath = args.Length > indexOfRestore + 3 ? args[indexOfRestore + 3] : null;
							LOGGER.Info("Restoring...");
							Restore(scanId, targetPath, sourcePath);
						} else {
							throw new ApplicationException("'backup.exe -restore scanId targetPath <sourcePath>' should be the syntax");
						}
					}
				} else {
					Scan(server, roots);
				}

			} catch (Exception exc) {
				exc.WriteToLog("main exception: ");
			} finally {

				if (mutex != null) {
					mutex.ReleaseMutex();
				}

				//At the end of program execution (after the Main() has finished) the program locks up waiting for NLog logs to be flushed, so mono process never completes and needs to be killed
				//http://nlog-project.org/2011/10/30/using-nlog-with-mono.html
				LogManager.Configuration = null;
			}
		}

		/*private static long Clean(int limit) {
			long result = 0;
			long prevDataId = -1;
			int prevDataIdCount = 0;
			string prevMd5 = null;
			FtpController ftp = new FtpController();

			foreach (PathToClean row in DAL.Instance.GetOrphanPaths(limit)) {

				DAL.Instance.DeletePathRow(row.PathId);
				result++;
				LOGGER.Debug("PathId " + row.PathId + " row deleted");

				if (prevDataId != row.DataId) {
					if (prevDataIdCount == 1) {
						ftp.DeleteFile(prevMd5);
						DAL.Instance.DeleteDataRow(prevDataId);
						LOGGER.Debug("DataId " + row.DataId + " row deleted");
					} else {
						prevDataIdCount = 1;
					}
				} else {
					prevDataIdCount++;
				}

				prevDataId = row.DataId;
				prevMd5 = row.Md5;
			}

			return result;
		}*/

		private static void Restore(int scanId, string targetPath, string sourcePath) {
			Directory.CreateDirectory(targetPath);

			IDecryptor decr = new FtpDecryptor();
			foreach (RecoverInfo rec in DAL.Instance.GetAllPathsStartedWith(scanId, sourcePath)) {
				rec.Decrypt(decr, targetPath);
			}
		}

		private static void Clean(int limit, uint cleanCount) {
			long cleaned;
			uint cleanPathsCount = cleanCount;
			while (cleanPathsCount != 0 && (cleaned = CleanPaths(limit)) > 0) {
				cleanPathsCount--;
				LOGGER.Info(cleaned + " cleaned orphan paths");
			}

			uint cleanDataCount = cleanCount;
			while (cleanDataCount != 0 && (cleaned = CleanDatas(limit)) > 0) {
				cleanDataCount--;
				LOGGER.Info(cleaned + " cleaned orphan datas");
			}
		}

		private static long CleanPaths(int limit) {
			long result = 0;

			foreach (long pathId in DAL.Instance.GetOrphanPathId(limit)) {
				DAL.Instance.DeletePathRow(pathId);
				result++;
				LOGGER.Debug("PathId " + pathId + " row deleted");
			}
			return result;
		}

		private static long CleanDatas(int limit) {
			long result = 0;
			FtpController ftp = new FtpController();
			
			foreach (KeyValuePair<long,string> dataIdMd5 in DAL.Instance.GetOrphanDataId(limit)) {
				if (dataIdMd5.Value != Crypto.EmptyMd5Str) {

					try {
						ftp.DeleteFile(dataIdMd5.Value);
					} catch (FtpCommandException exc) {
						LOGGER.Error("File " + dataIdMd5.Value + " exception: " + exc.Message);
					}
					/*if (ftp.FileExists(dataIdMd5.Value)) {
						ftp.DeleteFile(dataIdMd5.Value);
						LOGGER.Debug("File " + dataIdMd5.Value + " deleted");
					} else {
						LOGGER.Error("File " + dataIdMd5.Value + " does not exists on target ftp server");
					}*/

				}

				DAL.Instance.DeleteDataRow(dataIdMd5.Key);
				LOGGER.Debug("DataId " + dataIdMd5.Key + " row deleted");

				result++;
			}
			return result;
		}

		private static void Scan(string server, IEnumerable<string> roots) {
			int scanId = -1;

			try {
				scanId = DAL.Instance.CreateNewScan(server);
				ChageLog(scanId);
			
				using (BackupFileQueue encryptionQueue = new BackupFileQueue(EncryptionThreadsCount)) {
					foreach (string root in roots) {
						ScanFolder(scanId, server, root, encryptionQueue);
					}
					LOGGER.Info("Scan complete. Waiting for the working threads...");
				}
			} finally {
				if (scanId != -1) {
					try {
						DAL.Instance.EndScan(scanId);
					} catch (Exception exc) {
						exc.WriteToLog("main exception on Scan finally: ");
					}
				}
				WriteScanReport();
			}
		}

		private static string[] LoadSettings() {
			try {
				DbConnectionString = ConfigurationManager.AppSettings.Get("DbConnectionString");
				string[] roots = ConfigurationManager.AppSettings.Get("rootPaths").Split('|');
				Password = ConfigurationManager.AppSettings.Get("password");
				if (string.IsNullOrEmpty(Password))
					throw new ApplicationException("Password must be set");
				BackupStore = ConfigurationManager.AppSettings.Get("targetFtp");
				FtpUser = ConfigurationManager.AppSettings.Get("ftpUser");
				FtpPassword = ConfigurationManager.AppSettings.Get("ftpPassword");
				FtpMode = ConfigurationManager.AppSettings.Get("ftpMode");
				ExcludeFilter.Set(ConfigurationManager.AppSettings.Get("Filter"));
				EncryptionThreadsCount = uint.Parse(ConfigurationManager.AppSettings.Get("EncryptionThreadsCount"));
				OnlyOneInstanceOnServer = bool.Parse(ConfigurationManager.AppSettings.Get("OnlyOneInstanceOnServer"));
				return roots;
			} catch (Exception exc) {
				exc.WriteToLog("Exception in LoadSettings: ");
				throw;
			}
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

		public static void ScanFolder(int scanId, string server, string folder, BackupFileQueue encryptionQueue) {
			foreach (string path in Helper.EnumeratePath(folder)) {	
				try {
					ScanPath(scanId, path, server, encryptionQueue);
				} catch (Exception exc) {
					if (exc is UnauthorizedAccessException || exc is IOException) {
						exc.WriteToLog("Unable to access: " + path + ": ");
					} else {
						throw exc;
					}
				}
			}

			/*if (file.FileOrPathIsNew()) {
					file.AfterProcessNewPath(() => {
						file.UpgradeStatus(BackupFile.Status.Exist);
					});
					
					try {
						BackupFile inProcess;
						if (processing.TryGetValue(file.Md5, out inProcess)) {
							LOGGER.Info(file.FullName + " is waiting for " + inProcess.FullName);
							inProcess.AfterProcessNewFile(() => {
								file.ProcessNewPath();
							});
						} else {
							if (file.FileIsNew()) {
								processing.TryAdd(file.Md5, file);

								file.Encryptor = encryptor;
								file.ProcessNewFile();
								BackupFile removed;
								file.AfterProcessNewFile(() => {
									processing.TryRemove(file.Md5, out removed);
								});
								
							} else {
								file.ProcessNewPath();
								if (!file.Encrypted) {
									file.Encrypt();
								}
							}
						}
					} catch (ApplicationException exc) {
						LOGGER.Error(exc.Message + ". Skipping " + file.FullName + "...");
					}
					
				} else {
					file.UpgradeStatus(BackupFile.Status.Exist);
					if (!file.Encrypted) {
						file.Encrypt();
					}
				}*/
		}

		private static void ScanPath(int scanId, string path, string server, BackupFileQueue encryptionQueue) {
			//LOGGER.Trace("Scaning " + path);
			BackupItem item;
			if (path[path.Length - 1] == Path.DirectorySeparatorChar) {
				item = new BackupFolder(server, path);
			} else {
				item = new BackupFile(server, path);
				Stat.IncrementScannedFilesCount();
				Stat.IncrementScannedFilesLength(item.Length);
			}

			if (item.DataOrPathIsNew()) {
				if (item.DataIsNew) {
					if (item.WriteNewData()) {
						NEW_FILES_LOGGER.Debug(item.FullName + "|" + item.Length);
						//item.Encrypt(encryptionQueue);
					}
					LOGGER.Trace("Scanned " + path + ". Data and path were added in db.");
				} else {
					LOGGER.Trace("Scanned " + path + ". Data exists, path is not in db.");
				}
				item.WriteNewPath();
			} else {
				LOGGER.Trace("Scanned " + path + ". Data and path are exist in db.");
			}
			if (!item.Encrypted && !item.Encrypting) {
				item.Encrypt(encryptionQueue);
			}
			item.UpgradeStatus(scanId, BackupFile.Status.Exist);
		}

		private static void WriteScanReport() {
			string time = Stat.ElapsedTimeString;
			LOGGER.Info(string.IsNullOrEmpty(time) ? "Done instantly" : "Done in " + time);

			LOGGER.Info("Total scanned files: " + Stat.ScannedFilesCount);
			LOGGER.Info("Total send files: " + Stat.SendFilesCount);
			LOGGER.Info("Total scanned files size: " + Stat.ScannedFilesLengthString);
			LOGGER.Info("Total unencrypted files size could be send: " + Stat.SendUnencryptedFilesLengthString);
			LOGGER.Info("Total encrypted files size were send: " + Stat.SendEncryptedFilesLengthString);
		}

		/*private static HashSet<string> scanned = new HashSet<string>();
		public static void ScanFolder2(string server, string folder) {
			foreach (string filePath in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).SkipIfNoAccess()) {
				if (scanned.Contains(filePath)) {
					LOGGER.Error("path " + filePath + " already been scanned. Weird o_0");
					break;
				}
				BackupFile file = new BackupFile("test", filePath);
				if (file.FileOrPathIsNew()) {
					file.AfterProcessNewPath(() => {
						file.UpgradeStatus(BackupFile.Status.Exist);
					});

					try {
						BackupFile inProcess;
						if (processing.TryGetValue(file.Md5, out inProcess)) {
							LOGGER.Info(file.FullName + " is waiting for " + inProcess.FullName);
							inProcess.AfterProcessNewFile(() => {
								file.ProcessNewPath();
							});
						} else {
							if (file.FileIsNew()) {
								processing.TryAdd(file.Md5, file);

								BackupFile removed;
								file.ProcessNewFile();
								file.AfterProcessNewFile(() => {
									processing.TryRemove(file.Md5, out removed);
								});

							} else {
								file.ProcessNewPath();
							}
						}
					} catch (ApplicationException exc) {
						LOGGER.Error(exc.Message + ". Skipping " + file.FullName + "...");
					}

				} else {
					file.UpgradeStatus(BackupFile.Status.Exist);
				}
				scanned.Add(filePath);
			}
		}*/

	}
}