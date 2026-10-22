using System.Reflection;
var f = Directory.GetFiles(@"C:\Users\Ahri\.nuget\packages", "Avalonia.Controls.dll", SearchOption.AllDirectories)
    .First(x => x.Contains("12.1.3") && x.Contains("ref"));
var asm = Assembly.LoadFrom(f);
var d = asm.GetType("Avalonia.Controls.WindowDecorations")!;
Console.WriteLine($"IsEnum: {d.IsEnum}");
if (d.IsEnum) foreach (var n in Enum.GetNames(d)) Console.WriteLine($"  {n}");
else { foreach (var p in d.GetProperties()) Console.WriteLine($"  prop {p.Name}: {p.PropertyType.Name}"); }
