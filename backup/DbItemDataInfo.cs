using System;

namespace backup {
	//[Serializable]
	public class DbItemDataInfo {

		public string Md5 {
			get;
			private set;
		}

		public long DataId {
			get;
			private set;
		}

		public long PathId {
			get;
			private set;
		}

		public bool Ready {
			get;
			private set;
		}

		public DbItemDataInfo(string md5, long dataId, long pathId, bool ready) {
			this.Md5 = md5;
			this.DataId = dataId;
			this.PathId = pathId;
			this.Ready = ready;
		}
	}
}

