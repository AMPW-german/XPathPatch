
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using XPP.Doc;
using XPP.Patch;
using XPP.Path;

namespace XPP.Tests;

[TestClass]
public partial class PatchTests : BaseTest
{
  public static IEnumerable<object[]> LoadResultTests() =>
    DataLoader<PatchEntry>.LoadFilter("Patch.xml", e => e.Expected.Count > 0);

  public static string PatchTestDisplayName(MethodInfo method, object[] args) =>
    $"{method.Name}:{(args[0] as PatchEntry)?.Id ?? "ERROR"}";

  [TestMethod]
  [DynamicData(nameof(LoadResultTests),
    DynamicDataDisplayName = nameof(PatchTestDisplayName))]
  public void TestPatchResult(PatchEntry entry, Exception err = null)
  {
    if (err != null)
      throw err;
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
      if (!preExp.Valid)
        throw new InvalidOperationException($"{expected.Path}");

      if (expected.ElementContent != null)
      {
        var subDoc = XPDocument.New();
        subDoc.LatestRoot.Import(expected.ElementContent);
        preExp.Parent.Import(subDoc.LatestRoot.FirstContent, after: preExp);
        preExp.Remove();
      } else
      {
        var val = expected.StringContent ?? "";
        if(!preExp.Type.HasValue)
          throw new InvalidOperationException(
            $"{preExp.Type} node at {expected.Path} cannot have value");
        preExp.SetValue(val);
      }
    }

    var exec = new PatchExecutor(domain);
    exec.StepToEnd();

    XPNodeEqual(expDoc.LatestRoot.FirstContent, domain.Doc.LatestRoot.FirstContent);
  }
}

public class PatchEntry
{
  [XmlAttribute("Id")] public string Id;
  [XmlElement("Mod")] public List<PatchEntryMod> Mods;
  [XmlElement("Expected")] public List<PatchEntryExpected> Expected;
  [XmlElement("Action")] public PatchEntryAction Action;
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
  [XmlAnyElement] public XmlElement ElementContent;
  [XmlText] public string StringContent;
}

public partial class PatchEntryAction
{
  [XmlAttribute("Type")] public ActionType Type;
  [XmlAttribute("Target")] public string Target;
  [XmlAttribute("Source")] public string Source;
  [XmlAttribute("Pos")] public PatchPosition Pos = PatchPosition.Unset;

  [XmlElement("Target")] public PatchEntryValue TargetResult;
  [XmlElement("Source")] public PatchEntryValue SourceResult;
  [XmlElement("Action")] public List<PatchEntryAction> Children;
}

public class PatchEntryValue
{
  [XmlAttribute("Type")] public XPValueType Type;
  [XmlAttribute("Bool")] public bool Bool;
  [XmlAttribute("Number")] public double Number;
  [XmlAttribute("String")] public string String;
  [XmlElement("Node")] public List<PatchEntryNode> Nodes;
}

public class PatchEntryNode
{
  [XmlAttribute("Path")] public string Path;
}