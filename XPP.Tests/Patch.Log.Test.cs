
using System;
using System.Collections.Generic;
using System.Linq;
using XPP.Doc;
using XPP.Patch;
using XPP.Path;

namespace XPP.Tests;

public partial class PatchTests
{
  private static IEnumerable<object[]> LogCases => new List<LogCase>()
  {
    new([
      new("Mod1", [
        new("F1.xml", "<A><B C='V2' /></A>"),
        new("P1.xml", "<Patch><Copy Path='Mod/A/B/@C' From='\"v2\"' /></Patch>"),
      ]),
    ], new(ActionType.Root, Children: [
      new(ActionType.OpPatch, Target: "Root/Mod/Patch", Children: [
        new(ActionType.OpCopy, Target: "Root/Mod/Patch/Copy",
            TargetResults: [(Path)"Root/Mod/A/B/@C"], SourceResults: ["v2"], Children: [
          new(ActionType.Set, Target: "Root/Mod/A/B/@C", SourceResults: ["v2"])
        ])
      ])
    ])),
    new([
      new("Mod1", [
        new("F1.xml", "<A><B C='v1' /></A>"),
        new("P1.xml", """
          <Patch>
            <Merge Path='Mod/A' From='Mod/Patch/Merge/A'>
              <A><B D='E'/></A>
            </Merge>
          </Patch>
        """)
      ]),
    ], new ActionCase(ActionType.Root, Children: [
      new(ActionType.OpPatch, Target: "Root/Mod/Patch", Children: [
        new(ActionType.OpMerge, Target: "Root/Mod/Patch/Merge",
          TargetResults: [(Path)"Root/Mod/A"],
          SourceResults: [(Path)"Root/Mod/Patch/Merge/A"], Children:
        [
          new(ActionType.Merge, Target: "Root/Mod/A", Source: "Root/Mod/Patch/Merge/A", Children: [
            new(ActionType.Merge, Target: "Root/Mod/A/B", Source: "Root/Mod/Patch/Merge/A/B", Children: [
              new(ActionType.Insert, Target: "Root/Mod/A/B",
                Source: "Root/Mod/Patch/Merge/A/B/@D", Pos: PatchPosition.Append)
            ])
          ])
        ])
      ])
    ])),
  }.Select(c => new object[] { c });

  public record class LogCase(ModCase[] Mods, ActionCase Action);
  public record class ActionCase(
    ActionType Type,
    Path Target = null, Path Source = null, PatchPosition? Pos = null,
    object[] TargetResults = null, object[] SourceResults = null,
    ActionCase[] Children = null)
  {
    public static void Equals(
      TreeComparer t, ActionCase expected, PatchAction actual)
    {
      if (expected == null && actual == null)
        return;
      expected ??= new(ActionType.Invalid);
      var atgt = Path.FromNode(actual?.Target ?? XPNodeRef.Invalid);
      var asrc = Path.FromNode(actual?.Source ?? XPNodeRef.Invalid);
      t.Compare("Type", expected.Type == actual?.Type, expected.Type, actual?.Type);
      if (expected.Target != null)
        t.Compare("Target", expected.Target.Equals(atgt), expected.Target, atgt);
      else
        t.Add($"Target = {atgt}");
      if (expected.Source != null)
        t.Compare("Source", expected.Source.Equals(asrc), expected.Source, asrc);
      if (expected.Pos != null)
        t.Compare("Pos", expected.Pos == actual?.Position, expected.Pos, actual?.Position);
      if (expected.TargetResults != null)
        t.Child("TargetResults", t => CompareResults(t, expected.TargetResults,
          [.. actual?.TargetResult ?? []]), true);
      if (expected.SourceResults != null)
        t.Child("SourceResults", t => CompareResults(t, expected.SourceResults,
          [.. actual?.SourceResult ?? []]), true);

      var echildren = expected.Children ?? [];
      var achildren = actual?.Children ?? [];
      for (var index = 0; index < echildren.Length || index < achildren.Count; index++)
      {
        var echild = index < echildren.Length ? echildren[index] : null;
        var achild = index < achildren.Count ? achildren[index] : null;
        t.Child($"Child {index}", t => Equals(t, echild, achild), false);
      }
    }

    private static object Obj(ExecValue val) => val.Type switch
    {
      XPValueType.Bool => val.Bool,
      XPValueType.Number => val.Number,
      XPValueType.String => val.String,
      XPValueType.NodeSet => Path.FromNode(val.Node),
      _ => throw new InvalidOperationException($"{val.Type}"),
    };

    private static void CompareResults(
      TreeComparer t, object[] expected, ExecValue[] actual)
    {
      for (var i = 0; i < expected.Length || i < actual.Length; i++)
      {
        var e = i < expected.Length ? expected[i] : null;
        var a = i < actual.Length ? actual[i] : default(ExecValue?);
        if (e == null && a == null)
          continue;
        if (e == null)
          t.Compare($"{i}", false, "<null>", $"{a.Value.Type} {Obj(a.Value)}");
        else if (a == null)
          t.Compare($"{i}", false, $"{e.GetType().Name} {e}", "<null>");
        else
        {
          var aobj = Obj(a.Value);
          t.Compare($"{i}", e.Equals(aobj), $"{e.GetType().Name} {e}", $"{a.Value.Type} {aobj}");
        }
      }
    }
  }

  [TestMethod]
  [DynamicData(nameof(LogCases))]
  public void TestLog(LogCase pcase)
  {
    var domain = new PatchDomain();
    foreach (var pmod in pcase.Mods)
    {
      var mod = domain.AddMod(pmod.Id);
      foreach (var f in pmod.Files)
        mod.ImportXml(f.Path, f.Xml);
    }
    var exec = new PatchExecutor(domain);
    exec.StepToEnd();

    var t = new TreeComparer("");
    ActionCase.Equals(t, pcase.Action, exec.Root);
    t.Assert();
  }
}