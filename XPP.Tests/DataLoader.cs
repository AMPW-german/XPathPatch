
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace XPP.Tests;

using Path = System.IO.Path;
public class DataLoader<T>
{
  private static XmlSerializer Serializer => field ??= new(typeof(TestData<T>));

  public static IEnumerable<T> Load(string fname)
  {
    var path = Path.Join(
      typeof(DataLoader<T>).Assembly.Location, "../TestData", fname);

    using var f = File.OpenRead(path);
    return ((TestData<T>)Serializer.Deserialize(f)).Entries;
  }

  public static IEnumerable<object[]> LoadFilter(string fname, Func<T, bool> fn)
  {
    try
    {
      return Load(fname)
        .Where(fn)
        .Select(v => new object[] { v });
    }
    catch (Exception ex)
    {
      return [[null, ex]];
    }
  }
}

[XmlRoot("TestData")]
public class TestData<T>
{
  [XmlElement("Entry")]
  public List<T> Entries = [];
}