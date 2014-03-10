using NLog;
using System;
using System.IO;
using System.Net.FtpClient;

namespace backup {
	public class FtpEncryptor: FtpController, IEncryptor, IDisposable {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

		public FtpEncryptor():base() {
		}

		/*public void Dispose() {
			base.Dispose();
		}*/

		public long Encrypt(BackupFile file) {
			if (string.IsNullOrEmpty(MainClass.BackupStore))
				throw new ApplicationException("No target to backup");
			if (string.IsNullOrEmpty(MainClass.Password))
				throw new ApplicationException("No password to encrypt");

			return IntEncrypt(0, file);
		}

		private long IntEncrypt(uint attempt, BackupFile file) {
			long encryptedLength;
			string dstFolder;// = file.Md5.Substring(0, 2);
			string dstFile;// = file.Md5.Substring(2, file.Md5.Length - 2) + ".enc";
			string dstFullPath = GetFilePath(file.Md5, out dstFolder, out dstFile);

			try {
				CreateDirectoryIfNotExist(dstFolder);
				using (Stream ftpConn = OpenWrite(dstFullPath)) {
					encryptedLength = Crypto.EncryptFile(file.Md5, file.FullName, ftpConn, MainClass.Password);
				}
				return encryptedLength;
			} catch (Exception exc) {
				if (exc is TimeoutException || exc is IOException || exc is FtpCommandException) {
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

