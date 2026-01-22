
using System;
using System.IO;
using System.Xml;
using XPP.Doc;

namespace XPP.Tests;

[TestClass]
public class ImportTest : BaseTest
{
  [TestMethod]
  public void TestImportReaderChain()
  {
    var expected = XPDocument.New();
    using (var f = File.OpenRead("TestData/XPath.xml"))
    {
      expected.LatestRoot.Import(XmlReader.Create(f));
    }

    var intermediate = new XmlDocument();
    intermediate.Load(new XPDocReader(expected.LatestRoot));

    var actual = XPDocument.New();
    actual.LatestRoot.Import(new XmlNodeReader(intermediate));

    XPNodeEqual(expected.LatestRoot, actual.LatestRoot);
  }
}