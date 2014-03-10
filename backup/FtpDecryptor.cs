using NLog;
using System;
using System.IO;
using System.Net.FtpClient;

namespace backup {
	public class FtpDecryptor: FtpController, IDecryptor, IDisposable {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

		public FtpDecryptor():base() {
		}

		/*public void Dispose() {
			base.Dispose();
		}*/

		public long Decrypt(string md5, string targetPath) {
			if (string.IsNullOrEmpty(targetPath))
				throw new ApplicationException("No target to restore");
			if (string.IsNullOrEmpty(MainClass.Password))
				throw new ApplicationException("No password to decrypt");
			
			return IntDecrypt(0, md5, targetPath);
		}
		
		private long IntDecrypt(uint attempt, string md5, string targetPath) {
			long decryptedLength;
			string srcFullPath = GetFilePath(md5);
			
			try {
				using (Stream ftpConn = OpenRead(srcFullPath)) {

					/*if (!Directory.Exists("/home/andrew/t-server/" + srcFolder))
						Directory.CreateDirectory("/home/andrew/t-server/" + srcFolder);
					using (FileStream destination = new FileStream("/home/andrew/t-server/" + srcFolder + "/" + srcFile, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
						int num;
						byte[] buffer = new byte[0x14000];
						while ((num = ftpConn.Read(buffer, 0, buffer.Length)) != 0) {
							destination.Write(buffer, 0, num);
						}
					}*/

					decryptedLength = Crypto.DecryptFile(ftpConn, targetPath, MainClass.Password);
				}
				return decryptedLength;
			} catch (Exception exc) {
				if (exc is TimeoutException || exc is IOException || exc is FtpCommandException) {
					if (attempt < retrNumber) {
						LOGGER.Info("Decrypt attempt No" + attempt + " of file '" + srcFullPath + "' failed");
						//exc.WriteToLog();
						DisposeConnection();
						CreateConnection();
						if (File.Exists(targetPath)) {
							File.Delete(targetPath);
						}
						return IntDecrypt(attempt + 1, md5, targetPath);
					}
				}
				throw;
			}
		}
	}
}

