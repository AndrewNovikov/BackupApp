using System;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;

namespace backup {
	public class ApplSettings: ConfigurationSection {

		[ConfigurationProperty("OnlyOneInstanceOnServer", IsRequired = true)]
		public bool OnlyOneInstanceOnServer {
			get {
				return (bool)this["OnlyOneInstanceOnServer"];
			}
			set {
				this["OnlyOneInstanceOnServer"] = value;
			}
		}

		[ConfigurationProperty("DbConnectionString", IsRequired = true)]
		public string DbConnectionString {
			get {
				return (string)this["DbConnectionString"];
			}
			set {
				this["DbConnectionString"] = value;
			}
		}

		[ConfigurationProperty("BackupDb", DefaultValue = false)]
		public bool BackupDb {
			get {
				return (bool)this["BackupDb"];
			}
			set {
				this["BackupDb"] = value;
			}
		}

		[ConfigurationProperty("Filters")]
		public FilterCollection Filters {
			get {
				return (FilterCollection)this["Filters"];
			}
			set {
				this["Filters"] = value;
			}
		}

		[ConfigurationProperty("LocalBackup")]
		public LocalBackupSettings LocalBackup {
			get {
				return (LocalBackupSettings)this["LocalBackup"];
			}
			set {
				this["LocalBackup"] = value;
			}
		}
		/*Filter _filter;
		public Filter Filter {
			get {
				if (_filter == null) {
					_filter = new Filter();
					_filter.Set(FilterStr);
				}
				return _filter;
			}
		}

		[ConfigurationProperty("Filter", DefaultValue="*")]
		public string FilterStr {
			get {
				return (string)this["Filter"];
			}
			set {
				this["Filter"] = value;
			}
		}*/

		/*[ConfigurationProperty("rootPaths")]
		public string RootPaths {
			get {
				return (string)this["rootPaths"];
			}
			set {
				this["rootPaths"] = value;
			}
		}*/

		[ConfigurationProperty("RootPaths", IsRequired = true)]
		public RootPathCollection RootPaths {
			get {
				return (RootPathCollection)this["RootPaths"];
			}
			set {
				this["RootPaths"] = value;
			}
		}

		[ConfigurationProperty("FtpEncryptor")]
		public FtpEncryptorSettings FtpEncryptor {
			get {
				return (FtpEncryptorSettings)this["FtpEncryptor"];
			}
			set {
				this["FtpEncryptor"] = value;
			}
		}

	}

	public class FtpEncryptorSettings : ConfigurationElement {

		/*[ConfigurationProperty("rootPaths")]
		public string RootPaths {
			get {
				return (string)this["rootPaths"];
			}
			set {
				this["rootPaths"] = value;
			}
		}*/

		/*public FtpEncryptorSettings() {
			Properties.Add(new ConfigurationProperty(
				"EncryptionThreadsCount",
				typeof(uint),
				1U,
				null,
				new StringValidator(1), ConfigurationPropertyOptions.IsRequired));
		}*/

		//[ConfigurationProperty("RootPaths", IsRequired = true)]
		//public RootPathCollection RootPaths {
		//  get {
		//    return (RootPathCollection)this["RootPaths"];
		//  }
		//  set {
		//    this["RootPaths"] = value;
		//  }
		//}

		[ConfigurationProperty("password")]
		public string Password {
			get {
				return (string)this["password"];
			}
			set {
				this["password"] = value;
			}
		}

		[ConfigurationProperty("targetFtp", IsRequired = true)]
		public string TargetFtp {
			get {
				return (string)this["targetFtp"];
			}
			set {
				this["targetFtp"] = value;
			}
		}

		[ConfigurationProperty("ftpUser", IsRequired = true)]
		public string FtpUser {
			get {
				return (string)this["ftpUser"];
			}
			set {
				this["ftpUser"] = value;
			}
		}

		[ConfigurationProperty("ftpPassword", IsRequired = true)]
		public string FtpPassword {
			get {
				return (string)this["ftpPassword"];
			}
			set {
				this["ftpPassword"] = value;
			}
		}

		[ConfigurationProperty("ftpMode", DefaultValue = "passive")]
		public string FtpMode {
			get {
				return (string)this["ftpMode"];
			}
			set {
				this["ftpMode"] = value;
			}
		}

		[ConfigurationProperty("EncryptionThreadsCount", IsRequired = false)]
		public uint EncryptionThreadsCount {
			get {
				//int result = (int)this["EncryptionThreadsCount"];
				//if (result < 0) throw new ApplicationException("Encryption threads cound can not be less than zero");
				//return result;
				return (uint)this["EncryptionThreadsCount"];
			}
			set {
				this["EncryptionThreadsCount"] = value;
			}
		}

