using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace backup {
  public class Tuple<T1, T2> {
    private readonly T1 m_Item1;
    private readonly T2 m_Item2;

    public T1 Item1 {
      get {
        return this.m_Item1;
      }
    }

    public T2 Item2 {
      get {
        return this.m_Item2;
      }
    }

    public Tuple(T1 item1, T2 item2) {
      this.m_Item1 = item1;
      this.m_Item2 = item2;
    }

    public override bool Equals(object obj) {
      Tuple<T1, T2> tuple = obj as Tuple<T1, T2>;
      if (tuple == null) {
        return false;
      }
      return ((this.m_Item1 == null && tuple.m_Item1 == null) || (this.m_Item1.Equals(tuple.m_Item1))) &&
        ((this.m_Item2 == null && tuple.m_Item2 == null) || (this.m_Item2.Equals(tuple.m_Item2)));
    }

    public override int GetHashCode() {
      return (this.m_Item1 == null ? 0 : this.m_Item1.GetHashCode()) ^ (this.m_Item2 == null ? 0 : this.m_Item2.GetHashCode());
    }
  }
}
