
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using XPP.Doc;
using XPP.Patch;

namespace XPP.Tests;

[TestClass]
public partial class PatchTests : BaseTest
{
  public record class FileCase(string Path, string Xml);
  public record class ModCase(string Id, FileCase[] Files);
  public record class ResultCase(ModCase[] Mods, Path ExpPath, string Expected);

  public static IEnumerable<object[]> LoadResultTests() =>
    DataLoader<PatchEntry>.LoadFilter("Patch.xml", e => e.Expected.Count > 0);

  [TestMethod]
  [DynamicData(nameof(LoadResultTests))]
  public void TestPatchResult(PatchEntry entry)
  {
    var domain = new PatchDomain();
    foreach (var pmod in entry.Mods)
    {
      var mod = domain.AddMod(pmod.Id);
      foreach (var f in pmod.Files)
        mod.Import(f.Path, new XmlNodeReader(f.Content));
    }

    var expDoc = XPDocument.New();
    expDoc.LatestRoot.Import(domain.Doc.LatestRoot.FirstContent);
    foreach (var expected in entry.Expected)
    {
      var preExp = ((Path)expected.Path).Get(expDoc.LatestRoot);

      var subDoc = XPDocument.New();
      subDoc.LatestRoot.Import(expected.Content);
      preExp.Parent.Import(subDoc.LatestRoot.FirstContent, after: preExp);
      preExp.Remove();
    }

    var exec = new PatchExecutor(domain);
    exec.StepToEnd();

    XPNodeEqual(expDoc.LatestRoot.FirstContent, domain.Doc.LatestRoot.FirstContent);
  }
}

public class PatchEntry
{
  [XmlElement("Mod")] public List<PatchEntryMod> Mods;
  [XmlElement("Expected")] public List<PatchEntryExpected> Expected;
}

public class PatchEntryMod
{
  [XmlAttribute("Id")] public string Id;
  [XmlElement("File")] public List<PatchEntryFile> Files;
}

public class PatchEntryFile
{
  [XmlAttribute("Path")] public string Path;
  [XmlAnyElement] public XmlElement Content;
}

public class PatchEntryExpected
{
  [XmlAttribute("Path")] public string Path;
  [XmlAnyElement] public XmlElement Content;
}