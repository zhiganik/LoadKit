using System.Diagnostics.CodeAnalysis;

namespace LoadKit.Core.Scenarios.Templates;

/// <summary>Known template generators, looked up by exact (case-sensitive) name.</summary>
public sealed class TemplateRegistry
{
    private readonly Dictionary<string, ITemplateGenerator> _generatorsByName;

    public TemplateRegistry(IEnumerable<ITemplateGenerator> generators)
    {
        _generatorsByName = generators.ToDictionary(generator => generator.Name, StringComparer.Ordinal);
    }

    public IEnumerable<ITemplateGenerator> Generators => _generatorsByName.Values;

    public static TemplateRegistry CreateDefault()
    {
        return new TemplateRegistry(
        [
            new GuidTemplateGenerator(),
            new SequenceTemplateGenerator(),
            new RandomIntTemplateGenerator(),
            new NowTemplateGenerator(),
        ]);
    }

    public bool TryGet(string name, [NotNullWhen(true)] out ITemplateGenerator? generator)
    {
        return _generatorsByName.TryGetValue(name, out generator);
    }
}
