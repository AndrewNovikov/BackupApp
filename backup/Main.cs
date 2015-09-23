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
using System.Diagnostics;

namespace backup {
	public class MainClass {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		private static readonly Logger PROFILER_LOGGER = LogManager.GetLogger("profiler");
		private static Mutex mutex;
		private static bool ignoreInProgressByOther = false;
		/*private static DateTime startTime;
		private static long totalFilesLength;
		private static long totalFilesCount;*/
		//private static readonly object _totalFilesLengthLock = new object();

		//public static string Password;
		//public static string BackupStore;
		//public static string FtpUser;
		//public static string FtpPassword;
		//public static string FtpMode;
		//public static uint EncryptionThreadsCount;
		//public static bool OnlyOneInstanceOnServer;
		//public static string DbConnectionString;
		//public static long SaveScans;
		//public static readonly Filter ExcludeFilter = new Filter();
		public static ApplSettings Settings;
		public static readonly Report Stat = new Report();

		public static bool IgnoreInProgressByOther {
			get {
				return ignoreInProgressByOther;
			}
		}

		public static void Main(string[] args) {
			try {

				AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Backup_UnhandledException);

				Settings = ConfigurationManager.GetSection("ApplSettings") as ApplSettings;

				string server = System.Environment.MachineName;

				//Console.WriteLine("before " + System.GC.GetTotalMemory(false).ToString("N"));
				//Dictionary<string, DataInfo> dataCache = DataController.GetCache();
				//Console.WriteLine("after " + System.GC.GetTotalMemory(false).ToString("N"));
				//var cache = new FileRecordController("backup", "/raid/data/t-server");
				//Console.WriteLine("finally " + System.GC.GetTotalMemory(false).ToString("N"));
				//Console.WriteLine("done");
				//Console.ReadLine();
				//return;

				if (Settings.OnlyOneInstanceOnServer) {
					mutex = new Mutex(true, "andrewsBackupApp");
					//if (!mutex.WaitOne(TimeSpan.FromSeconds(2), false)) {
					if (!mutex.WaitOne(TimeSpan.Zero, true)) {
						LOGGER.Error("Application already started!");
						return;
					}
				}

				ignoreInProgressByOther = Array.IndexOf(args, "-ignoreInProgressByOther") != -1;
				int indexOfRestore = Array.LastIndexOf(args, "-restore");
				if (indexOfRestore != -1) {
					//int indexOfCleanArg = Array.IndexOf(args, "-clean");
					//if (indexOfCleanArg != -1) {
					//  uint cleanScanIdArg;
					//  if (uint.TryParse(args[indexOfCleanArg + 1], out cleanScanIdArg)) {
					//    RemoveScanId(cleanScanIdArg);
					//  } else {
					//    throw new ApplicationException("After clean argument should go scanId argument in uint mode");
					//  }
					//}
					//int indexOfCleanOrphanPathsArg = Array.IndexOf(args, "-cleanOrphans");
					//if (indexOfCleanOrphanPathsArg != -1) {
					//  LOGGER.Info("Clean orphans started");
					//  int orphanLimit;
					//  if (int.TryParse(args[indexOfCleanOrphanPathsArg + 1], out orphanLimit)) {
					//    uint cleanCount;
					//    if (uint.TryParse(args[indexOfCleanOrphanPathsArg + 2], out cleanCount)) {
					//      Clean(orphanLimit, cleanCount);
					//    } else {
					//      Clean(orphanLimit, 1);
					//    }
					//  } else {
					//    //Clean(0, 1);
					//    Clean(100000, int.MaxValue);
					//  }
					//  LOGGER.Info("Clean orphans finished");
					//}
          //int scanId;
          //if (int.TryParse(args[indexOfRestore + 1], out scanId)) {
          //  if (args.Length < indexOfRestore + 3) {
          //    throw new ApplicationException("'backup.exe -restore scanId targetPath <sourcePath>' should be the syntax");
          //  }
          //  string targetPath = args[indexOfRestore + 2];
          //  string sourcePath = args.Length > indexOfRestore + 3 ? args[indexOfRestore + 3] : null;
          //  LOGGER.Info("Restoring...");
          //  Restore(scanId, targetPath, sourcePath);
          //} else {
          //  throw new ApplicationException("'backup.exe -restore scanId targetPath <sourcePath>' should be the syntax");
          //}
					/*int indexOfRestoreDb = Array.LastIndexOf(args, "-restoreDb");
					if (indexOfRestoreDb != -1) {
						if (args.Length > indexOfRestoreDb + 1) {
							DAL.Instance.RestoreFromFtp(args[indexOfRestoreDb + 1], args[indexOfRestoreDb + 2]);
						} else {
							throw new ApplicationException("'backup.exe -restoreDb srcFileName targetPath' should be the syntax");
						}
					}*/
				} else {
					Scan(server);
					if (Settings.BackupDb) {
						//string targetFile = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_backup.mysql";
						//LOGGER.Info("Backing up MySQL database to the file '" + targetFile + "'...");
						//DAL.Instance.BackupToFtp("DataBase", targetFile);
						//LOGGER.Info("MySQL database backup complete");
					}
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

		static void Backup_UnhandledException(object sender, UnhandledExceptionEventArgs args) {
			if (mutex != null) {
				mutex.ReleaseMutex();
			}
			Exception exc = args.ExceptionObject as Exception;
			if (exc != null) {
				exc.WriteToLog("unhandled exception: ");
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

    //private static void Restore(int scanId, string targetPath, string sourcePath) {
    //  Directory.CreateDirectory(targetPath);

    //  IRecoverer decr = new FtpDecryptor();
    //  foreach (RecoverInfo rec in DAL.Instance.GetAllPathsStartedWith(scanId, sourcePath)) {
    //    rec.Recover(decr, targetPath);
    //  }
    //}

		#region "RemoveOldScans"
    //private static void RemoveScanId(long scanId) {
    //  LOGGER.Info("Clean scan with id=" + scanId + " started");
    //  DAL.Instance.DeleteScanRow(scanId);
    //  Clean(100000, int.MaxValue);
    //}

    //private static void RemoveOldScans(string server, long saveLastCount) {
    //  long[] scans = DAL.Instance.GetScanId(server);
    //  for (int i=0; i < scans.Length - saveLastCount; i++) {
    //    RemoveScanId(scans[i]);
    //  }
    //}

    //private static void Clean(int limit, uint cleanCount) {
    //  long cleaned;
    //  uint cleanPathsCount = cleanCount;
    //  while (cleanPathsCount != 0 && (cleaned = CleanPaths(limit)) > 0) {
    //    cleanPathsCount--;
    //    LOGGER.Info(cleaned + " cleaned orphan paths");
    //  }

    //  uint cleanDataCount = cleanCount;
    //  while (cleanDataCount != 0 && (cleaned = CleanDatas(limit)) > 0) {
    //    cleanDataCount--;
    //    LOGGER.Info(cleaned + " cleaned orphan datas");
    //  }
    //}

    //private static long CleanPaths(int limit) {
    //  long result = 0;

    //  foreach (long pathId in DAL.Instance.GetOrphanPathId(limit)) {
    //    DAL.Instance.DeletePathRow(pathId);
    //    result++;
    //    LOGGER.Debug("PathId " + pathId + " row deleted");
    //  }
    //  return result;
    //}

    //private static long CleanDatas(int limit) {
    //  long result = 0;
    //  FtpController ftp = new FtpController();
			
    //  foreach (KeyValuePair<long,string> dataIdMd5 in DAL.Instance.GetOrphanDataId(limit)) {
    //    if (dataIdMd5.Value != Crypto.EmptyMd5Str) {

    //      try {
    //        ftp.DeleteFileByMd5(dataIdMd5.Value);
    //      } catch (System.Net.FtpClient.FtpCommandException exc) {
    //        LOGGER.Error("File " + dataIdMd5.Value + " exception: " + exc.Message);
    //      }

    //    }

    //    DAL.Instance.DeleteDataRow(dataIdMd5.Key);
    //    LOGGER.Debug("DataId " + dataIdMd5.Key + " row deleted");

    //    result++;
    //  }
    //  return result;
    //}
		#endregion

		private static void Scan(string server) {
			using (FsScanner scanner = new FsScanner(server)) {
				Dictionary<long, List<IBackupItem>> postponed = new Dictionary<long, List<IBackupItem>>();
				LocalBackup lBackup = new LocalBackup(Settings.LocalBackup.BackupLocation, Settings.LocalBackup.SingleTarget);

				using (BackupFileQueue rBackupQueue = new BackupFileQueue(Settings.FtpEncryptor.EncryptionThreadsCount, () => new FtpEncryptor())) {
					using (BackupFileQueue lBackupQueue = new BackupFileQueue(Settings.LocalBackup.ThreadsCount, () => lBackup)) {

						rBackupQueue.Success += rBackupQueue_Success;
						rBackupQueue.Failure += rBackupQueue_Failure;

						lBackupQueue.Success += lBackupQueue_Success;
						lBackupQueue.Failure += lBackupQueue_Failure;

						foreach (string root in Settings.RootPaths) {
							FileRecordController fController = new FileRecordController(server, root);
							foreach (FsInfo item in scanner.GetItems(root)) {
								Stopwatch sw = Stopwatch.StartNew();
								FileRecord dbRec = fController.GetAndUpsertDbFile(item);

								//files with no access won't be processed
								if (dbRec != null) {
									DAL.Instance.AddFileRecScan(dbRec.ID, scanner.ScanId);

									//dbRec.Data.GetLastInProgressByOtherProcess(); not optimal. will do that in postponed processing
									if (!dbRec.Data.InProgress) {

										if (!dbRec.Data.OnRemote) {
											dbRec.Data.InRemoteProgressByThisProcess = DateTime.Now;
											if (!rBackupQueue.Add(dbRec)) {
												dbRec.Data.InRemoteProgressByThisProcess = null;
											}
										} else {
											LOGGER.Trace("File " + dbRec.FullName + " md5=" + dbRec.Data.MD5 + " already on Remote");
										}

										if (!dbRec.Data.OnLocal) {
											dbRec.Data.InLocalProgressByThisProcess = DateTime.Now;
											if (!lBackupQueue.Add(dbRec)) {
												dbRec.Data.InLocalProgressByThisProcess = null;
											}
										} else {
											LOGGER.Trace("File " + dbRec.FullName + " md5=" + dbRec.Data.MD5 + " already on Local. Linking...");
											lBackup.Link(dbRec);
										}

									} else {
										lock (dbRec.Data) {
											if (dbRec.Data.InRemoteProgressByThisProcess.HasValue) {
												LOGGER.Trace("Data id " + dbRec.Data.ID + " md5 " + dbRec.Data.MD5 + " file " + item.FullName + " is backing up to remote server by this process since " + dbRec.Data.InRemoteProgressByThisProcess.Value.ToString("dd.MM.yyyy HH:mm:ss.fff") + ". Backup suspended.");
											} else if (dbRec.Data.InLocalProgressByThisProcess.HasValue) {
												LOGGER.Trace("Data id " + dbRec.Data.ID + " md5 " + dbRec.Data.MD5 + " file " + item.FullName + " is backing up to local server by this process since " + dbRec.Data.InLocalProgressByThisProcess.Value.ToString("dd.MM.yyyy HH:mm:ss.fff") + ". Backup suspended.");
											} else if (dbRec.Data.InProgressByOtherProcess.HasValue) {
												LOGGER.Trace("Data id " + dbRec.Data.ID + " md5 " + dbRec.Data.MD5 + " file " + item.FullName + " is backing up by someone else since " + dbRec.Data.InProgressByOtherProcess.Value.ToString("dd.MM.yyyy HH:mm:ss.fff") + ". Backup suspended.");
											} else {
												LOGGER.Trace("Data id " + dbRec.Data.ID + " md5 " + dbRec.Data.MD5 + " file " + item.FullName + " is backing up but InRemoteProgressByThisProcess, InLocalProgressByThisProcess and InProgressByOtherProcess are null");
											}
										}
										if (!postponed.ContainsKey(dbRec.Data.ID)) {
											postponed.Add(dbRec.Data.ID, new List<IBackupItem>());
										}
										LOGGER.Trace("Postponning data id " + dbRec.Data.ID + " md5 " + dbRec.Data.MD5 + " file " + item.FullName);
										postponed[dbRec.Data.ID].Add(dbRec);
									}
								} else {
									LOGGER.Trace("dbRec is null from GetAndUpsertDbFile");
								}

								sw.Stop();
								PROFILER_LOGGER.Info("Time taken: {0}ms for file '{1}'", sw.ElapsedMilliseconds, item.FullName);
							}
						}

					} //lBackupQueue disposing
				} //rBackupQueue disposing

				foreach (long dbDataID in postponed.Keys) {
					DataInfo dbData = DAL.Instance.GetData(dbDataID); //will refresh all fields including InProgressByOtherProcess
					LOGGER.Trace("Working on postponed " + dbData.MD5 + " data");
					//dbData.GetLastInProgressByOtherProcess();
					if (!dbData.InProgress) {
						if (dbData.OnLocal) {
							foreach (FileRecord file in postponed[dbDataID]) {
								lBackup.Link(file);
							}
						} else {
							LOGGER.Error("Data of md5 " + dbData.MD5 + " id=" + dbData.ID + " in postponed list but no local data. Skipping all local linking...");
						}
					} else {
						lock (dbData) {
							if (dbData.InProgressByOtherProcess.HasValue) {
								LOGGER.Error("Data of md5 " + dbData.MD5 + " id=" + dbData.ID + " in postponed list and still backing up by other process. Skipping all local linking...");
							} else if (dbData.InRemoteProgressByThisProcess.HasValue) {
								LOGGER.Error("Data of md5 " + dbData.MD5 + " id=" + dbData.ID + " in postponed list and still remotely backing up by this process. Skipping all local linking...");
							} else if (dbData.InLocalProgressByThisProcess.HasValue) {
								LOGGER.Error("Data of md5 " + dbData.MD5 + " id=" + dbData.ID + " in postponed list and still locally backing up by this process. Skipping all local linking...");
							} else {
								if (dbData.InProgress) {
									LOGGER.Error("Data of md5 " + dbData.MD5 + " id=" + dbData.ID + ". InProgress is true, but neither InProgressByOtherProcess no InRemoteProgressByThisProcess no InLocalProgressByThisProcess has value");
								} else {
									LOGGER.Error("Data of md5 " + dbData.MD5 + " id=" + dbData.ID + " is not in progress, but OnLocal is false.");
								}
							}
						}
					}
				}

				//DAL.Instance.RemoveOldScans(server, Settings.FtpEncryptor.SaveScans);
			}
		}

		static void rBackupQueue_Success(long resultLength, IBackupItem file) {
			DAL.Instance.SetDataReadyOnRemote(file.Data);
			LOGGER.Trace("Data " + file.Md5 + " SetDataReadyOnRemote called");
			lock (file) {
				if (!file.Data.InLocalProgressByThisProcess.HasValue) {
					DAL.Instance.SetDataReady(file.Data);
					LOGGER.Trace("Data " + file.Md5 + " ready was set.");
				}
			}
		}

		static void lBackupQueue_Success(long resultLength, IBackupItem file) {
			DAL.Instance.SetDataReadyOnLocal(file.Data);
			LOGGER.Trace("Data " + file.Md5 + " SetDataReadyOnLocal called");
			lock (file) {
				if (!file.Data.InRemoteProgressByThisProcess.HasValue) {
					DAL.Instance.SetDataReady(file.Data);
					LOGGER.Trace("Data " + file.Md5 + " ready was set.");
				}
			}
		}

		static void rBackupQueue_Failure(Exception exc, IBackupItem file) {
			exc.WriteToLog("Failure on remote backupping of file dataId " + file.Data.ID + " " + file.FullName + ". ");
			file.Data.InRemoteProgressByThisProcess = null;
			LOGGER.Trace("Data " + file.Md5 + " InRemoteProgressByThisProcess = null");
			lock (file) {
				if (!file.Data.InLocalProgressByThisProcess.HasValue) {
					DAL.Instance.SetDataReady(file.Data);
					LOGGER.Trace("Data " + file.Md5 + " ready was set.");
				}
			}
			//LOGGER.Error("Failure on remote backupping of file dataId " + file.Data.ID + " " + file.FullName + "(" + exc.GetType().ToString() + ")" + ":" + exc.Message);
		}

		static void lBackupQueue_Failure(Exception exc, IBackupItem file) {
			exc.WriteToLog("Failure on local backupping of file dataId " + file.Data.ID + " " + file.FullName + ". ");
			file.Data.InLocalProgressByThisProcess = null;
			LOGGER.Trace("Data " + file.Md5 + " InLocalProgressByThisProcess = null");
			lock (file) {
				if (!file.Data.InRemoteProgressByThisProcess.HasValue) {
					DAL.Instance.SetDataReady(file.Data);
					LOGGER.Trace("Data " + file.Md5 + " ready was set.");
				}
			}
			//LOGGER.Error("Failure on local backupping of file dataId " + file.Data.ID + " " + file.FullName + "(" + exc.GetType().ToString() + ")" + ":" + exc.Message);
		}

		/*private static ItemInfo UpdateDb(string server, DirectoryInfo folder, ItemInfo dbData) {
			ItemInfo result;
			if (dbData == null) {
				result = new ItemInfo();
				result.FullName = folder.FullName;
				result.Length = 0;
				result.FolderId = DAL.Instance.AddIfNewFolder(server, folder.FullName);
			} else {
				result = dbData;
			}
			result.Status = Status.ExistOnSource | Status.ExistOnLocalBackup | Status.ExistOnRemoteBackup;
			return result;
		}

		private static ItemInfo UpdateDb(string server, FileInfo file, ItemInfo dbData) {
			ItemInfo result;
			if (dbData == null) {
				result = new ItemInfo();
				result.Name = file.Name;
				result.FullName = file.FullName;
				result.Length = file.Length;
				result.ModTime = file.LastWriteTime.Truncate(TimeSpan.FromSeconds(1));
				result.FolderId = DAL.Instance.AddIfNewFolder(server, file.DirectoryName);
				result.Status = Status.ExistOnSource;
			} else {
				result = dbData;
				if (result.Status == null) {
					result.Status = Status.ExistOnSource;
				}
			}

			DateTime mTime = file.LastWriteTime.Truncate(TimeSpan.FromSeconds(1));
			long size = file.Length;

			if (dbData == null || dbData.Length != size || dbData.ModTime != mTime) {
				result.Md5 = Crypto.GetMd5(file.FullName);
				result.DataId = DAL.Instance.AddIfNewData(result.Md5, size); //if exception occurs then md5 exist but with other file size (can't bee so)
				result.FileId = DAL.Instance.AddNewFile(file.Name, result.FolderId.Value, result.DataId.Value, mTime);
				result.Status = Status.ExistOnSource;
			}

			return result;
		}*/

		/*private static void Scan(string server) {
			int scanId = -1;

			try {
				scanId = DAL.Instance.CreateNewScan(server);
				ChageLog(scanId);
			
				using (BackupFileQueue encryptionQueue = new BackupFileQueue(Settings.FtpEncryptor.EncryptionThreadsCount)) {
					foreach (string root in Settings.RootPaths) {
						ScanFolder(scanId, server, root, encryptionQueue);
					}
					foreach (string root in Settings.FtpEncryptor.RootPaths) {
						ScanFolder(scanId, server, root, encryptionQueue);
					}
					LOGGER.Info("Scan complete. Waiting for the working threads...");
				}
				RemoveOldScans(server, Settings.FtpEncryptor.SaveScans);
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
		}*/

		/*private static string[] LoadSettings() {
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
				SaveScans = long.Parse(ConfigurationManager.AppSettings.Get("SaveScans"));
				return roots;
			} catch (Exception exc) {
				exc.WriteToLog("Exception in LoadSettings: ");
				throw;
			}
		}*/

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

		/*public static void ScanFolder(int scanId, string server, string folder, BackupFileQueue encryptionQueue) {
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
*/
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
		//}

		/*private static void ScanPath(int scanId, string path, string server, BackupFileQueue encryptionQueue) {
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
					}
					LOGGER.Trace("Scanned " + path + ". Data and path were added in db.");
				} else {
					LOGGER.Trace("Scanned " + path + ". Data exists, path is not in db.");
				}
				item.WriteNewPath();
			} else {
				LOGGER.Trace("Scanned " + path + ". Data and path are exist in db.");
			}
			//if (!item.Encrypted && !item.Encrypting) {
			if (!item.Encrypted) {
				LOGGER.Trace("Item " + path + " with md5 " + item.Md5 + " is not encrypted. Encrypting...");
				item.Encrypt(encryptionQueue);
			}
		//item.UpgradeStatus(scanId, BackupFile.Status.Exist);
		}*/

		//private static void WriteScanReport() {
		//  string time = Stat.ElapsedTimeString;
		//  LOGGER.Info(string.IsNullOrEmpty(time) ? "Done instantly" : "Done in " + time);

		//  LOGGER.Info("Total scanned files: " + Stat.ScannedFilesCount);
		//  LOGGER.Info("Total send files: " + Stat.SendFilesCount);
		//  LOGGER.Info("Total scanned files size: " + Stat.ScannedFilesLengthString);
		//  LOGGER.Info("Total unencrypted files size could be send: " + Stat.SendUnencryptedFilesLengthString);
		//  LOGGER.Info("Total encrypted files size were send: " + Stat.SendEncryptedFilesLengthString);
		//}

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