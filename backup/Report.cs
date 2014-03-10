using System;

namespace backup {
	public class Report {
		private DateTime startTime;
		private const string timePattern = " {0} {1}";
		private const string sizePattern = " {0} {1}";

		private long totalScannedFilesLength;
		private static readonly object lock_totalScannedFilesLength = new object();
		private long totalScannedFilesCount;
		private static readonly object lock_totalScannedFilesCount = new object();
		private long totalSendFilesCount;
		private static readonly object lock_totalSendFilesCount = new object();
		private long totalSendUnencryptedFilesLength;
		private static readonly object lock_totalSendUnencryptedFilesLength = new object();
		private long totalSendEncryptedFilesLength;
		private static readonly object lock_totalSendEncryptedFilesLength = new object();

		public long ScannedFilesCount {
			get {
				return totalScannedFilesCount;
			}
		}

		public long ScannedFilesLength {
			get {
				return totalScannedFilesLength;
			}
		}

		public string ScannedFilesLengthString {
			get {
				return BytesToHumanString(ScannedFilesLength);
			}
		}

		public long SendFilesCount {
			get {
				return totalSendFilesCount;
			}
		}

		public long SendUnencryptedFilesLength {
			get {
				return totalSendUnencryptedFilesLength;
			}
		}

		public string SendUnencryptedFilesLengthString {
			get {
				return BytesToHumanString(SendUnencryptedFilesLength);
			}
		}

		public long SendEncryptedFilesLength {
			get {
				return totalSendEncryptedFilesLength;
			}
		}

		public string SendEncryptedFilesLengthString {
			get {
				return BytesToHumanString(SendEncryptedFilesLength);
			}
		}

		public Report() {
			startTime = DateTime.Now;
		}

		public void IncrementScannedFilesCount() {
			lock (lock_totalScannedFilesCount) {
				totalScannedFilesCount++;
			}
		}

		public void IncrementScannedFilesLength(long length) {
			lock (lock_totalScannedFilesLength) {
				totalScannedFilesLength += length;
			}
		}

		public void IncrementSendFilesCount() {
			lock (lock_totalSendFilesCount) {
				totalSendFilesCount++;
			}
		}

		public void IncrementSendUnencryptedFilesLength(long length) {
			lock (lock_totalSendUnencryptedFilesLength) {
				totalSendUnencryptedFilesLength += length;
			}
		}

		public void IncrementSendEncryptedFilesLength(long length) {
			lock (lock_totalSendEncryptedFilesLength) {
				totalSendEncryptedFilesLength += length;
			}
		}

		public TimeSpan ElapsedTime {
			get {
				return DateTime.Now - startTime;
			}
		}

		public string ElapsedTimeString {
			get {
				TimeSpan timeLength = this.ElapsedTime;

				string timeReport = string.Empty;
				if (timeLength.Days > 0)
					timeReport = string.Format(timePattern, timeLength.Days, "days");
				if (timeLength.Hours > 0)
					timeReport += string.Format(timePattern, timeLength.Hours, "hours");
				if (timeLength.Minutes > 0)
					timeReport += string.Format(timePattern, timeLength.Minutes, "minutes");
				if (timeLength.Seconds > 0)
					timeReport += string.Format(timePattern, timeLength.Seconds, "seconds");

				return timeReport.TrimStart();
			}
		}

		private const long TbBytes = 1099511627776L;
		private const long GbBytes = 1073741824;
		private const long MbBytes = 1048576;
		private const long KbBytes = 1024;
		private static string BytesToHumanString(long value) {
			string result = string.Empty;
			
			long cur = value;
			if (cur >= TbBytes) {
				long tb = (int)(cur / TbBytes);
				result += string.Format(sizePattern, tb, "TB");
				cur -= tb * TbBytes;
			}
			if (cur >= GbBytes) {
				long gb = (int)(cur / GbBytes);
				result += string.Format(sizePattern, gb, "GB");
				cur -= gb * GbBytes;
			}
			if (cur >= MbBytes) {
				long mb = (int)(cur / MbBytes);
				result += string.Format(sizePattern, mb, "MB");
				cur -= mb * MbBytes;
			}
			if (cur >= KbBytes) {
				long kb = (int)(cur / KbBytes);
				result += string.Format(sizePattern, kb, "kB");
				cur -= kb * KbBytes;
			}
			result += string.Format(sizePattern, cur, "bytes");
			
			return result.TrimStart();
		}

	}
}

