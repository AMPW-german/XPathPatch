
using System.Collections.Generic;
using System.Xml.Serialization;
using XPP.Doc;

namespace XPP.Patch;

public class PatchOpDeserializer
{
  private readonly Dictionary<string, XmlSerializer> serializers = [];

  public PatchOpDeserializer(bool registerDefault = true)
  {
    if (registerDefault)
      RegisterDefault();
  }

  public IPatchOp Deserialize(XPNodeRef node)
  {
    if (node.Type != XPType.Element || node.Name.Prefix != "")
      return null;
    if (!serializers.TryGetValue(node.Name.Local, out var serializer))
      return null;
    return serializer.Deserialize(new XPDocReader(node)) as IPatchOp;
  }

  public void Register<T>(
    string name, XmlSerializer serializer = null
  ) where T : IPatchOp, new() =>
    serializers[name] = serializer ?? new(typeof(T), new XmlRootAttribute(name));

  public void RegisterDefault()
  {
    Register<CopyPatch>("Copy");
    Register<MergePatch>("Merge");
    Register<DeletePatch>("Delete");
    Register<IfPatch>("If");
    Register<IfAnyPatch>("IfAny");
    Register<IfNonePatch>("IfNone");
    Register<WithPatch>("With");
  }
}

public interface IPatchOp
{
  public void Build(PatchLog.ActionRef parent, XPNodeRef node);
}

public abstract class PatchOp : IPatchOp
{
  [XmlAttribute("Path")] public string Path = ".";
  [XmlAttribute("From")] public string From = "$patch/*";

  public abstract void Build(PatchLog.ActionRef parent, XPNodeRef node);
}

[XmlRoot("Patch")]
public class PatchFile : PatchOp
{
  // TODO: priority/ordering

  public override void Build(PatchLog.ActionRef parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpPatch, target: node.Id);
}

public class CopyPatch : PatchOp
{
  [XmlAttribute("Pos")] public PatchPosition Pos;

  public override void Build(PatchLog.ActionRef parent, XPNodeRef node) =>
    parent.AddChild(
      ActionType.OpCopy, target: node.Id, pos: Pos, targetPath: Path, sourcePath: From);
}

public class MergePatch : PatchOp
{
  public override void Build(PatchLog.ActionRef parent, XPNodeRef node) =>
    parent.AddChild(
      ActionType.OpMerge, target: node.Id, targetPath: Path, sourcePath: From);
}

public class DeletePatch : PatchOp
{
  public override void Build(PatchLog.ActionRef parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpDelete, target: node.Id, targetPath: Path);
}

public class IfPatch : PatchOp
{
  public override void Build(PatchLog.ActionRef parent, XPNodeRef node)
  {
    var action = parent.AddChild(ActionType.OpIf, target: node.Id, targetPath: Path);
    var child = node.FirstContent;
    while (child.Valid)
    {
      if (child.Type is XPType.Element && child.Name == new XPName("", "", "Any"))
        action.AddChild(ActionType.OpIfAny, target: child.Id);
      else if (child.Type is XPType.Element && child.Name == new XPName("", "", "None"))
        action.AddChild(ActionType.OpIfNone, target: child.Id);
      child = child.NextSibling;
    }
  }
}

public class IfAnyPatch : PatchOp
{
  public override void Build(PatchLog.ActionRef parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpIfAny, target: node.Id, targetPath: Path);
}

public class IfNonePatch : PatchOp
{
  public override void Build(PatchLog.ActionRef parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpIfNone, target: node.Id, targetPath: Path);
}

public class WithPatch : PatchOp
{
  public override void Build(PatchLog.ActionRef parent, XPNodeRef node) =>
    parent.AddChild(ActionType.OpIfNone, target: node.Id, targetPath: Path);
}