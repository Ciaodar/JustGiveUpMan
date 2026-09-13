using System;
using System.Reflection;

class Program {
    static void Main() {
        Assembly asm = Assembly.LoadFrom(@"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll");
        
        var stanceLinkType = asm.GetType("TaleWorlds.CampaignSystem.StanceLink");
        if (stanceLinkType != null) {
            Console.WriteLine("StanceLink methods:");
            foreach(var m in stanceLinkType.GetMethods()) {
                if (m.Name.Contains("Score") || m.Name.Contains("Casualt") || m.Name.Contains("Siege") || m.Name.Contains("Town"))
                    Console.WriteLine(m.Name);
            }
        }
        
        Console.WriteLine("\nChangeOwnerOfSettlementAction methods:");
        var actionType = asm.GetType("TaleWorlds.CampaignSystem.Actions.ChangeOwnerOfSettlementAction");
        if (actionType != null) {
            foreach(var m in actionType.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                Console.WriteLine(m.Name);
            }
        }
    }
}
