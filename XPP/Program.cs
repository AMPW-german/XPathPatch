
using System;
using System.IO;
using System.Xml;
using XPP.Doc;

using DocXPath = System.Xml.XPath.XPathExpression;
using XPXPath = XPP.Path.XPath;

namespace XPP;

public static class Program
{

  private const string TestFolder = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Stationeers\\rocketstation_Data\\StreamingAssets";

  public static void Main(string[] args)
  {
    var doc = new XmlDocument();
    var xpdoc = XPDocument.New();

    var root = doc.AppendChild(doc.CreateElement("Root"));
    var xproot = xpdoc.LatestRoot.AddElement("Root");

    var fcount = 0;
    foreach (var file in Directory.EnumerateFiles(TestFolder, "*.xml", SearchOption.AllDirectories))
    {
      {
        var fileDoc = new XmlDocument();
        using var f = File.OpenRead(file);
        fileDoc.Load(XmlReader.Create(f, new()
        {
          IgnoreComments = false,
          ValidationFlags = 0,
          ValidationType = ValidationType.None,
        }));
        root.AppendChild(doc.ImportNode(fileDoc.DocumentElement, true));
      }
      {
        using var f = File.OpenRead(file);
        xproot.Import(XmlReader.Create(f, new() { IgnoreWhitespace = true }));
      }
      Console.WriteLine(file);
      if (++fcount >= 5)
        break;
    }

    const string countExpr = "count(//* | //@*)";
    var countDxp = DocXPath.Compile(countExpr);
    var countXpp = XPXPath.Parse(countExpr);

    var docRes = root.CreateNavigator().Evaluate(countDxp);
    Console.WriteLine(docRes);
    foreach (var val in countXpp.Exec(xproot))
      Console.WriteLine($"XPP {val.Number}");
  }
}