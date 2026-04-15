using System;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string path = @"D:\Unity Stuff\HorrorProject\Assets\Script\Player\PlayerWeaponSystem.cs";
        string content = File.ReadAllText(path);
        
        // Remove all the malformed SetLastHitPoint lines
        content = Regex.Replace(content, @"[\s]*damageInfo\[i\]\.enemy\.health\.SetLastHitPoint\(damageInfo\[i\]\.hit\.point\);[^\r\n]*", "");
        
        // Add the correct line
        content = Regex.Replace(content, 
            @"(damageInfo\[i\]\.enemy\.health\.isDamageByWeakpointHit = damageInfo\[i\]\.isHittingWeakpoint;)\s*\r?\n\s*(damageInfo\[i\]\.enemy\.health\.Damage)",
            "$1\r\n                damageInfo[i].enemy.health.SetLastHitPoint(damageInfo[i].hit.point);\r\n                $2");
        
        File.WriteAllText(path, content);
        Console.WriteLine("Fixed!");
    }
}
