//using NLog;
using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.FtpClient;

namespace backup {
	public class FtpUploader: BackupFileQueue, IDisposable {
		//private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

		private static ConcurrentDictionary<int, FtpClient> locked = new ConcurrentDictionary<int, FtpClient>();
		private static ConcurrentBag<FtpClient> unlocked = new ConcurrentBag<FtpClient>();

		/*private static bool _connectionCreated = false;
		private static readonly Lazy<FtpClient> _ftpCon =
			new Lazy<FtpClient>(() => {
			  FtpClient conn = new FtpClient();
			  conn.Host = MainClass.BackupStore;
			  conn.DataConnectionType = FtpDataConnectionType.AutoActive;
			  conn.Credentials = new NetworkCredential(MainClass.FtpUser, MainClass.FtpPassword);
				conn.EnableThreadSafeDataConnections = false;
				_connectionCreated = true;
			  return conn;
			});

		private static FtpClient Connection {
			get {
				return _ftpCon.Value;
			}
		}*/

		static FtpUploader() {
			//Connection = new FtpClient();
			//Connection.Host = MainClass.BackupStore;
			//Connection.DataConnectionType = FtpDataConnectionType.AutoActive;
			//Connection.Credentials = new NetworkCredential(MainClass.FtpUser, MainClass.FtpPassword);
			//Connection.Connect();
			//_connectionCreated = true;
		}

		public FtpUploader(Action<BackupFile> onEnd, uint maxThreadsCount): base(Upload, onEnd, maxThreadsCount){
		}

		public void Dispose() {
			if (locked.Count > 0)
				throw new ApplicationException("Not all connections are freed. Disposing while there is still work on going?");

			FtpClient connection;
			while (unlocked.TryTake(out connection)) {
				if (connection != null) { //strange bug happened
					if (connection.IsConnected) {
						connection.Disconnect();
					}
					if (!connection.IsDisposed) {
						connection.Dispose();
					}
				}
			}
			/*if (_connectionCreated) {
				FtpClient conn = Connection;
				if (conn != null) {
					if (conn.IsConnected) {
						conn.Disconnect();
					}
					if (!conn.IsDisposed) {
						conn.Dispose();
					}
				}
			}*/
		}

		#region "Ftp connections"
		/*public static void PrepareFtpConnections(uint count) {
			while (locked.Count + unlocked.Count < count) {
				unlocked.Add(CreateConnection());
			}
		}*/

		private static FtpClient CreateConnection() {
			FtpClient conn = new FtpClient();
			conn.Host = MainClass.BackupStore;
			conn.DataConnectionType = FtpDataConnectionType.AutoActive;
			conn.Credentials = new NetworkCredential(MainClass.FtpUser, MainClass.FtpPassword);
			conn.EnableThreadSafeDataConnections = false;
			//conn.Connect();
			return conn;
		}

		private static int GetConnection(out FtpClient connection) {
			FtpClient con;
			bool unlockedTaken;
			if (!unlocked.TryTake(out con)) {
				con = CreateConnection();
				unlockedTaken = false;
			} else {
				unlockedTaken = true;
			}
			int connectionId = System.Threading.Thread.CurrentThread.ManagedThreadId;
			if (!locked.TryAdd(connectionId, con))
				throw new ApplicationException("Ftp connection with id = " + connectionId + " is already locked.");

			if (con == null) Console.WriteLine(connectionId + " threadId returns null connection (unlockedTaken = " + unlockedTaken + ") ("+locked.Count+" locked) and ("+unlocked.Count+" unlocked)");
			else Console.WriteLine(connectionId + " threadId. GetConnection is good");

			connection = con;
			//LOGGER.Info(unlocked.Count + " unlocked connections and " + locked.Count + " locked connections on GetConnection in thread " + connectionId);
			return connectionId;
		}

		private static void ReleaseConnection(int connectionId) {
			FtpClient result;
			if (!locked.TryRemove(connectionId, out result))
				throw new ApplicationException("No ftp connection with id " + connectionId + " is locked.");
			if (result != null)
				unlocked.Add(result);
		}
		#endregion

		private static void Upload(BackupFile file) {
			//try {
			if (string.IsNullOrWhiteSpace(MainClass.BackupStore) || string.IsNullOrWhiteSpace(MainClass.Password))
				return;

			string dstFolder = file.Md5.Substring(0, 2);
			string dstFile = file.Md5.Substring(2, file.Md5.Length - 2) + ".enc";

			FtpClient connection;
			int connectionId = GetConnection(out connection);
			try {
				if (!connection.DirectoryExists(dstFolder)) {
					connection.CreateDirectory(dstFolder);
				}
				using (Stream ftpConn = connection.OpenWriteMultiple(dstFolder + '/' + dstFile)) {
					//using (Stream ftpConn = connection.OpenWriteMultiple(file.Md5 + ".enc")) {
					Crypto.EncryptFile(file.Md5, file.FullName, ftpConn, MainClass.Password);
				}
			} finally {
				ReleaseConnection(connectionId);
			}
			/*} catch (NullReferenceException exc) {
				Console.WriteLine(exc.Message + " file == null = " + (file == null).ToString() + " md5==null=" + (file.Md5 == null));
				throw;
			}*/

			/*FtpWebRequest request = (FtpWebRequest)WebRequest.Create(MainClass.BackupStore + file.Md5 + ".enc");
			request.UsePassive = false;
			request.UseBinary = true;
			request.Method = WebRequestMethods.Ftp.UploadFile;
			request.Credentials = new NetworkCredential(MainClass.FtpUser, MainClass.FtpPassword);

			using (Stream requestStream = request.GetRequestStream()) {
				Crypto.EncryptFile(file.Md5, file.FullName, requestStream, MainClass.Password);
				requestStream.Close();
			}

			FtpWebResponse response = (FtpWebResponse)request.GetResponse();
			response.Close();*/
		}

	}
}