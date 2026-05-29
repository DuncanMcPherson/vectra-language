using VectraLang.ModuleLoader.Models;

namespace VectraLang.ModuleLoader;

public static class ModuleSorter
{
    public static List<VectraModule> TopoSort(List<VectraModule> modules)
    {
        var moduleMap = modules.ToDictionary(m => m.Name);
        var inDegree = modules.ToDictionary(m => m.Name, m => m.Dependencies.Count);
        var dependents = modules.ToDictionary(m => m.Name, _ => new List<string>());

        foreach (var module in modules)
        {
            foreach (var dep in module.Dependencies)
            {
                if (dependents.TryGetValue(dep, out var value))
                    value.Add(module.Name);
            }
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        var sorted = new List<VectraModule>();
        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            sorted.Add(moduleMap[name]);

            foreach (var dep in dependents[name])
            {
                inDegree[dep]--;
                if (inDegree[dep] == 0)
                    queue.Enqueue(dep);
            }
        }

        return sorted.Count != modules.Count ? throw new InvalidOperationException("Circular dependency detected in module graph") : sorted;
    }
}