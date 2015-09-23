using NLog;
using System;
using System.Threading;
using System.Collections.Generic;
//using System.Collections.Concurrent;

namespace backup {
	public delegate void BackupEventHandler(long resultLength, IBackupItem file);
	public delegate void BackupErrorEventHandler(Exception exc, IBackupItem file);

	public class BackupFileQueue: IDisposable {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		private static readonly Logger QUEUE_LOGGER = LogManager.GetLogger("queue");
		//private ConcurrentQueue<BackupFileQueued> _files = new ConcurrentQueue<BackupFileQueued>();
		//private ConcurrentQueue<BackupFile> _files = new ConcurrentQueue<BackupFile>();
		private Queue<IBackupItem> _files = new Queue<IBackupItem>();
		private ThreadStart _threadJob;
		//private Action<BackupFile> _onEnd;
		private readonly Dictionary<int, Worker> _allThreads = new Dictionary<int, Worker>();

		private readonly Dictionary<int, Worker> _threadsOnWork = new Dictionary<int, Worker>();
		private readonly Queue<Worker> _threadsOnSleep = new Queue<Worker>();
		private readonly object _workSleepThreadsLock = new object();

		//private object _threadsLock = new object();
		private bool disposing = false;
		private uint _maxThreadsCount;
		//private Func<IBackupEngine> _backupEngineConstructor;
		//private bool _waitingThreads = false;
		//private bool _working = false;
		public event BackupEventHandler Success;
		public event BackupErrorEventHandler Failure;

		public BackupFileQueue(uint maxThreadsCount, Func<IBackupEngine> backupEngineConstructor) {
			_maxThreadsCount = maxThreadsCount;
			//_backupEngineConstructor = backupEngineConstructor;

			_threadJob = (() => {
				int threadId = Thread.CurrentThread.ManagedThreadId;

				IBackupEngine enc = backupEngineConstructor();

				while (!disposing) {
					IBackupItem file;
					//Action<long> onSuccess;
					lock(_files) {
						QUEUE_LOGGER.Debug("ThreadId " + threadId + ": " + _files.Count + " files in a queue");
						if (_files.Count > 0) {
							//var fileAct = _files.Dequeue();
							//file = fileAct.Key;
							file = _files.Dequeue();
							//onSuccess = fileAct.Value;
						} else {
							file = null;
							//onSuccess = null;
						}
					}
					if (file != null) {
						try {
							QUEUE_LOGGER.Debug("ThreadId " + threadId + ": File " + file.FullName + " dequeued. Will backup now...");

							long encryptedLength = enc.Backup(file);
							//long encryptedLength = 0;

							QUEUE_LOGGER.Debug("ThreadId " + threadId + ": File " + file.FullName + " backed up, " + encryptedLength + " bytes");

							OnSuccess(encryptedLength, file);

						} catch (Exception exc) {
							//exc.WriteToLog("ThreadId " + threadId + ": Exception on a job with file:" + file.FullName + ". ");
							OnFailure(exc, file);
						}
					} else {
						QUEUE_LOGGER.Debug("ThreadId " + threadId + ": Nothing in a queue.");
						ToSleep(threadId);
					}
				}

				QUEUE_LOGGER.Debug("ThreadId " + threadId + ":  Disposing...");

				IDisposable encd = enc as IDisposable;
				if (encd != null) {
					encd.Dispose();
				}

				lock (_workSleepThreadsLock) {
					_threadsOnWork[threadId].Wh.Close();
					_threadsOnWork.Remove(Thread.CurrentThread.ManagedThreadId);
				}

				QUEUE_LOGGER.Debug("ThreadId " + threadId + ":  Disposed");
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

		private void OnSuccess(long resultLength, IBackupItem file) {
			if (Success != null) {
				Success(resultLength, file);
			}
		}

		private void OnFailure(Exception exc, IBackupItem file) {
			if (Failure != null) {
				Failure(exc, file);
			}
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
				QUEUE_LOGGER.Debug("ThreadId " + id + ": Will sleep");
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
					QUEUE_LOGGER.Debug("ThreadId " + w.Tr.ManagedThreadId + ": Will be awaiked");
				} else {
					w = null;
				}
			}
			if (avail) {
				w.Wh.Set();
			}
			return avail;
		}

		public bool Add(IBackupItem file) {
			if (disposing)
				throw new ApplicationException("Can not add file. Queue is in disposing state now.");
			if (_maxThreadsCount > 0) {
				lock (_files) {
					_files.Enqueue(file);
				}
        QUEUE_LOGGER.Trace("File " + file.FullName + " md5=" + file.Data.MD5 + " is added to a queue");
			} else {
        QUEUE_LOGGER.Trace("File " + file.FullName + " md5=" + file.Data.MD5 + " will not be added to a queue. _maxThreadsCount=" + _maxThreadsCount);
			}
			Do();
			return _maxThreadsCount > 0;
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

					QUEUE_LOGGER.Debug("ThreadId " + newThread.ManagedThreadId + " created with max " + _maxThreadsCount + " and " + _allThreads.Count + " created");
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
			QUEUE_LOGGER.Debug("Pushing sleeped threads...");
			while (ToWork()) {
			}
		}

		private void WaitAwaikedWorkers() {
			Worker w;
			QUEUE_LOGGER.Debug("Waiting running threads...");
			while (TryGetAwaikedWorker(out w)) {
				QUEUE_LOGGER.Debug("Waiting threadId " + w.Tr.ManagedThreadId + "...");
				int total = 0;
				const int waitMs = 5000;
				//const int reCheckMs = 300000;
				const int maxWait = 43200000; //21600000 - 6 hours; //10800000 - 3 hours; //3600000 - 1 hour; //43200000 - 12 hours timeout
				if (w.Tr.IsAlive) {
					int reCheckIn = 0;
					bool threadOnWork = true;
					while (threadOnWork && total < maxWait) {
						reCheckIn -= waitMs;
						if (reCheckIn < 1) {
							QUEUE_LOGGER.Trace("Waiting for thread with id=" + w.Tr.ManagedThreadId + " and state=" + w.Tr.ThreadState + " with max " + maxWait + " ms...");
							reCheckIn = 600000; //10 minutes
						}

						Thread.Sleep(waitMs);

						lock (_workSleepThreadsLock) {
							threadOnWork = _threadsOnWork.ContainsKey(w.Tr.ManagedThreadId);
						}
						total += waitMs;

					}
					if (total >= maxWait) {
						w.Tr.Abort();
						LOGGER.Error("Thread " + w.Tr.ManagedThreadId + " not finished in " + maxWait + " milliseconds. Aborted.");
						//throw new ApplicationException("Thread not finished in " + maxWait + " milliseconds");
					}
				} else {
					throw new ApplicationException("Thread id=" + w.Tr.ManagedThreadId + " is in a _threadsOnWork and is not in IsAlive state. ThreadState=" + w.Tr.ThreadState);
				}
			}
			QUEUE_LOGGER.Debug("All running threads are done");
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


		class Worker {

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
}