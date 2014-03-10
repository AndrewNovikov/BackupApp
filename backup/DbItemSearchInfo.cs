using System;

namespace backup {
	//[Serializable]
	public class DbItemSearchInfo {

		public string Path {
			get;
			private set;
		}

		public DateTime MTime {
			get;
			private set;
		}

		public long Size {
			get;
			private set;
		}

		public DbItemSearchInfo(string path, DateTime mTime, long size) {
			this.Path = path;
			this.MTime = mTime;
			this.Size = size;
		}

		public override bool Equals(object obj) {
			DbItemSearchInfo _obj = obj as DbItemSearchInfo;
			if (_obj == null) return false;
			return this.Path == _obj.Path && this.MTime == _obj.MTime && this.Size == _obj.Size;
		}

		public override int GetHashCode() {
			return Path.GetHashCode() ^ MTime.GetHashCode() ^ Size.GetHashCode();
		}

	}
}

