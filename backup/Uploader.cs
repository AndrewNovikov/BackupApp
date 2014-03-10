using System;
using System.IO;
using System.Net;
using System.Collections.Generic;

namespace backup {
	public static class Uploader {
		private static Queue<BackupFile> _files = new Queue<BackupFile>();
		private static object _lockObject = new object();
		private static bool _working;

		public static void Add(BackupFile file) {
			lock (_lockObject) {
				_files.Enqueue(file);
			}			
			if (!_working) {
				_working = true;
				System.Threading.ThreadPool.QueueUserWorkItem((o) => {
					Upload();
				});
			}
		}

		private static void Upload() {
			lock (_lockObject) {
				if (_files.Count == 0) {
					_working = false;
					return;
				}
			}

			BackupFile file = _files.Dequeue();
			try {
				FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://parovoz/" + file.Md5 + ".enc");
				request.UsePassive = false;
				request.Method = WebRequestMethods.Ftp.UploadFile;
				
				request.Credentials = new NetworkCredential("andrew", "dakinom8");
				using (Stream requestStream = request.GetRequestStream()) {
					file.WriteEncrypted(requestStream);
					requestStream.Close();
				}
				
				FtpWebResponse response = (FtpWebResponse)request.GetResponse();
				response.Close();
			} catch (Exception exc) {
				exc.WriteToLog("exception on uploading file " + file.FullName + ": ");
			}
			Upload();
		}
	}
}

