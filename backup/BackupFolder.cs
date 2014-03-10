using System;
using System.IO;

namespace backup {
	public class BackupFolder: BackupItem {
		private DirectoryInfo _folder;

		#region "Properties"
		public override string FullName {
			get {
				return _folder.FullName;
			}
		}

		public override string FolderPath {
			get {
				return _folder.FullName;
			}
		}

		public override long Length {
			get {
				return 0L;
			}
		}

		public override DateTime LastWriteTime {
			get {
				return _folder.LastWriteTime;
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
		public BackupFolder(string server, string fullPath):this(server, new DirectoryInfo(fullPath)) {
		}

		public BackupFolder(string server, DirectoryInfo folder):base(server) {
			_folder = folder;
		}
		#endregion

		public override bool DataOrPathIsNew() {
			string dummyMd5 = null;
			bool? dummyReady = false;
			return !DAL.ItemInDb(_server, this, ref dummyMd5, ref _dbDataId, ref dummyReady, out _dbPathId);
		}

		public override void Encrypt() {
		}

	}
}