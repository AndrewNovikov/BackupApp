using NLog;
using System;
using System.IO;
using System.Net;
using System.Net.FtpClient;
using System.Collections.Generic;

namespace backup {
	public class FtpController: IDisposable {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		protected readonly string _targetFtp;
		protected readonly string _ftpMode;
		protected readonly string _ftpUser;
		protected readonly string _ftpPassword;
		private FtpClient connection;
		protected static readonly uint retrNumber = 4;
		
		//private static List<string> _directoryListing;

		public FtpController() {
			_targetFtp = MainClass.Settings.FtpEncryptor.TargetFtp;
			_ftpMode = MainClass.Settings.FtpEncryptor.FtpMode;
			_ftpUser = MainClass.Settings.FtpEncryptor.FtpUser;
			_ftpPassword = MainClass.Settings.FtpEncryptor.FtpPassword;
			CreateConnection();
		}

		public Stream OpenWrite(string path) {
			return connection.OpenWrite(path);
		}

		public Stream OpenRead(string path) {
			return connection.OpenRead(path);
		}

		public void DeleteFileByMd5(string md5) {
			string filePath = GetFilePath(md5);
			DeleteFile(filePath);
		}

		public void DeleteFile(string fullName) {
			try {
				connection.DeleteFile(fullName);
			} catch (Exception exc) {
				LOGGER.Error("Exception " + exc.GetType() + " message:" + exc.Message + " on file " + fullName);
				throw;
			}
		}

		public bool FileExistsByMd5(string md5) {
			string filePath = GetFilePath(md5);
			return FileExists(filePath);
		}

		public bool FileExists(string fullName) {
			try {
				return connection.FileExists(fullName);
			} catch (Exception exc) {
				LOGGER.Error("Exception " + exc.GetType() + " message:" + exc.Message + " on file " + fullName);
				throw;
			}
		}

		protected string GetFilePath(string md5, out string folder, out string file) {
			folder = md5.Substring(0, 2);
			file = md5.Substring(2, md5.Length - 2) + ".enc";
			return folder + '/' + file;
		}

		protected string GetFilePath(string md5) {
			string dstFolder;
			string dstFile;
			return GetFilePath(md5, out dstFolder, out dstFile);
		}

		private static HashSet<string> _directoryCache = new HashSet<string>();
		private static readonly object dirLock = new object();
		public void CreateDirectoryIfNotExist(string name) {
			lock (dirLock) {
				if (!_directoryCache.Contains(name)) {
					if (!connection.DirectoryExists(name)) {
						connection.CreateDirectory(name);
					}
					_directoryCache.Add(name);
				}
			}
			//lock (dirLock) {
			//  if (_directoryListing == null) {
			//    LOGGER.Debug("FtpEncryptor: gathering directories listing in a thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
			//    _directoryListing = new List<string>();
					
			//    connection.SetWorkingDirectory("/");
			//    foreach (FtpListItem item in connection.GetListing()) {
			//      if (item == null) throw new NullReferenceException("item in GetListing() result is null");
			//      if (item.Type == FtpFileSystemObjectType.Directory) {
			//        _directoryListing.Add(item.Name);
			//      }
			//    }

			//  }
				
			//  if (!_directoryListing.Contains(name)) {
			//    connection.CreateDirectory(name);
			//    _directoryListing.Add(name);
			//  }
			//}
		}
		
		protected void CreateConnection() {
			connection = new FtpClient();
			connection.Host = _targetFtp;
			connection.DataConnectionReadTimeout = 60000;
			switch (_ftpMode) {
			case "active":
				connection.DataConnectionType = FtpDataConnectionType.AutoActive;
				break;
			case "passive":
				connection.DataConnectionType = FtpDataConnectionType.AutoPassive;
				break;
			default:
				throw new ApplicationException("Ftp mode support only active or passive");
			}
			connection.Credentials = new NetworkCredential(_ftpUser, _ftpPassword);
			connection.EnableThreadSafeDataConnections = false;
			LOGGER.Debug("FtpEncryptor: connection of thread " + System.Threading.Thread.CurrentThread.ManagedThreadId + " created");
		}
		
		public void Dispose() {
			DisposeConnection();
			LOGGER.Debug("FtpEncryptor of thread " + System.Threading.Thread.CurrentThread.ManagedThreadId + " disposed");
		}
		
		protected void DisposeConnection() {
			if (connection.IsConnected) {
				connection.Disconnect();
				LOGGER.Debug("FtpEncryptor: connection of thread " + System.Threading.Thread.CurrentThread.ManagedThreadId + " disconnected");
			}
			if (!connection.IsDisposed) {
				connection.Dispose();
			}
			//lock (dirLock) {
			//  _directoryListing = null;
			//}
			lock (dirLock) {
				_directoryCache = null;
			}
		}
		
	}
}

