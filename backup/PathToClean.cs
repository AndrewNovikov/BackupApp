using System;

namespace backup {
	public class PathToClean {

		public long DataId {
			get;
			private set;
		}

		public long PathId {
			get;
			private set;
		}

		public string Md5 {
			get;
			private set;
		}

		public PathToClean(long dataId, long pathId, string md5) {
			DataId = dataId;
			PathId = pathId;
			Md5 = md5;
		}
	}
}

