
using System;
using XPP.Path;

namespace XPP;

public static class Program
{
  public static void Main(string[] args)
  {
    var xpath = XPath.Parse("child::para[position()=5][attribute::type='warning']");
    Console.WriteLine(xpath.Source);
    Console.WriteLine(new string(xpath.Data));
    for (var i = 0; i < xpath.Vals.Length; i++)
    {
      ref var val = ref xpath.Vals[i];
      Console.WriteLine($"V{i:00} {val.Type} {val.Left} {val.Right}");
    }
    for (var i = 0; i < xpath.Paths.Length; i++)
    {
      ref var path = ref xpath.Paths[i];
      Console.WriteLine($"P{i:00} {path.Type} {path.Paths} {path.Name}");
    }
  }
}