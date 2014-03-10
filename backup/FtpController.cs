using NLog;
using System;
using System.IO;
using System.Net;
using System.Net.FtpClient;
using System.Collections.Generic;

namespace backup {
	public class FtpController: IDisposable {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		private FtpClient connection;
		protected static readonly uint retrNumber = 2;
		
		private static List<string> _directoryListing;

		public FtpController() {
			CreateConnection();
		}

		protected Stream OpenWrite(string path) {
			return connection.OpenWriteMultiple(path);
		}

		public Stream OpenRead(string path) {
			return connection.OpenRead(path);
		}

		public void DeleteFile(string md5) {
			string filePath = GetFilePath(md5);
			try {
				connection.DeleteFile(filePath);
			} catch (Exception exc) {
				LOGGER.Error("Exception " + exc.GetType() + " message:" + exc.Message + " on file " + filePath);
				throw;
			}
		}

		public bool FileExists(string md5) {
			string filePath = GetFilePath(md5);
			try {
				return connection.FileExists(filePath);
			} catch (Exception exc) {
				LOGGER.Error("Exception " + exc.GetType() + " message:" + exc.Message + " on file " + filePath);
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

		private static readonly object dirLock = new object();
		protected void CreateDirectoryIfNotExist(string name) {
			lock (dirLock) {
				if (_directoryListing == null) {
					_directoryListing = new List<string>();
					foreach (FtpListItem item in connection.GetListing()) {
						if (item == null) throw new NullReferenceException("item in GetListing() result is null");
						if (item.Type == FtpFileSystemObjectType.Directory) {
							_directoryListing.Add(item.Name);
						}
					}
				}
				
				if (!_directoryListing.Contains(name)) {
					connection.CreateDirectory(name);
					_directoryListing.Add(name);
				}
			}
		}
		
		protected void CreateConnection() {
			connection = new FtpClient();
			connection.Host = MainClass.BackupStore;
			connection.DataConnectionReadTimeout = 60000;
			switch (MainClass.FtpMode) {
			case "active":
				connection.DataConnectionType = FtpDataConnectionType.AutoActive;
				break;
			case "passive":
				connection.DataConnectionType = FtpDataConnectionType.AutoPassive;
				break;
			default:
				throw new ApplicationException("Ftp mode support only active or passive");
			}
			connection.Credentials = new NetworkCredential(MainClass.FtpUser, MainClass.FtpPassword);
			connection.EnableThreadSafeDataConnections = false;
		}
		
		public void Dispose() {
			DisposeConnection();
			LOGGER.Debug("FtpEncryptor of thread " + System.Threading.Thread.CurrentThread.ManagedThreadId + " disposed");
		}
		
		protected void DisposeConnection() {
			if (connection.IsConnected) {
				connection.Disconnect();
			}
			if (!connection.IsDisposed) {
				connection.Dispose();
			}
			lock (dirLock) {
				_directoryListing = null;
			}
		}
		
	}
}

