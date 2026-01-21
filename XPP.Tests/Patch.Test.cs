
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using XPP.Doc;
using XPP.Patch;

namespace XPP.Tests;

[TestClass]
public partial class PatchTests : BaseTest
{
  private static IEnumerable<object[]> ResultCases => new List<ResultCase>()
  {
    new([
      new("Mod1", [
        new("F1.xml", "<A><B C='v1' /></A>"),
        new("P1.xml", "<Patch><Copy Path='Mod/A/B/@C' From='\"v2\"' /></Patch>"),
      ]),
    ], "Root/Mod/A", "<A Path='F1.xml'><B C='v2' /></A>"),
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
    ], "Root/Mod/A", "<A Path='F1.xml'><B C='v1' D='E' /></A>"),
    new([
      new("Mod1", [
        new("F1.xml", "<A><B C='v1' /></A>"),
        new("P1.xml", "<Patch><Delete Path='Mod/A/B/@C' /></Patch>")
      ]),
    ], "Root/Mod/A", "<A Path='F1.xml'><B /></A>"),
    new([
      new("Mod1", [
        new("F1.xml", "<A><B C='1' /><B C='2' /></A>"),
        new("P1.xml", """
          <Patch>
            <With Path='Mod/A/B/@C'>
              <If Path='.=1'>
                <Any><Copy From='"a"' /></Any>
                <None><Copy From='"b"' /></None>
              </If>
            </With>
          </Patch>
        """)
      ]),
    ], "Root/Mod/A", "<A Path='F1.xml'><B C='a' /><B C='b' /></A>"),
    new([
      new("Mod1", [
        new("F1.xml", "<A><B C='1' /></A>"),
        new("P1.xml", "<Patch><Copy Path='Mod/A/B/@C'>2</Copy></Patch>"),
      ]),
    ], "Root/Mod/A/B", "<B C='2' />"),
    new([
      new("Mod1", [
        new("F1.xml", "<A><B C='1' /></A>"),
        new("P1.xml", """
          <Patch>
            <Copy Path='Mod/A/B/@C' From='sum($patch/X/@*)'>
              <X A='2' B='3' />
            </Copy>
          </Patch>
        """),
      ]),
    ], "Root/Mod/A/B", "<B C='5' />"),
    new([
      new("Mod1", [
        new("F1.xml", "<A><B C='1' /></A>"),
        new("P1.xml", """
          <Patch>
            <SetVar Name='testvar' Path='sum($patch/X/@*)'>
              <X A='2' B='3' />
            </SetVar>
            <SetVar Name='testvar' Path='$testvar*2' />
            <Copy Path='Mod/A/B/@C' From='$testvar' />
          </Patch>
        """),
      ]),
    ], "Root/Mod/A/B", "<B C='10' />"),
  }.Select(p => new object[] { p });

  public record class FileCase(string Path, string Xml);
  public record class ModCase(string Id, FileCase[] Files);
  public record class ResultCase(ModCase[] Mods, Path ExpPath, string Expected);

  [TestMethod]
  [DynamicData(nameof(ResultCases))]
  public void TestPatchResult(ResultCase pcase)
  {
    var domain = new PatchDomain();
    foreach (var pmod in pcase.Mods)
    {
      var mod = domain.AddMod(pmod.Id);
      foreach (var f in pmod.Files)
        mod.ImportXml(f.Path, f.Xml);
    }

    var expDoc = XPDocument.New();
    expDoc.LatestRoot.Import(domain.Doc.LatestRoot.FirstContent);
    var preExp = pcase.ExpPath.Get(expDoc.LatestRoot);

    var subDoc = XPDocument.New();
    subDoc.LatestRoot.Import(XmlReader.Create(
      new StringReader(pcase.Expected),
      new() { IgnoreWhitespace = true }));

    preExp.Parent.Import(subDoc.LatestRoot.FirstContent, after: preExp);
    preExp.Remove();

    var exec = new PatchExecutor(domain);
    exec.StepToEnd();

    XPNodeEqual(expDoc.LatestRoot.FirstContent, domain.Doc.LatestRoot.FirstContent);
  }
}