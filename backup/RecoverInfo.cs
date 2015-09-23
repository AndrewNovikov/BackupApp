using NLog;
using System;
using System.IO;
using System.Collections.Generic;

namespace backup {
	public class RecoverInfo {
		private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		List<ItemInfo> _files;

		public string Md5 {
			get;
			private set;
		}

		public IEnumerable<ItemInfo> Files {
			get {
				return _files;
			}
		}

		public RecoverInfo(string md5, string path, DateTime lastModTime): this(md5) {
			this.Add(path, lastModTime);
		}

		public RecoverInfo(string md5) {
			_files = new List<ItemInfo>();
			this.Md5 = md5;
		}

		public void Add(string path, DateTime lastModTime) {
			this.Add(new ItemInfo(path, lastModTime));
		}

		public void Add(ItemInfo file) {
			_files.Add(file);
		}

		public void Recover(IRecoverer recoverer, string targetPath) {
			var filesEnum = this.Files.GetEnumerator();
			if (filesEnum.MoveNext()) {

				string firstTargetPath = IOHelper.CombinePath(targetPath, filesEnum.Current.RelativePath);
				IntRecover(this.Md5, firstTargetPath, filesEnum.Current.LastModTime, () => {
					recoverer.Recover(this.Md5, firstTargetPath);
				});
				
				while (filesEnum.MoveNext()) {
					string nextTargetPath = IOHelper.CombinePath(targetPath, filesEnum.Current.RelativePath);
					IntRecover(this.Md5, nextTargetPath, filesEnum.Current.LastModTime, () => {
						File.Copy(firstTargetPath, nextTargetPath);
					});
					
				}
			}
		}

		private static char[] invalidPathChars = System.IO.Path.GetInvalidPathChars();
		public char[] GetInvalidChars(string path) {
			List<char> result = new List<char>();
			foreach (char ch in invalidPathChars) {
				if (path.IndexOf(ch) != -1) {
					result.Add(ch);
				}
			}
			return result.ToArray();
		}
		
		private void IntRecover(string md5, string path, DateTime mTime, Action recover) {
			char[] invalid = GetInvalidChars(path);
			if (invalid.Length > 0) {
				LOGGER.Error("Path '" + path + "' contains invalid characters '" + invalid.Join(", ") + "'");
				return;
			}

			try {
				if (md5 == Crypto.EmptyMd5Str) {
					if (path[path.Length - 1] != Path.DirectorySeparatorChar) {
						Directory.CreateDirectory(Path.GetDirectoryName(path));
						File.Create(path).Dispose();
						IOHelper.SetFileLastWriteTime(path, mTime);
						LOGGER.Trace("File " + path + " of 0 size. Created.");
					} else {
						Directory.CreateDirectory(path);
					}
				} else {
					Directory.CreateDirectory(Path.GetDirectoryName(path));

					if (File.Exists(path)) {
						File.Delete(path);
					}
					
					//LOGGER.Trace("Recovereing path " + filesEnum.Current.Path + " to " + firstTargetPath + "...");
					//decryptor.Decrypt(md5, path);
					recover();
					IOHelper.SetFileLastWriteTime(path, mTime);
					LOGGER.Trace("File with md5 " + md5 + " to " + path + " recovered.");
				}
			} catch (Exception exc) {
				exc.WriteToLog("Error while decrypting md5 '" + md5 + "' file '" + path + "':");
			}
		}

		public class ItemInfo {

			public string Path {
				get;
				private set;
			}

			public DateTime LastModTime {
				get;
				private set;
			}

			/*public bool IsFile {
				get {
					return this.Path[this.Path.Length - 1] != System.IO.Path.DirectorySeparatorChar;
				}
			}*/

			private string _relativePath;
			public string RelativePath {
				get {
					if (_relativePath == null) {
						int diskLetterIndex = this.Path.IndexOf(':');
						string withoutDiskLetterPath = diskLetterIndex != -1 ? this.Path.Substring(diskLetterIndex + 1) : this.Path;
						_relativePath = Helper.DirectorySeparatorCharToLocal(withoutDiskLetterPath);
					}
					return _relativePath;
				}
			}

			//public DateTime BackupTime {
			//  get;
			//  private set;
			//}

			public ItemInfo(string path, DateTime lastModTime) {
				this.Path = path;
				//this.BackupTime = backupTime;
				this.LastModTime = lastModTime;
			}
		}

	}
}