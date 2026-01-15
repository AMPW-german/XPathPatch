
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

  public static IEnumerable<object[]> Load(string fname)
  {
    var path = Path.Join(
      typeof(DataLoader<T>).Assembly.Location, "../TestData", fname);
    Console.WriteLine(path);
    try
    {
      using var f = File.OpenRead(path);

      return ((TestData<T>)Serializer.Deserialize(f)).Entries
        .Select(t => new object[] {t});
    }
    catch (Exception ex)
    {
      return [[null, ex.ToString()]];
    }
  }
}

[XmlRoot("TestData")]
public class TestData<T>
{
  [XmlElement("Entry")]
  public List<T> Entries = [];
}