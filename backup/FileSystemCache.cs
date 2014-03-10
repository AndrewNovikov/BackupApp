//using NLog;
using System;
using System.Collections.Generic;
using System.IO;

namespace backup {
	public class FileSystemCache {
		//static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
		Node root;

		public int TotalItems {
			get;
			private set;
		}

		public FileSystemCache() {
			root = new Node();
			TotalItems = 0;
		}

		public void Add(string path, DateTime mTime, long length, string md5, long dataId, long pathId, bool ready) {
			TotalItems++;
			string[] paths = path.Split(Path.DirectorySeparatorChar);
			int curr = 0;
			Node target = root;
			while (curr < paths.Length - 1) {
				string currFolder = paths[curr];
				Node subTarget;
				if (!target.Branches.TryGetValue(currFolder, out subTarget)) {
					subTarget = new Node();
					target.Branches.Add(currFolder, subTarget);
				}
				target = subTarget;
				curr++;
			}
			Leaf leaf = new Leaf(paths[paths.Length - 1], mTime, length);
			LeafDb leafDb = new LeafDb(md5, dataId, pathId, ready);
			target.Leafs.Add(leaf, leafDb);
		}

		public bool TryGet(string path, DateTime mTime, long length, ref string md5, ref long dbDataId, ref bool? ready, out long dbPathId) {
			string[] paths = path.Split(Path.DirectorySeparatorChar);
			int curr = 0;
			Node target = root;
			bool? exist = null;
			while (curr < paths.Length - 1 && !exist.HasValue) {
				string currPath = paths[curr];
				Node subTarget;
				if (target.Branches.TryGetValue(currPath, out subTarget)) {
					target = subTarget;
				} else {
					exist = false;
				}
				curr++;
			}
			if (!exist.HasValue) {
				LeafDb leafDb;
				Leaf leaf = new Leaf(paths[paths.Length - 1], mTime, length);
				if (target.Leafs.TryGetValue(leaf, out leafDb)) {
					md5 = leafDb.Md5;
					dbDataId = leafDb.DataId;
					ready = leafDb.Ready;
					dbPathId = leafDb.PathId;
					return true;
				}
			}
			dbPathId = -1;
			return false;
		}

		private class Node {
			public Dictionary<string, Node> Branches { get; private set; }
			public Dictionary<Leaf, LeafDb> Leafs { get; private set; }

			public Node() {
				Branches = new Dictionary<string, Node>();
				Leafs = new Dictionary<Leaf, LeafDb>();
			}
		}

		private class Leaf {
			public string Name { get; private set; }
			public DateTime ModTime { get; private set; }
			public long Length { get; private set; }

			public Leaf(string name, DateTime mTime, long length) {
				Name = name;
				ModTime = mTime.Truncate(TimeSpan.FromSeconds(1)); //in db datetime contains with a precision of a second. Aaaa, close enough.
				Length = length;
			}

			public override bool Equals(object obj) {
				Leaf _obj = obj as Leaf;
				if (_obj == null) return false;
				return this.Name == _obj.Name && this.ModTime == _obj.ModTime && this.Length == _obj.Length;
			}

			public override int GetHashCode() {
				return Name.GetHashCode() ^ ModTime.GetHashCode() ^ Length.GetHashCode();
			}
		}

		private class LeafDb {
			public string Md5 { get; private set; }
			public long DataId{ get; private set; }
			public long PathId { get; private set; }
			public bool Ready { get; private set; }

			public LeafDb(string md5, long dataId, long pathId, bool ready) {
				Md5 = md5;
				DataId = dataId;
				PathId = pathId;
				Ready = ready;
			}
		}

	}
}

