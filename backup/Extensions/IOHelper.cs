using System;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace backup {
	internal static class IOHelper {

		public static string ShrinkTo255Bytes(string fileName) {
			int extSeparatorIndex = fileName.LastIndexOf('.');
			string name, ext;
			if (extSeparatorIndex == -1) {
				name = fileName;
				ext = string.Empty;
			} else {
				name = fileName.Substring(0, extSeparatorIndex);
				ext = fileName.Substring(extSeparatorIndex);
			}
			int index = 1;
			while (System.Text.Encoding.UTF8.GetByteCount(name) + System.Text.Encoding.UTF8.GetByteCount(ext) > 253) {
				name = name.Substring(0, name.Length - 1);
			}
			while (IOHelper.FileExists(name + "~" + index)) {
				index++;
				name = name.Substring(0, name.Length - (index - 1));
			}
			return name + "~" + index + ext;
		}

		public static void CreateSymLink(string srcPath, string targetPath, PInvoke.SymbolicLinkType slType) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix) {
				if (slType == PInvoke.SymbolicLinkType.File) {
					Mono.Unix.UnixFileInfo fi = new Mono.Unix.UnixFileInfo(srcPath);
					fi.CreateSymbolicLink(targetPath);
				} else {
					Mono.Unix.UnixDirectoryInfo di = new Mono.Unix.UnixDirectoryInfo(srcPath);
					di.CreateSymbolicLink(targetPath);
				}
			} else {
				PInvoke.PInvokeHelper.CreateSymbolicLink(srcPath, targetPath, slType);
			}
		}

		public static System.IO.FileStream OpenFile(string filePath, System.IO.FileMode fileMode, System.IO.FileAccess fileAccess, System.IO.FileShare fileShare) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || filePath.Length < PInvoke.PInvokeHelper.MAX_PATH) {
				return System.IO.File.Open(filePath, fileMode, fileAccess, fileShare);
			} else {
				string fp = AddLongPathPrefix(filePath).TrimEnd('\\');

				if ((fileShare & System.IO.FileShare.Inheritable) == System.IO.FileShare.Inheritable) throw new NotSupportedException("FileShare.Inheritable does not supported in Win32");
				if ((fileMode & System.IO.FileMode.Append) == System.IO.FileMode.Append) throw new NotSupportedException("FileMode.Append not supported");
				backup.PInvoke.FileAccess fAccess;
				if ((fileAccess & System.IO.FileAccess.ReadWrite) == System.IO.FileAccess.ReadWrite) fAccess = PInvoke.FileAccess.GenericRead | PInvoke.FileAccess.GenericWrite;
				else if ((fileAccess & System.IO.FileAccess.Write) == System.IO.FileAccess.Write) fAccess = PInvoke.FileAccess.GenericWrite;
				else if ((fileAccess & System.IO.FileAccess.Read) == System.IO.FileAccess.Read) fAccess = PInvoke.FileAccess.GenericRead;
				else throw new ArgumentException("File access should be set");
				return new System.IO.FileStream(CreateFileHandle(fp, (backup.PInvoke.CreationDisposition)fileMode, fAccess, (backup.PInvoke.FileShare)fileShare), fileAccess);
			}
		}

		private static SafeFileHandle CreateFileHandle(string filePath, backup.PInvoke.CreationDisposition creationDisposition, backup.PInvoke.FileAccess fileAccess, backup.PInvoke.FileShare fileShare) {
			// Create a file with generic write access
			var fileHandle = PInvoke.PInvokeHelper.CreateFile(filePath, fileAccess, fileShare, IntPtr.Zero, creationDisposition, 0, IntPtr.Zero);

			// Check for errors.
			if (fileHandle.IsInvalid) {
				var lastWin32Error = Marshal.GetLastWin32Error();
				throw new System.ComponentModel.Win32Exception(string.Format("Error {0} creating file handle for file path '{1}'", lastWin32Error, filePath), new System.ComponentModel.Win32Exception(lastWin32Error));
			}

			// Pass the file handle to FileStream. FileStream will close the handle.
			return fileHandle;
		}

		public static DateTime GetFileLastWriteTime(string filePath) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || filePath.Length < PInvoke.PInvokeHelper.MAX_PATH) {
				return System.IO.File.GetLastWriteTime(filePath);
			} else {
				string fp = AddLongPathPrefix(filePath).TrimEnd('\\');

				PInvoke.PInvokeHelper.WIN32_FIND_DATA fd;
				var result = PInvoke.PInvokeHelper.FindFirstFile(fp, out fd);

				try {
					if (result == PInvoke.PInvokeHelper.INVALID_HANDLE_VALUE || result == PInvoke.PInvokeHelper.ERROR_FILE_NOT_FOUND) {
						throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
					} else {
						var ft = fd.ftLastWriteTime;
							
						var hft2 = (((long)ft.dwHighDateTime) << 32) + ft.dwLowDateTime;
						return DateTime.FromFileTime(hft2);
					}
				} finally {
					PInvoke.PInvokeHelper.FindClose(result);
				}
			}
		}

		public static void SetFileLastWriteTime(string filePath, DateTime lastModTime) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || filePath.Length < PInvoke.PInvokeHelper.MAX_PATH) {
				System.IO.File.SetLastWriteTime(filePath, lastModTime);
			} else {
				string fp = AddLongPathPrefix(filePath).TrimEnd('\\');

				using (var handle = CreateFileHandle(fp, backup.PInvoke.CreationDisposition.OpenExisting, backup.PInvoke.FileAccess.GenericAll, backup.PInvoke.FileShare.Read | backup.PInvoke.FileShare.Write)) {
					long lastModTimeFs = lastModTime.ToFileTime();
					
					if (!backup.PInvoke.PInvokeHelper.SetFileTime3(handle.DangerousGetHandle(), IntPtr.Zero, IntPtr.Zero, ref lastModTimeFs)) {
						throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
					}
				}
			}
		}

		public static long GetFileLength(string filePath) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || filePath.Length < PInvoke.PInvokeHelper.MAX_PATH) {
				return new System.IO.FileInfo(filePath).Length;
			} else {
				string fp = AddLongPathPrefix(filePath).TrimEnd('\\');

				PInvoke.PInvokeHelper.WIN32_FIND_DATA fd;
				var result = PInvoke.PInvokeHelper.FindFirstFile(fp, out fd);

				try {
					if (result == PInvoke.PInvokeHelper.INVALID_HANDLE_VALUE || result == PInvoke.PInvokeHelper.ERROR_FILE_NOT_FOUND) {
						throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
					} else {
						var low = (uint)fd.nFileSizeLow;
						var high = (uint)fd.nFileSizeHigh;
						
						return (((long)high) << 32) + low;
					}
				} finally {
					PInvoke.PInvokeHelper.FindClose(result);
				}
			}
		}

		public static System.IO.FileAttributes GetAttributes(string fullPath) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || fullPath.Length < PInvoke.PInvokeHelper.MAX_PATH) {
				return new System.IO.DirectoryInfo(fullPath).Attributes;
			} else {
				string fp = AddLongPathPrefix(fullPath).TrimEnd('\\');
				uint result = PInvoke.PInvokeHelper.GetFileAttributes(fp);
				if (result == PInvoke.PInvokeHelper.INVALID_FILE_ATTRIBUTES) {
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				}
				return (System.IO.FileAttributes)result;
			}
		}

		public static string[] GetFiles(string path, Action<Exception> onError = null) {
			return GetFiles(path, @"*.*", onError);
		}

		public static string[] GetFiles(string path, string pattern, Action<Exception> onError = null) {
			return GetFiles(path, pattern, System.IO.SearchOption.TopDirectoryOnly, onError);
		}

		public static string[] GetFiles(string path, string pattern, System.IO.SearchOption searchOptions, Action<Exception> onError = null) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || path.Length < PInvoke.PInvokeHelper.MAX_PATH) {
				try {
					return System.IO.Directory.GetFiles(path, pattern, searchOptions);
				} catch (Exception exc) {
					if (onError == null) throw exc;
					onError(exc);
					return null;
				}
			} else {
				List<string> result = GetWin32DiskItems(path, pattern, true, onError);
			
				if (searchOptions == System.IO.SearchOption.AllDirectories) {
					foreach (string dir in GetDirectories(path, pattern, searchOptions, onError)) {
						result.AddRange(GetFiles(dir, pattern, searchOptions, onError));
					}
				}
			
				return result.ToArray();
			}
		}

		public static string[] GetDirectories(string path, Action<Exception> onError = null) {
			return GetDirectories(path, @"*", onError);
		}

		public static string[] GetDirectories(string path, string pattern, Action<Exception> onError = null) {
			return GetDirectories(path, pattern, System.IO.SearchOption.TopDirectoryOnly, onError);
		}

		public static string[] GetDirectories(string path, string pattern, System.IO.SearchOption searchOptions, Action<Exception> onError = null) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || path.Length < PInvoke.PInvokeHelper.MAX_PATH) {
				try {
					return System.IO.Directory.GetDirectories(path, pattern, searchOptions);
				} catch (Exception exc) {
					if (onError == null) throw exc;
					onError(exc);
					return null;
				}
			} else {
				List<string> result = GetWin32DiskItems(path, pattern, false, onError);

				if (searchOptions == System.IO.SearchOption.AllDirectories) {
					foreach (string dir in result) {
						result.AddRange(GetDirectories(dir, pattern, searchOptions, onError));
					}
				}

				return result.ToArray();
			}
		}

		public static bool FileExists(string filePath) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || filePath.Length < PInvoke.PInvokeHelper.MAX_PATH) {
				return System.IO.File.Exists(filePath);
			} else {
				filePath = AddLongPathPrefix(filePath);

				var wIn32FileAttributeData = default(PInvoke.PInvokeHelper.WIN32_FILE_ATTRIBUTE_DATA);

				var b = PInvoke.PInvokeHelper.GetFileAttributesEx(filePath, 0, ref wIn32FileAttributeData);
				return b && wIn32FileAttributeData.dwFileAttributes != -1 && (wIn32FileAttributeData.dwFileAttributes & 16) == 0;
			}
		}

		public static bool DirectoryExists(string directoryPath) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || directoryPath.Length < PInvoke.PInvokeHelper.MAX_PATH) {
				return System.IO.Directory.Exists(directoryPath);
			} else {
				directoryPath = AddLongPathPrefix(directoryPath);

				var wIn32FileAttributeData = default(PInvoke.PInvokeHelper.WIN32_FILE_ATTRIBUTE_DATA);

				var b = PInvoke.PInvokeHelper.GetFileAttributesEx(directoryPath, 0, ref wIn32FileAttributeData);
				return b && wIn32FileAttributeData.dwFileAttributes != -1 && (wIn32FileAttributeData.dwFileAttributes & 16) != 0;
			}
		}

		public static void DeleteFile(string filePath) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || filePath.Length < PInvoke.PInvokeHelper.MAX_PATH) {
				System.IO.File.Delete(filePath);
			} else {
				filePath = AddLongPathPrefix(filePath);

				if (!PInvoke.PInvokeHelper.DeleteFile(filePath)) {
					// http://msdn.microsoft.com/en-us/library/ms681382(VS.85).aspx.

					var lastWin32Error = Marshal.GetLastWin32Error();
					if (lastWin32Error != PInvoke.PInvokeHelper.ERROR_NO_MORE_FILES && lastWin32Error != PInvoke.PInvokeHelper.ERROR_FILE_NOT_FOUND.ToInt32()) {
						// Sometimes it returns "ERROR_SUCCESS" and stil deletes the file.
						if (lastWin32Error != PInvoke.PInvokeHelper.ERROR_SUCCESS || FileExists(filePath)) {
							throw new System.ComponentModel.Win32Exception(lastWin32Error);
						}
					}
				}
			}
		}

		public static void DeleteDirectory(string folderPath, bool recursive) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || folderPath.Length < PInvoke.PInvokeHelper.MAX_PATH) {
				System.IO.Directory.Delete(folderPath, recursive);
			} else {
				folderPath = AddLongPathPrefix(folderPath);

				if (DirectoryExists(folderPath)) {
					if (recursive) {
						var files = GetFiles(folderPath);
						var dirs = GetDirectories(folderPath);

						foreach (var file in files) {
							DeleteFile(file);
						}

						foreach (var dir in dirs) {
							DeleteDirectory(dir, true);
						}
					}

					if (!PInvoke.PInvokeHelper.RemoveDirectory(folderPath)) {
						// http://msdn.microsoft.com/en-us/library/ms681382(VS.85).aspx.
						var lastWin32Error = Marshal.GetLastWin32Error();
						if (lastWin32Error != PInvoke.PInvokeHelper.ERROR_NO_MORE_FILES && lastWin32Error != PInvoke.PInvokeHelper.ERROR_SUCCESS) {
							throw new System.ComponentModel.Win32Exception(lastWin32Error);
						}
					}
				}
			}
		}

		/// <summary>
		/// Gets the win32 disk items (files or directories).
		/// </summary>
		/// <returns>
		/// Files or Directories.
		/// </returns>
		/// <param name='path'>
		/// Path.
		/// </param>
		/// <param name='pattern'>
		/// Search pattern (*.*).
		/// </param>
		/// <param name='getFiles'>
		/// If set to <c>true</c> get files. Else get directories
		/// </param>
		private static List<string> GetWin32DiskItems(string path, string pattern, bool onlyFiles, Action<Exception> onError = null) {
			string fp = AddLongPathPrefix(path).TrimEnd('\\');
			List<string> results = new List<string>();
			PInvoke.PInvokeHelper.WIN32_FIND_DATA findData;
			var findHandle = PInvoke.PInvokeHelper.FindFirstFile(fp + @"\" + pattern, out findData);

			if (findHandle != PInvoke.PInvokeHelper.INVALID_HANDLE_VALUE) {
				try {
					bool found;
					do {
						var currentFileName = findData.cFileName;
						bool currentIsDirectory = (findData.dwFileAttributes & PInvoke.FileAttributes.Directory) == PInvoke.FileAttributes.Directory;

						if (onlyFiles != currentIsDirectory) {
							if (currentFileName != @"." && currentFileName != @"..") {
								results.Add(CombinePath(path, currentFileName));
							}
						}

						// find next
						found = PInvoke.PInvokeHelper.FindNextFile(findHandle, out findData);
					} while (found);
				} finally {
					// close the find handle
					PInvoke.PInvokeHelper.FindClose(findHandle);
				}
			} else {
				if (onError != null) {
					onError(new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
				} else {
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				}
			}

			return results;
		}

		public static string AddLongPathPrefix(string path) {
			if (string.IsNullOrEmpty(path) || path.StartsWith(@"\\?\") || path.Length <= PInvoke.PInvokeHelper.MAX_PATH) {
				return path;
			} else {
				// http://msdn.microsoft.com/en-us/library/aa365247.aspx
				
				if (path.StartsWith(@"\\")) {
					// UNC.
					return @"\\?\UNC\" + path.Substring(2);
				} else {
					return @"\\?\" + path;
				}
			}
		}

		public static string CombinePath(string path1, string path2) {
			if (string.IsNullOrEmpty(path1)) {
				return path2;
			} else if (string.IsNullOrEmpty(path2)) {
				return path1;
			} else {
				return path1.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar + path2.TrimStart(System.IO.Path.DirectorySeparatorChar);
			}
		}

	}
}

