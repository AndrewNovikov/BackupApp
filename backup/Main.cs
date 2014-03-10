using NLog;
using System;
using System.Collections.Generic;
//using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Linq;
using System.Configuration;
using System.Net;
using System.Net.Sockets;

namespace backup {
	class MainClass {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		//private static string root;
		//private static readonly SortedSet<string> filesOnInsert = new SortedSet<string>();
		//private static object setLock = new object();
		//private static ConcurrentDictionary<string, BackupFile> processing = new ConcurrentDictionary<string, BackupFile>();
		//private static FtpUploader encryptor = new FtpUploader();
		public static string Password;
		public static string BackupStore;
		public static string FtpUser;
		public static string FtpPassword;
		public static uint EncryptionThreadsCount;

		public static void Main(string[] args) {

			Socket s = new Socket(new IPAddress(new byte[]{10,10,10,10}).AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			IDisposable m_s = s as IDisposable;
			if (m_s != null) {
				LOGGER.Info("Socket is disposable");
				m_s.Dispose();
			} else {
				LOGGER.Info("Socket is NOT disposable!!!!!!!!!!!!!!!!");
			}

			int scanId = -1;
			try {
				string[] roots = LoadSettings();

				scanId = DAL.CreateNewScan();
				using (FtpUploader encryptor = new FtpUploader(EncryptionThreadsCount)) {
					foreach (string root in roots) {
						SafeScanFolder(scanId, root, encryptor);
					}
					encryptor.WaitAll();
				}
			} catch (Exception exc) {
				exc.WriteToLog("main exception: ");
			} finally {
				if (scanId != -1) {
					try {
						DAL.EndScan(scanId);
					} catch (Exception exc) {
						exc.WriteToLog("main exception on main finally: ");
					}
				}
			}
		}

		private static string[] LoadSettings() {
			try {
				string[] roots = ConfigurationManager.AppSettings.Get("rootPaths").Split('|');
				Password = ConfigurationManager.AppSettings.Get("password");
				if (string.IsNullOrWhiteSpace(Password))
					throw new ApplicationException("Password must be set");
				BackupStore = ConfigurationManager.AppSettings.Get("targetFtp");
				FtpUser = ConfigurationManager.AppSettings.Get("ftpUser");
				FtpPassword = ConfigurationManager.AppSettings.Get("ftpPassword");
				EncryptionThreadsCount = uint.Parse(ConfigurationManager.AppSettings.Get("EncryptionThreadsCount"));
				return roots;
			} catch (Exception exc) {
				exc.WriteToLog("Exception in LoadSettings: ");
				throw;
			}
		}

		public static void SafeScanFolder(int scanId, string folder, FtpUploader encryptor) {
			try {
				ScanFolder(scanId, System.Environment.MachineName, folder, encryptor);
			} catch (Exception exc) {
				exc.WriteToLog("ScanFolder exception: ");
			}
		}


		//private static HashSet<string> scanned2 = new HashSet<string>();
		public static void ScanFolder(int scanId, string server, string folder, FtpUploader encryptor) {

			foreach (string path in Helper.EnumeratePath(folder)) {	
				LOGGER.Debug("starting file: " + path);
				//foreach (string filePath in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).SkipIfNoAccess()) {
				//if (Helper.PathIsLink(filePath)) {
				//	LOGGER.Error("path " + filePath + " is symlink. Skipping...");
				//	break;
				//}
				//if (scanned2.Contains(filePath)) {
				//	LOGGER.Error("path " + filePath + " already been scanned. Weird o_0");
				//	break;
				//}

				BackupItem item;
				if (path[path.Length - 1] == Path.DirectorySeparatorChar) {
					item = new BackupFolder(server, path);
				} else {
					item = new BackupFile(server, path) { Encryptor = encryptor };
				}

				if (item.DataOrPathIsNew()) {
					LOGGER.Debug("file or path is new: " + path);
					if (item.DataIsNew) {
						if (item.WriteNewData()) {
							item.Encrypt();
						}
					}
					item.WriteNewPath();
				} else {
					//LOGGER.Debug("file is old: " + path);
				}
				if (!item.Encrypted && !item.Encrypting) {
					item.Encrypt();
				}
				item.UpgradeStatus(scanId, BackupFile.Status.Exist);

			}
			//LOGGER.Info("scan of " + folder + " is finished");
			//scanned2.Add(path);

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

		/*public static void ScanFolder(string server, string folder, Action<FileInfo, string> onNewFile) {
			using (DAL dal = new DAL()) {
				foreach (string filePath in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).SkipIfNoAccess()) {

					try {
						FileInfo file = new FileInfo(filePath);

						if (!DAL.FileInDb(server, file)) {
							string fileMd5 = Crypto.GetMd5(file.FullName);
							//if file can be read
							if (fileMd5 != null) {
								long fileId = DAL.GetFileId(fileMd5, file);
								if (fileId == -1 && !IsOnGoing(fileMd5)) {
									OnNewData(dal, server, file, fileMd5);
								} else {
									DAL.AddNewPath(server, fileId, file);
								}
							}
						}
					} catch(Exception exc) {
						exc.WriteToLog("exception on file " + filePath + ": ");
					}

				}
			}
		}*/

		/*public static void OnNewData(DAL dal, string server, FileInfo file, string fileMd5) {
			SetOnGoing(fileMd5);
			System.Threading.ThreadPool.QueueUserWorkItem((o) => {
				try {

					string fullPath = "/Encrypted/" + fileMd5 + ".enc";
					if (File.Exists(fullPath))
						throw new ApplicationException("File " + file.FullName + " already encrypted");
					
					Crypto.EncryptFile(file.FullName, fullPath, "test");
					Console.WriteLine(file.FullName);

					long fileId = DAL.AddNewFile(fileMd5, file);
					DAL.AddNewPath(server, fileId, file);

					UnsetOnGoing(fileMd5);

				} catch (Exception exc) {
					exc.WriteToLog("exception at thread " + System.Threading.Thread.CurrentThread.ManagedThreadId + " on file " + file.FullName + ": ");
				}
			});
		}*/

		/*public static void SetOnGoing(string md5) {
			lock (setLock) {
				filesOnInsert.Add(md5);
			}
		}

		public static bool IsOnGoing(string md5) {
			return filesOnInsert.Contains(md5);
		}

		public static void UnsetOnGoing(string md5) {
			lock (setLock) {
				filesOnInsert.Remove(md5);
			}
		}*/

	}
}