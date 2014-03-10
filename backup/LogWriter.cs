using System;
using System.IO;

namespace backup {
	public class LogWriter: Worker, IDisposable {
		private static readonly string lineFormat = "{0}| {1}";
		private Lazy<StreamWriter> _writer = new Lazy<StreamWriter>(() => {
			string path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			return new StreamWriter(path, true);
		});

		public LogWriter(): base(Write, 1) {
		}

		public void Dispose() {
			_writer.Value.Close();
			_writer.Value.Dispose();
		}

		public static void Write(string line) {
			_writer.Value.WriteLineAsync(string.Format(lineFormat, DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.f"), line));
		}
	}
}

