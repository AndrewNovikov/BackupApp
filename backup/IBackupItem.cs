using System;

namespace backup {

	public interface IBackupItem {
		DataInfo Data {
			get;
		}
		string Md5 {
			get;
		}
		string Name {
			get;
		}
		string RootFolder {
			get;
		}
		string RelativeFolder {
			get;
		}
		string FullName {
			get;
		}
		long Length {
			get;
		}
		DateTime LastModifyTime {
			get;
		}
		string GetShortFileName();
		string GetShortRelativeFolder();
	}

}

