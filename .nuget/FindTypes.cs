using Mono.Cecil;
using System;
using System.Linq;

class FindTypes
{
  static void Main()
  {
    var path = @"E:\code\WOTRNineSwords-master\lib\Assembly-CSharp.dll";
    var asm = AssemblyDefinition.ReadAssembly(path);
    foreach (var type in asm.MainModule.GetTypes())
    {
      if (type.Name == "PrerequisiteFeature")
      {
        foreach (var field in type.Fields)
          Console.WriteLine(" field " + field.FieldType.FullName + " " + field.Name);
      }
      if (type.Name == "IOwnerGainLevelHandler")
      {
        foreach (var iface in type.Interfaces)
          Console.WriteLine(" iface " + iface.InterfaceType.FullName);
      }
      if (type.Name.IndexOf("GainLevel", StringComparison.Ordinal) >= 0 || type.Name == "IAbilityVisibilityProvider")
      {
        Console.WriteLine(type.FullName);
        foreach (var method in type.Methods)
          Console.WriteLine("  " + method.Name + "(" + string.Join(", ", method.Parameters.Select(p => p.ParameterType.FullName + " " + p.Name).ToArray()) + ")");
      }
    }
    var handler = asm.MainModule.Types.FirstOrDefault(t => t.Name == "IUnitGainLevelHandler");
    if (handler != null)
    {
      foreach (var method in handler.Methods)
        Console.WriteLine(" method " + method.Name + "(" + string.Join(", ", method.Parameters.Select(p => p.ParameterType.FullName + " " + p.Name)) + ")");
    }
    var vis = asm.MainModule.Types.FirstOrDefault(t => t.Name == "IAbilityVisibilityProvider");
    if (vis != null)
    {
      foreach (var method in vis.Methods)
        Console.WriteLine(" vis " + method.Name + "(" + string.Join(", ", method.Parameters.Select(p => p.ParameterType.FullName + " " + p.Name)) + ")");
    }
  }
}
