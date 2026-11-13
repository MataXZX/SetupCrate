using SetupCrate.Services;

namespace SetupCrate.Models;

public sealed class CategoryEntry
{
    public CategoryEntry(string name, int count)
    {
        Name = name;
        Count = count;
    }

    public string Name { get; }
    public int Count { get; }

    public string DisplayName => Loc.T(Name);
}
