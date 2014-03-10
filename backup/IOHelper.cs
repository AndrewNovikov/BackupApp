using System;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace backup {
	internal static class IOHelper {

    public static System.IO.FileStream OpenFile(string filePath, System.IO.FileMode fileMode, System.IO.FileAccess fileAccess, System.IO.FileShare fileShare) {
      if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
      } else if (Environment.OSVersion.Platform == PlatformID.Unix || filePath.Length < 260) {
        return System.IO.File.Open(filePath, fileMode, fileAccess, fileShare);
      } else {
        string fp = @"\\?\" + filePath.TrimEnd('\\');

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
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || filePath.Length < 260) {
				return System.IO.File.GetLastWriteTime(filePath);
			} else {
				string fp = @"\\?\" + filePath.TrimEnd('\\');

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
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || filePath.Length < 260) {
				System.IO.File.SetLastWriteTime(filePath, lastModTime);
			} else {
				string fp = @"\\?\" + filePath.TrimEnd('\\');

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
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || filePath.Length < 260) {
				return new System.IO.FileInfo(filePath).Length;
			} else {
				string fp = @"\\?\" + filePath.TrimEnd('\\');

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
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || fullPath.Length < 260) {
				return new System.IO.DirectoryInfo(fullPath).Attributes;
			} else {
				string fp = @"\\?\" + fullPath.TrimEnd('\\');
				uint result = PInvoke.PInvokeHelper.GetFileAttributes(fp);
				if (result == PInvoke.PInvokeHelper.INVALID_FILE_ATTRIBUTES) {
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				}
				return (System.IO.FileAttributes)result;
			}
		}

    public static string[] GetFiles(string path, string pattern, System.IO.SearchOption searchOptions, Action<Exception> onError) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || path.Length < 260) {
				try {
					return System.IO.Directory.GetFiles(path, pattern, searchOptions);
				} catch (Exception exc) {
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

    public static string[] GetDirectories(string path, string pattern, System.IO.SearchOption searchOptions, Action<Exception> onError) {
			if (Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Xbox) {
				throw new PlatformNotSupportedException();
			} else if (Environment.OSVersion.Platform == PlatformID.Unix || path.Length < 260) {
				try {
					return System.IO.Directory.GetDirectories(path, pattern, searchOptions);
				} catch (Exception exc) {
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
		private static List<string> GetWin32DiskItems(string path, string pattern, bool onlyFiles, Action<Exception> onError) {
			string fp = @"\\?\" + path.TrimEnd('\\');
			List<string> results = new List<string>();
			PInvoke.PInvokeHelper.WIN32_FIND_DATA findData;
			var findHandle = PInvoke.PInvokeHelper.FindFirstFile(fp.TrimEnd('\\') + @"\" + pattern, out findData);

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
      } else if (onError != null) {
        onError(new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
      }

			return results;
		}


		private static string CombinePath(string path1, string path2) {
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

