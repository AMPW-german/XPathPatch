
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using XPP.Doc;
using XPP.Patch;
using XPP.Path;

namespace XPP.Tests;

using Path = BaseTest.Path;

public partial class PatchTests
{
  private static IEnumerable<object[]> LoadLogTests() =>
    DataLoader<PatchEntry>.LoadFilter("Patch.xml", e => e.Action != null);

  [TestMethod]
  [DynamicData(nameof(LoadLogTests))]
  public void TestLog(PatchEntry entry, Exception err = null)
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
    var exec = new PatchExecutor(domain);
    exec.StepToEnd();

    var t = new TreeComparer("");
    PatchEntryAction.TreeCompare(t, entry.Action, exec.Root);
    t.Assert();
  }
}

public partial class PatchEntryAction
{
  public static void TreeCompare(
    TreeComparer t, PatchEntryAction expected, PatchAction actual)
  {
    if (expected == null && actual == null)
      return;
    expected ??= new() { Type = ActionType.Invalid };
    var atgt = Path.FromNode(actual?.Target ?? XPNodeRef.Invalid);
    var asrc = Path.FromNode(actual?.Source ?? XPNodeRef.Invalid);
    t.Compare("Type", expected.Type == actual?.Type, expected.Type, actual?.Type);
    if (expected.Target != null)
      t.Compare("Target", atgt.Equals((Path)expected.Target), expected.Target, atgt);
    else
      t.Add($"Target = {atgt}");
    if (expected.Source != null)
      t.Compare("Source", asrc.Equals((Path)expected.Source), expected.Source, asrc);
    if (expected.Pos != PatchPosition.Unset)
      t.Compare("Pos", expected.Pos == actual?.Position, expected.Pos, actual?.Position);
    if (expected.TargetResult != null)
      t.Child("TargetResults", t => CompareResults(t, expected.TargetResult,
        [.. actual?.TargetResult ?? []]), true);
    if (expected.SourceResult != null)
      t.Child("SourceResults", t => CompareResults(t, expected.SourceResult,
        [.. actual?.SourceResult ?? []]), true);

    t.Compare(
      "Children", expected.Children ?? [], actual?.Children ?? [],
      (t, i, e, a) => t.Child($"{i:00}", t => TreeCompare(t, e, a)), true);
  }

  private static void CompareResults(
    TreeComparer t, PatchEntryValue expected, ExecValue[] actual)
  {
    if (expected.Type != XPValueType.NodeSet)
    {
      t.CmpThrow("Length", actual.Length == 1, 1, actual.Length);
      var aval = actual[0];
      t.CmpThrow("Type", expected.Type == aval.Type, expected.Type, aval.Type);
      switch (expected.Type)
      {
        case XPValueType.Bool:
          t.Compare("Bool", expected.Bool == aval.Bool, expected.Bool, aval.Bool);
          break;
        case XPValueType.Number:
          t.Compare(
            "Number", expected.Number == aval.Number, expected.Number, aval.Number);
          break;
        case XPValueType.String:
          t.Compare(
            "String", expected.String == aval.String, expected.String, aval.String);
          break;
        default: throw new InvalidOperationException($"{expected.Type}");
      }
      return;
    }
    var epaths = expected.Nodes.Select(n => (Path)n.Path).ToList();
    var apaths = actual.Select(n => Path.FromNode(n.Node)).ToList();
    t.Compare("Nodes", epaths, apaths);
  }

  private static object Obj(ExecValue val) => val.Type switch
  {
    XPValueType.Bool => val.Bool,
    XPValueType.Number => val.Number,
    XPValueType.String => val.String,
    XPValueType.NodeSet => Path.FromNode(val.Node),
    _ => throw new InvalidOperationException($"{val.Type}"),
  };
}