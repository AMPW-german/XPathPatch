
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using XPP.Doc;
using XPP.Path;
using DocXPath = System.Xml.XPath.XPathExpression;
using XPXPath = XPP.Path.XPath;

namespace XPP;

public static class Program
{
  private const string TestFolder = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Stationeers\\rocketstation_Data\\StreamingAssets";

  public static void Main(string[] args)
  {
    var fileData = new List<byte[]>();
    foreach (var file in Directory.EnumerateFiles(TestFolder, "*.xml", SearchOption.AllDirectories))
    {
      // if (file.Replace('\\', '/').Contains("/Language/"))
      //   continue;
      fileData.Add(File.ReadAllBytes(file));
    }

    const string countExpr = "count(//* | //@*)";

    {
      var doc = new XmlDocument();
      var root = doc.AppendChild(doc.CreateElement("Root"));
      var countDxp = DocXPath.Compile(countExpr);

      using (Time("XmlDocument Import"))
      {
        foreach (var file in fileData)
        {
          var fdoc = new XmlDocument();
          fdoc.Load(XmlReader.Create(new MemoryStream(file)));
          root.AppendChild(doc.ImportNode(fdoc.DocumentElement, true));
        }
      }

      using (Time("XmlDocument Eval"))
      {
        var docRes = root.CreateNavigator().Evaluate(countDxp);
        Console.WriteLine($"DOC {docRes}");
      }
    }

    {
      var xpdoc = XPDocument.New();
      var countXpp = XPXPath.Parse(countExpr);
      var xproot = xpdoc.LatestRoot.AddElement("Root");

      using (Time("XPDocument Import"))
      {
        foreach (var file in fileData)
          xproot.Import(XmlReader.Create(new MemoryStream(file)));
      }

      const int EVAL_ITERS = 1;
      using (Time("XPDocument Eval"))
      {
        ExecResult val = default;
        for (var i = 0; i < EVAL_ITERS; i++)
          val = countXpp.Exec(xproot);
        Console.WriteLine(val.Number);
      }
    }
  }

  private static Timing Time(string name) => new(name);

  private struct Timing(string name) : IDisposable
  {
    private readonly string name = name;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();

    public void Dispose()
    {
      stopwatch.Stop();
      Console.WriteLine($"{name}: {stopwatch.Elapsed.TotalMilliseconds:0.##}ms");
    }
  }
}