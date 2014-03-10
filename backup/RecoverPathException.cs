using System;

namespace backup {
	public class RecoverPathException: Exception {

		public string Path {
			get;
			private set;
		}

		public RecoverPathException(string path, string message): base(message) {
			Path = path;
		}
	}
}

