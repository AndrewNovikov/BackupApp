using NLog;
using System;
using System.Threading;
using System.Collections.Generic;
//using System.Collections.Concurrent;

namespace backup {
	public class BackupFileQueue: IDisposable {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		//private ConcurrentQueue<BackupFileQueued> _files = new ConcurrentQueue<BackupFileQueued>();
		//private ConcurrentQueue<BackupFile> _files = new ConcurrentQueue<BackupFile>();
		private Queue<BackupFile> _files = new Queue<BackupFile>();
		private ThreadStart _threadJob;
		//private Action<BackupFile> _onEnd;
		private readonly Dictionary<int, Worker> _allThreads = new Dictionary<int, Worker>();

		private readonly Dictionary<int, Worker> _threadsOnWork = new Dictionary<int, Worker>();
		private readonly Queue<Worker> _threadsOnSleep = new Queue<Worker>();
		private readonly object _workSleepThreadsLock = new object();

		//private object _threadsLock = new object();
		private bool disposing = false;
		private uint _maxThreadsCount;
		//private bool _waitingThreads = false;
		//private bool _working = false;

		public BackupFileQueue(uint maxThreadsCount) {
			_maxThreadsCount = maxThreadsCount;

			_threadJob = (() => {
				int threadId = Thread.CurrentThread.ManagedThreadId;

				IEncryptor enc = new FtpEncryptor();

				while (!disposing) {
					BackupFile file;
					lock(_files) {
						if (_files.Count > 0) {
							file = _files.Dequeue();
						} else {
							file = null;
						}
					}
					//if (_files.TryDequeue(out file)) {
					if (file != null) {
						try {
							//fileJob(file);
							long encryptedLength = enc.Encrypt(file);
							BackupFile.OnEncryptionEnds(file, encryptedLength);
							/*if (_onEnd != null) {
								_onEnd(file);
							}*/
						} catch (Exception exc) {
							exc.WriteToLog("exception on job with file:" + file.FullName + ". ");
						}
					} else {
						ToSleep(threadId);
					}
				}

				LOGGER.Debug("disposing thread " + threadId);

				IDisposable encd = enc as IDisposable;
				if (encd != null) {
					encd.Dispose();
				}

				lock (_workSleepThreadsLock) {
					_threadsOnWork[threadId].Wh.Close();
					_threadsOnWork.Remove(Thread.CurrentThread.ManagedThreadId);
				}

				/*BackupFile file;
				while (_files.TryDequeue(out file)) {
					try {
						fileJob(file);
						if (_onEnd != null) {
							_onEnd(file);
						}
					} catch (Exception exc) {
						exc.WriteToLog("exception on job with file:" + file.FullName + ". ");
					}
				}
				lock(_threads) {
					_threads.Remove(threadId);
				}*/
				//self.Do(); // check if new files were added

			});
		}

		public void Dispose() {
			WaitAwaikedWorkers();
			disposing = true;
			PushSleepWorkers();
		}

		private void ToSleep(int id) {
			Worker w;
			lock (_workSleepThreadsLock) {
				if (!_threadsOnWork.TryGetValue(id, out w))
					throw new ApplicationException("No worker with id " + id + " is on working now");
				_threadsOnWork.Remove(id);
				_threadsOnSleep.Enqueue(w);
				LOGGER.Debug("threadId " + id + " will sleep");
			}
			w.Wh.WaitOne();
		}

		private bool ToWork() {
			Worker w;
			bool avail;
			lock (_workSleepThreadsLock) {
				avail = _threadsOnSleep.Count > 0;
				if (avail) {
					w = _threadsOnSleep.Dequeue();
					_threadsOnWork.Add(w.Tr.ManagedThreadId, w);
					LOGGER.Debug("threadId " + w.Tr.ManagedThreadId + " will be awaiked");
				} else {
					w = null;
				}
			}
			if (avail) {
				w.Wh.Set();
			}
			return avail;
		}

		public void Add(BackupFile file) {
			if (disposing)
				throw new ApplicationException("Can not add file. Queue is in disposing state now.");
			lock (_files) {
				_files.Enqueue(file);
			}
			Do();
		}

		private void Do() {
			if (_files.Count == 0)
				return;

			Thread newThread = null;
			Worker w = null;
			lock (_allThreads) {
				if (_allThreads.Count < _maxThreadsCount) {

					newThread = new Thread(_threadJob) { IsBackground = false };
					w = new Worker(newThread);
					_allThreads.Add(newThread.ManagedThreadId, w);

					LOGGER.Debug("threadId " + newThread.ManagedThreadId + " created with max " + _maxThreadsCount + " and " + _allThreads.Count + " created");
				}
			}

			if (newThread != null && w != null) {
				lock (_workSleepThreadsLock) {
					_threadsOnWork.Add(newThread.ManagedThreadId, w);
				}
				newThread.Start();
			} else {
				ToWork();
			}
		}

		private void PushSleepWorkers() {
			LOGGER.Debug("Pushing sleeped threads...");
			while (ToWork()) {
			}
		}

		private void WaitAwaikedWorkers() {
			Worker w;
			LOGGER.Debug("Waiting running threads...");
			while (TryGetAwaikedWorker(out w)) {
				LOGGER.Debug("Waiting threadId " + w.Tr.ManagedThreadId + "...");
				int total = 0;
				const int waitMs = 5000;
				const int maxWait = 43200000; //21600000 - 6 hours; //10800000 - 3 hours; //3600000 - 1 hour; //43200000 - 12 hours timeout
				if (w.Tr.IsAlive) {
					LOGGER.Trace("Waiting for thread with id=" + w.Tr.ManagedThreadId + " and state=" + w.Tr.ThreadState + " with max " + maxWait + " ms...");
					bool threadOnWork = true;
					//while (w.Tr.ThreadState == ThreadState.Running !w.Tr.Join(waitMs) && total < maxWait) {
					while (threadOnWork && total < maxWait) {
						Thread.Sleep(waitMs);
						lock (_workSleepThreadsLock) {
							threadOnWork = _threadsOnWork.ContainsKey(w.Tr.ManagedThreadId);
						}
						total += waitMs;
					}
					if (total >= maxWait) {
						w.Tr.Abort();
						LOGGER.Error("Thread not finished in " + maxWait + " milliseconds. Aborted.");
						//throw new ApplicationException("Thread not finished in " + maxWait + " milliseconds");
					}
				} else {
					throw new ApplicationException("Thread id=" + w.Tr.ManagedThreadId + " is in a _threadsOnWork and is not in IsAlive state. ThreadState=" + w.Tr.ThreadState);
				}
			}
			LOGGER.Debug("All running threads are done");
		}

		private bool TryGetAwaikedWorker(out Worker w) {
			bool result;
			lock (_workSleepThreadsLock) {
				var thrEnum = _threadsOnWork.Values.GetEnumerator();
				result = thrEnum.MoveNext();
				if (result) {
					w = thrEnum.Current;
				} else {
					w = null;
				}
			}
			return result;
		}

	}
}