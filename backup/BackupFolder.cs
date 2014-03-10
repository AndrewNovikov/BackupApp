using System;
using System.IO;

namespace backup {
	public class BackupFolder: BackupItem {

		#region "Properties"
		public override string FolderPath {
			get {
				return this.FullName;
			}
		}

		public override long Length {
			get {
				return 0L;
			}
		}

		public override string Md5 {
			get {
				return Crypto.EmptyMd5Str;
			}
		}

		public override bool Encrypted {
			get {
				return true;
			}
		}
		#endregion

		#region "Constructors"
		public BackupFolder(string server, string fullPath):base(server, fullPath) {
		}
		#endregion

		public override bool DataOrPathIsNew() {
			string dummyMd5 = null;
			bool? dummyReady = false;
			if (DAL.Instance.GetDbCacheDataForServer(_server, this.FullName, this.LastWriteTime, this.Length, ref dummyMd5, ref _dbDataId, ref dummyReady, out _dbPathId)) {
				return false;
			} else if (!MainClass.OnlyOneInstanceOnServer) {
				return !DAL.Instance.ItemInDb(_server, this, ref dummyMd5, ref _dbDataId, ref dummyReady, out _dbPathId);
			} else {
				return true;
			}
		}

		public override void Encrypt(BackupFileQueue encryptor) {
		}

	}
}