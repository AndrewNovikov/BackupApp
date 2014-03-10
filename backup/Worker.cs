using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace backup {
	public class Worker {
		private ConcurrentQueue<BackupFileQueued> _files = new ConcurrentQueue<BackupFileQueued>();
		private Action<Guid> _threadJob;
		private Dictionary<Guid, Thread> _threadsOnWork = new Dictionary<Guid, Thread>();
		private object _threadsLock = new object();
		private uint _maxThreadsCount;
		private bool _waitingThreads = false;
		//private bool _working = false;

		public Worker(Action<BackupFile> fileJob, uint maxThreadsCount) {
			if (fileJob == null)
				throw new NullReferenceException();

			_maxThreadsCount = maxThreadsCount;

			var self = this;
			_threadJob = ((threadId) => {

				BackupFileQueued file;
				while (_files.TryDequeue(out file)) {
					try {
						fileJob(file.File);
						file.OnWorkerEnds();
					} catch (Exception exc) {
						exc.WriteToLog("exception on job with file:" + file.File.FullName + ". ");
					}
				}
				lock(_threadsLock) {
					_threadsOnWork.Remove(threadId);
				}
				self.Do(); // check if new files were added

				/*try {
					job(file.File);
					file.OnWorkerEnds();
				} catch (Exception exc) {
					exc.WriteToLog("exception on job with file:" + file.File.FullName + ". ");
				} finally {
					lock (_threadsLock) {
						_threadsOnWork.Remove(threadId);
					}
					self.Do();
				}*/

			});
		}

		public void Add(BackupFile file, Action done) {
			_files.Enqueue(new BackupFileQueued(file, done));
			//if (!_working) {
			//System.Threading.ThreadPool.QueueUserWorkItem((o) => {
			Do();
			//});
			//}
		}

		private void Do() {
			if (_files.Count == 0)
				return;

			Thread newThread = null;
			lock (_threadsLock) {
				if (_threadsOnWork.Count < _maxThreadsCount) {
					Guid threadPlaceInAQueue = Guid.NewGuid();
					newThread = new Thread(() => {
						_threadJob(threadPlaceInAQueue);
					}) { IsBackground = false };
					_threadsOnWork.Add(threadPlaceInAQueue, newThread);
				}
			}
			if (newThread != null) {
				newThread.Start();
				if (_waitingThreads) {
					JoinThread(newThread);
				}
			}

		}

		/*private void Do() {
			Guid threadPlaceInAQueue = Guid.Empty;
			//to ensure not to exceed maxThreadsCount we need to check available place and take a place in one atomic operation
			lock (_threadsLock) {
				if (_threadsOnWork.Count < _maxThreadsCount) {
					threadPlaceInAQueue = Guid.NewGuid();
					_threadsOnWork.Add(threadPlaceInAQueue, null);
				}
			}
			if (threadPlaceInAQueue != Guid.Empty) {
				BackupFileQueued file;
				if (_files.TryDequeue(out file)) {

					Thread newThread = new Thread(() => {
						_job(file, threadPlaceInAQueue);
					});
					newThread.IsBackground = false;
					_threadsOnWork[threadPlaceInAQueue] = newThread;
					newThread.Start();

					//Do();
				} else {
					lock (_threadsLock) {
						if (!_threadsOnWork.Remove(threadPlaceInAQueue))
							throw new ApplicationException("No thread with id " + threadPlaceInAQueue + " in a queue");
					}
				}
			}
		}*/

		public void WaitAll() {
			_waitingThreads = true;
			Thread tr;
			while (TryGetThreadOnWork(out tr)) {
				JoinThread(tr);
			}
		}

		private bool TryGetThreadOnWork(out Thread tr) {
			bool result;
			lock (_threadsLock) {
				var thrEnum = _threadsOnWork.Values.GetEnumerator();
				result = thrEnum.MoveNext();
				if (result) {
					tr = thrEnum.Current;
				} else {
					tr = null;
				}
			}
			return result;
		}

		private void JoinThread(Thread tr) {
			if (!tr.Join(43200000)) { //12 hours timeout
				throw new ApplicationException("Thread not finished in 12 hours");
			}
		}

		/*public void OnDone(Action finish) {
			foreach (Thread tr in _threadsOnWork) {
				if (tr != null && tr.ThreadState == (ThreadState.Background | ThreadState.Running)) {
					if (!tr.Join(43200000)) //12 hours
						throw new ApplicationException("Thread not finished in 12 hours");
				}
			}
			finish();
		}*/

	}
}