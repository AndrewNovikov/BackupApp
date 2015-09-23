using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace backup.PInvoke {

	internal static class PInvokeHelper	{
		[DllImport("kernel32.dll")]
		internal static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, SymbolicLinkType dwFlags);

		// http://zetalongpaths.codeplex.com/Thread/View.aspx?ThreadId=230652&ANCHOR#Post557779
		internal const int MAX_PATH = 250;
		
		// http://msdn.microsoft.com/en-us/library/ms681382(VS.85).aspx
		internal const int ERROR_SUCCESS = 0;
		internal static readonly IntPtr ERROR_FILE_NOT_FOUND = new IntPtr(2);
		internal const int ERROR_NO_MORE_FILES = 18;
		
		// http://www.dotnet247.com/247reference/msgs/21/108780.aspx
		[DllImportAttribute(@"advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern int GetNamedSecurityInfo(
			string pObjectName,
			int objectType,
			int securityInfo,
			out IntPtr ppsidOwner,
			out IntPtr ppsidGroup,
			out IntPtr ppDacl,
			out IntPtr ppSacl,
			out IntPtr ppSecurityDescriptor);
		
		[DllImport(@"advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern int LookupAccountSid(
			string systemName,
			IntPtr psid,
			StringBuilder accountName,
			ref int cbAccount,
			[Out] StringBuilder domainName,
			ref int cbDomainName,
			out int use);
		
		public const int OwnerSecurityInformation = 1;
		public const int SeFileObject = 1;
		
		[StructLayout(LayoutKind.Sequential)]
		internal struct FILETIME
		{
			internal uint dwLowDateTime;
			internal uint dwHighDateTime;
		}
		
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct WIN32_FIND_DATA
		{
			internal FileAttributes dwFileAttributes;
			internal FILETIME ftCreationTime;
			internal FILETIME ftLastAccessTime;
			internal FILETIME ftLastWriteTime;
			internal int nFileSizeHigh;
			internal int nFileSizeLow;
			internal int dwReserved0;
			internal int dwReserved1;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			internal string cFileName;
			// not using this
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
			internal string cAlternate;
		}
		
		
		[StructLayout(LayoutKind.Sequential)]
		public struct SECURITY_ATTRIBUTES
		{
			public int nLength;
			public IntPtr lpSecurityDescriptor;
			public int bInheritHandle;
		}
		
		//[DllImport(@"kernel32.dll", CharSet = CharSet.Unicode)]
		//[return: MarshalAs(UnmanagedType.Bool)]
		//internal static extern bool CopyFile(string lpExistingFileName, string lpNewFileName, bool bFailIfExists);
		
		[DllImport(@"kernel32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CopyFile(
			[MarshalAs(UnmanagedType.LPTStr)] string lpExistingFileName,
			[MarshalAs(UnmanagedType.LPTStr)] string lpNewFileName,
			[MarshalAs(UnmanagedType.Bool)] bool bFailIfExists);
		
		[DllImport(@"kernel32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool MoveFile(
			[MarshalAs(UnmanagedType.LPTStr)] string lpExistingFileName,
			[MarshalAs(UnmanagedType.LPTStr)] string lpNewFileName);
		
		[DllImport(@"kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool CreateDirectory(
			[MarshalAs(UnmanagedType.LPTStr)]string lpPathName,
			IntPtr lpSecurityAttributes);
		
		[DllImport(@"kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern uint GetFileAttributes(
			[MarshalAs(UnmanagedType.LPTStr)]string lpFileName);
		
		[DllImport(@"kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, BestFitMapping = false)]
		internal static extern bool GetFileAttributesEx(
			[MarshalAs(UnmanagedType.LPTStr)]string lpFileName,
			int fInfoLevelId,
			ref WIN32_FILE_ATTRIBUTE_DATA fileData);
		
		[StructLayout(LayoutKind.Sequential)]
		public struct WIN32_FILE_ATTRIBUTE_DATA
		{
			public int dwFileAttributes;
			public FILETIME ftCreationTime;
			public FILETIME ftLastAccessTime;
			public FILETIME ftLastWriteTime;
			public uint nFileSizeHigh;
			public uint nFileSizeLow;
		}
		
		[DllImport(@"kernel32.dll", CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool SetFileAttributes(
			[MarshalAs(UnmanagedType.LPTStr)]string lpFileName,
			[MarshalAs(UnmanagedType.U4)] FileAttributes dwFileAttributes);
		
		[DllImport(@"kernel32.dll", CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool RemoveDirectory(
			[MarshalAs(UnmanagedType.LPTStr)]string lpPathName);
		
		[DllImport(@"kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool DeleteFile(
			[MarshalAs(UnmanagedType.LPTStr)]string lpFileName);
		
		[DllImport(@"kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern IntPtr FindFirstFile(
			[MarshalAs(UnmanagedType.LPTStr)]string lpFileName,
			out WIN32_FIND_DATA lpFindFileData);
		
		[DllImport(@"kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern bool FindNextFile(
			IntPtr hFindFile,
			out WIN32_FIND_DATA lpFindFileData);
		
		[DllImport(@"kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool FindClose(
			IntPtr hFindFile);
		
		[DllImport(@"kernel32.dll", SetLastError = true, EntryPoint = @"SetFileTime", ExactSpelling = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool SetFileTime1(
			IntPtr hFile,
			ref long lpCreationTime,
			IntPtr lpLastAccessTime,
			IntPtr lpLastWriteTime);
		
		[DllImport(@"kernel32.dll", SetLastError = true, EntryPoint = @"SetFileTime", ExactSpelling = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool SetFileTime2(
			IntPtr hFile,
			IntPtr lpCreationTime,
			ref long lpLastAccessTime,
			IntPtr lpLastWriteTime);
		
		[DllImport(@"kernel32.dll", SetLastError = true, EntryPoint = @"SetFileTime", ExactSpelling = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool SetFileTime3(
			IntPtr hFile,
			IntPtr lpCreationTime,
			IntPtr lpLastAccessTime,
			ref long lpLastWriteTime);
		
		[DllImport(@"kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern int GetFullPathName(
			[MarshalAs(UnmanagedType.LPTStr)]string lpFileName,
			int nBufferLength,
			/*[MarshalAs(UnmanagedType.LPTStr), Out]*/StringBuilder lpBuffer,
			IntPtr mustBeZero);
		//internal static extern uint GetFullPathName(
		//    string lpFileName,
		//    uint nBufferLength,
		//    [Out] StringBuilder lpBuffer,
		//    out StringBuilder lpFilePart);
		
		//internal static int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
		internal static uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;
		
		internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
		
		// Assume dirName passed in is already prefixed with \\?\
		/*public static List<string> FindFilesAndDirectories(
			string directoryPath)
		{
			var results = new List<string>();
			WIN32_FIND_DATA findData;
			var findHandle = FindFirstFile(directoryPath.TrimEnd('\\') + @"\*", out findData);
			
			if (findHandle != INVALID_HANDLE_VALUE)
			{
				bool found;
				do
				{
					var currentFileName = findData.cFileName;
					
					// if this is a directory, find its contents
					if (((int)findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
					{
						if (currentFileName != @"." && currentFileName != @"..")
						{
							var childResults = FindFilesAndDirectories(Path.Combine(directoryPath, currentFileName));
							// add children and self to results
							results.AddRange(childResults);
							results.Add(Path.Combine(directoryPath, currentFileName));
						}
					}
					
					// it's a file; add it to the results
					else
					{
						results.Add(Path.Combine(directoryPath, currentFileName));
					}
					
					// find next
					found = FindNextFile(findHandle, out findData);
				}
				while (found);
			}
			
			// close the find handle
			FindClose(findHandle);
			return results;
		}*/
		
		[DllImport(@"kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern SafeFileHandle CreateFile(
			[MarshalAs(UnmanagedType.LPTStr)]string lpFileName,
			FileAccess dwDesiredAccess,
			FileShare dwShareMode,
			IntPtr lpSecurityAttributes,
			CreationDisposition dwCreationDisposition,
			FileAttributes dwFlagsAndAttributes,
			IntPtr hTemplateFile);

		[DllImport("kernel32.dll")]
		internal static extern uint GetLastError();
	}

	public enum SymbolicLinkType {
		File = 0,
		Directory = 1
	}

	[Flags]
	public enum FileAccess : uint
	{
		GenericRead = 0x80000000,
		GenericWrite = 0x40000000,
		GenericExecute = 0x20000000,
		GenericAll = 0x10000000,
	}

	/*[Flags]
	public enum FileAttributeConstants : int
	{
		//The handle that identifies a directory.
		FILE_ATTRIBUTE_DIRECTORY = 0x00000010,
		//A file or directory that is an archive file or directory. 
		//Applications typically use this attribute to mark files for backup or removal . 
		FILE_ATTRIBUTE_ARCHIVE = 0x00000020,
		//A file or directory that is compressed.
		//For a file, all of the data in the file is compressed. 
		//For a directory, compression is the default for newly created files and subdirectories.
		FILE_ATTRIBUTE_COMPRESSED = 0x00000800,
		//This value is reserved for system use.
		FILE_ATTRIBUTE_DEVICE = 0x00000040, 
		//A file or directory that is encrypted. 
		//For a file, all data streams in the file are encrypted. 
		//For a directory, encryption is the default for newly created files and subdirectories.
		FILE_ATTRIBUTE_ENCRYPTED = 0x00004000,
		//The file or directory is hidden. It is not included in an ordinary directory listing.
		FILE_ATTRIBUTE_HIDDEN = 0x00000002,
	}*/

	[Flags]
	public enum FileShare : uint {
		None = 0x00000000,
		Read = 0x00000001,
		Write = 0x00000002,
		Delete = 0x00000004,
	}
	
	public enum CreationDisposition : uint
	{
		New = 1,
		CreateAlways = 2,
		OpenExisting = 3,
		OpenAlways = 4,
		TruncateExisting = 5,
	}
	
	[Flags]
	public enum FileAttributes : uint
	{
		Readonly = 0x00000001,
		Hidden = 0x00000002,
		System = 0x00000004,
		Directory = 0x00000010,
		Archive = 0x00000020,
		Device = 0x00000040,
		Normal = 0x00000080,
		Temporary = 0x00000100,
		SparseFile = 0x00000200,
		ReparsePoint = 0x00000400,
		Compressed = 0x00000800,
		Offline = 0x00001000,
		NotContentIndexed = 0x00002000,
		Encrypted = 0x00004000,
		Write_Through = 0x80000000,
		Overlapped = 0x40000000,
		NoBuffering = 0x20000000,
		RandomAccess = 0x10000000,
		SequentialScan = 0x08000000,
		DeleteOnClose = 0x04000000,
		BackupSemantics = 0x02000000,
		PosixSemantics = 0x01000000,
		OpenReparsePoint = 0x00200000,
		OpenNoRecall = 0x00100000,
		FirstPipeInstance = 0x00080000
	}
}