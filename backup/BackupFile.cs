using NLog;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace backup {
	public class BackupFile: BackupItem {
		//private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		private FileInfo _file;

		#region "Properties
		public override string FullName {
			get {
				return _file.FullName;
			}
		}

		public override string FolderPath {
			get {
				return _file.DirectoryName;
			}
		}

		public override long Length {
			get {
				return _file.Length;
			}
		}

		public override DateTime LastWriteTime {
			get {
				return _file.LastWriteTime;
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

		/*private string _md5String;
		public string Md5String {
			get {
				if (_md5String == null) {
					StringBuilder sb = new StringBuilder();
					foreach (Byte b in Md5)
						sb.Append(b.ToString("x2").ToLower());
					_md5String = sb.ToString();
				}
				return _md5String;
			}
		}*/

		/*private long _dbDataId = -2;
		protected override long DbDataId {
			get {
				if (_dbDataId == -2) {
					_dbDataId = DAL.GetDataId(this.Md5, _file);
				}
				return _dbDataId;
			}
		}*/

		/*private long _dbPathId = -2;
		protected override long DbPathId {
			get {
				if (_dbPathId == -2) {
					this.DataOrPathIsNew();
				}
				return _dbPathId;
			}
		}*/

		private bool? _ready;
		public override bool Encrypted {
			get {
				if (!_ready.HasValue) {
					_ready = DAL.GetDataReady(this.DbDataId);
				}
				return _ready.Value;
			}
		}
		#endregion

		#region "Constructors"
		public BackupFile(string server, string fileFullPath):this(server, new FileInfo(fileFullPath)) {
		}

		public BackupFile(string server, FileInfo file): base(server) {
			_file = file;
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

		public override void Encrypt() {
			if (this.Length > 0) {
				var self = this;
				Encrypting = true;
				Encryptor.Add(this, () => {
					self._ready = true;
					self.Encrypting = false;
					DAL.SetDataReady(this.DbDataId);
				});
			}
		}


		public override bool DataOrPathIsNew() {
			DbItemDataInfo data;
			if (DAL.GetDbCacheData(new DbItemSearchInfo(this.FullName, this.LastWriteTime, this.Length), out data)) {
				_md5 = data.Md5;
				_dbDataId = data.DataId;
				_dbPathId = data.PathId;
				_ready = data.Ready;
				return false;
			} else {
				//_md5 will be taken from database. No checking with real md5 to speed up the process
				return !DAL.ItemInDb(_server, this, ref _md5, ref _dbDataId, ref _ready, out _dbPathId);
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

