using System.Collections.Generic;
using System.Linq;

namespace VicotSoft.OutlineFeature
{

public class OutlineManager
{
    private static OutlineManager Instance { get; } = new();

    private HashSet<OutlineEffect> objects = new HashSet<OutlineEffect>();

    public static void AddObject(OutlineEffect effect)
    {
        Instance.objects.Add(effect);
    }
    
    public static void RemoveObject(OutlineEffect effect)
    {
        Instance.objects.Remove(effect);
    }

    public static OutlineEffect[] GetObjects()
    {
        return Instance.objects.ToArray();
    }
}

}
