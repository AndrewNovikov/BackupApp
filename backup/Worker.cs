using System;
using System.Threading;

namespace backup {
	public class Worker {

		public Thread Tr {
			get;
			private set;
		}

		public EventWaitHandle Wh {
			get;
			private set;
		}

		public Worker(Thread thread) {
			Wh = new AutoResetEvent(false);
			Tr = thread;
		}
	}
}

