//using NLog;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace backup {
	public class BackupFile: BackupItem {

		#region "Properties
		public override string FolderPath {
			get {
				var ls = _fullPath.LastIndexOf(Path.DirectorySeparatorChar);
				return ls < 0 ? _fullPath : _fullPath.Substring(0, ls);
			}
		}

		public override long Length {
			get {
				return IOHelper.GetFileLength(_fullPath);
			}
		}

		private string _md5;
		public override string Md5 {
			get {
				if (_md5 == null) {
					_md5 = Crypto.GetMd5(this.FullName);
					if (_md5 == null)
						throw new ApplicationException("Cannot read " + this.FullName + " file md5");
				}
				return _md5;
			}
		}

		private bool? _ready;
		public override bool Encrypted {
			get {
				if (!_ready.HasValue) {
					_ready = DAL.Instance.GetDataReady(this.DbDataId);
				}
				return _ready.Value;
			}
		}
		#endregion

		#region "Constructors"
		public BackupFile(string server, string fileFullPath):base(server, fileFullPath) {

		}
		#endregion

		/*private void Encrypt(string target) {
			if (File.Exists(target))
				throw new ApplicationException("File " + this.FullName + " already encrypted");

			Crypto.EncryptFile(Md5, this.FullName, Path.Combine(target, this.Md5 + ".enc"), _server);
		}*/

		/*public void WriteEncrypted(Stream target) {
			Crypto.EncryptFile(Md5, this.FullName, target, _server);
		}*/

		public static void OnEncryptionEnds(BackupFile file, long encryptedLength) {
			file._ready = true;
			file.Encrypting = false;
			DAL.Instance.SetDataReady(file.DbDataId);

			MainClass.Stat.IncrementSendFilesCount();
			MainClass.Stat.IncrementSendUnencryptedFilesLength(file.Length);
			MainClass.Stat.IncrementSendEncryptedFilesLength(encryptedLength);
		}

		public override void Encrypt(BackupFileQueue encryptor) {
			if (this.Length > 0) {
				Encrypting = true;
				encryptor.Add(this);
			}
		}


		public override bool DataOrPathIsNew() {
			if (DAL.Instance.GetDbCacheDataForServer(_server, this.FullName, this.LastWriteTime, this.Length, ref _md5, ref _dbDataId, ref _ready, out _dbPathId)) {
				return false;
			} else if (!MainClass.OnlyOneInstanceOnServer) {
				//_md5 will be taken from database. No checking with real md5 to speed up the process
				return !DAL.Instance.ItemInDb(_server, this, ref _md5, ref _dbDataId, ref _ready, out _dbPathId);
			} else {
				return true;
			}
		}

		/*public void UpgradeStatus(int scanId, Status cur) {
			if (DbPathId == -1) throw new ApplicationException("No pathId is set. Forget to process new path?");
			DAL.SetStatus(scanId, DbPathId, cur);
		}*/

		/*public bool FileIsNew() {
			return DbDataId == -1;
		}*/

		//public long GetDbFileId() {
		//	return DAL.GetFileId(this.Md5, _file);
		//}

	}
}