		[ConfigurationProperty("SaveScans", DefaultValue = 10)]
		public int SaveScans {
			get {
				return (int)this["SaveScans"];
			}
			set {
				this["SaveScans"] = value;
			}
		}

	}

	public class FilterCollection : ConfigurationElementCollection {

		public bool Exclude(string path, out string patternMatched) {
			IEnumerator en = this.GetEnumerator();
			patternMatched = null;
			bool result = false;
			while (!result && en.MoveNext()) {
				result = ((FilterSettings)en.Current).Engine.Exclude(path, out patternMatched);
			}
			return result;
		}

		protected override string ElementName {
			get {
				return "Filter";
			}
		}

		protected override ConfigurationElement CreateNewElement() {
			return new FilterSettings();
		}

		protected override object GetElementKey(ConfigurationElement element) {
			return ((FilterSettings)element).Engine.GetHashCode();
		}

		public override ConfigurationElementCollectionType CollectionType {
			get {
				return ConfigurationElementCollectionType.BasicMap;
			}
		}
	}

	public class FilterSettings : ConfigurationElement {

		[ConfigurationProperty("value", IsRequired = true)]
		public string Value {
			get {
				return (string)this["value"];
			}
			set {
				this["value"] = value;
			}
		}

		private Filter _engine;
		public Filter Engine {
			get {
				if (_engine == null) {
					_engine = new Filter();
					_engine.Set(this.Value);
				}
				return _engine;
			}
		}

	}

	public class LocalBackupSettings: ConfigurationElement {

		//[ConfigurationProperty("Md5Location", IsRequired = true)]
		//public string Md5Location {
		//  get {
		//    return (string)this["Md5Location"];
		//  }
		//  set {
		//    this["Md5Location"] = value;
		//  }
		//}
		[ConfigurationProperty("SingleTarget", IsRequired = false)]
		public bool SingleTarget {
			get {
				//int result = (int)this["EncryptionThreadsCount"];
				//if (result < 0) throw new ApplicationException("Encryption threads cound can not be less than zero");
				//return result;
				return (bool)this["SingleTarget"];
			}
			set {
				this["SingleTarget"] = value;
			}
		}

		[ConfigurationProperty("ThreadsCount", IsRequired = false)]
		public uint ThreadsCount {
			get {
				//int result = (int)this["EncryptionThreadsCount"];
				//if (result < 0) throw new ApplicationException("Encryption threads cound can not be less than zero");
				//return result;
				return (uint)this["ThreadsCount"];
			}
			set {
				this["ThreadsCount"] = value;
			}
		}

		[ConfigurationProperty("BackupLocation", IsRequired = true)]
		public string BackupLocation {
			get {
				return (string)this["BackupLocation"];
			}
			set {
				this["BackupLocation"] = value;
			}
		}

		[ConfigurationProperty("LinkerService", IsRequired = true)]
		public string LinkerService {
			get {
				return (string)this["LinkerService"];
			}
			set {
				this["LinkerService"] = value;
			}
		}

	}

	public class RootPathCollection : ConfigurationElementCollection, IEnumerable<string> {

		protected override string ElementName {
			get {
				return "RootPath";
			}
		}
		
		protected override ConfigurationElement CreateNewElement() {
			return new RootPathSettings();
		}
		
		protected override object GetElementKey(ConfigurationElement element) {
			return ((RootPathSettings)element).GetHashCode();
		}
		
		public override ConfigurationElementCollectionType CollectionType {
			get {
				return ConfigurationElementCollectionType.BasicMap;
			}
		}

		public new IEnumerator<string> GetEnumerator() {
			IEnumerator en = base.GetEnumerator();
			while(en.MoveNext()) {
				yield return ((RootPathSettings)en.Current).Value;
			}
		}
	}

	public class RootPathSettings : ConfigurationElement {

		[ConfigurationProperty("value", IsRequired = true)]
		public string Value {
			get {
				return (string)this["value"];
			}
			set {
				this["value"] = value;
			}
		}

		//[ConfigurationProperty("alias", IsRequired = true)]
		//public string Alias {
		//  get {
		//    return (string)this["alias"];
		//  }
		//  set {
		//    this["alias"] = value;
		//  }
		//}

		public override int GetHashCode() {
			string value = this["value"] as string;
			return value == null ? 0 : value.GetHashCode();
		}

		/*public static explicit operator string(RootPathSettings rootPath) {
			return rootPath.Value;
		}

		public static explicit operator RootPathSettings(string rootPath) {
			return new RootPathSettings() { Value = rootPath };
		}*/
	}

	/*public class FtpEncryptorSettingsCollection : ConfigurationElementCollection {

		protected override string ElementName {
			get {
				return "FtpEncryptor";
			}
		}

	}*/
}

