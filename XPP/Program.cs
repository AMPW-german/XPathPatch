
using System;
using XPP.XPatch;

namespace XPP;

public static class Program
{
  public static void Main(string[] args)
  {
    var xpath = XPath.Parse("child::para[position()=last()-1]");
    Console.WriteLine(xpath.Source);
    for (var i = 0; i < xpath.Nodes.Length; i++)
    {
      var node = xpath.Nodes[i];
      Console.WriteLine($"{i:00} {node.Type} {node.Token.Type} '{xpath.Source.AsSpan()[node.Token.Data]}' {node.Child0} {node.Child1}");
    }
  }
}