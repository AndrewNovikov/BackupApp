using NLog;
using System;
using System.IO;
using System.Net.FtpClient;

namespace backup {
	public class FtpEncryptor: FtpController, IBackupEngine, IDisposable {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

		public FtpEncryptor():base() {
		}

		/*public void Dispose() {
			base.Dispose();
		}*/

		public long Backup(IBackupItem file) {
			//file.Data.InRemoteProgressByThisProcess = DateTime.Now;

			if (string.IsNullOrEmpty(_targetFtp))
				throw new ApplicationException("No target to backup");
			if (string.IsNullOrEmpty(MainClass.Settings.FtpEncryptor.Password))
				throw new ApplicationException("No password to encrypt");

			MainClass.Stat.IncrementSendUnencryptedFilesLength(file.Length);
			long encryptedLength = file.Length == 0 ? 0 : IntEncrypt(0, file);
			MainClass.Stat.IncrementSendEncryptedFilesLength(encryptedLength);

			//file.Data.InRemoteProgressByThisProcess = null;
			//file.Data.OnRemote = true;
			return encryptedLength;
		}

		private long IntEncrypt(uint attempt, IBackupItem file) {
			long encryptedLength;
			string dstFolder;// = file.Md5.Substring(0, 2);
			string dstFile;// = file.Md5.Substring(2, file.Md5.Length - 2) + ".enc";
			string dstFullPath = GetFilePath(file.Md5, out dstFolder, out dstFile);

			try {
				CreateDirectoryIfNotExist(dstFolder);
				using (Stream ftpConn = OpenWrite(dstFullPath)) {
					encryptedLength = Crypto.EncryptFile(file.Md5, file.FullName, ftpConn, MainClass.Settings.FtpEncryptor.Password);
				}
				LOGGER.Trace(file.Md5 + " data is backed up to remote");
				return encryptedLength;
			} catch (Exception exc) {
				if (exc is TimeoutException || exc is IOException || exc is FtpCommandException || exc is System.Net.Sockets.SocketException) {
					if (attempt < retrNumber) {
						LOGGER.Debug("Encrypt attempt No" + attempt + " of file '" + dstFullPath + "' failed");
						//exc.WriteToLog();
						DisposeConnection();
						CreateConnection();
						return IntEncrypt(attempt + 1, file);
					}
				}
				throw;
			}
		}

	}
}

