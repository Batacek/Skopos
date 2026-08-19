using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace Skopos.Highlighting.Rendering;

/// <summary>
/// Foldable ranges come out of the tree essentially for free — this is only the conversion
/// to <see cref="NewFolding"/>, the counterpart of AvalonEdit's <c>XmlFoldingStrategy</c>.
/// </summary>
public static class TreeSitterFoldingStrategy
{
    public static void Apply(FoldingManager manager, TextDocument document, IReadOnlyList<FoldRegion> regions)
    {
        var foldings = new List<NewFolding>(regions.Count);
        var length = document.TextLength;

        foreach (var region in regions)
        {
            var start = Math.Clamp(region.Start, 0, length);
            var end = Math.Clamp(region.End, 0, length);
            if (end <= start) continue;

            foldings.Add(new NewFolding(start, end) { Name = region.Preview });
        }

        // UpdateFoldings requires the ranges sorted by start offset.
        foldings.Sort(static (a, b) => a.StartOffset.CompareTo(b.StartOffset));

        // Fold state is not persisted between runs, so there is nothing to restore.
        manager.UpdateFoldings(foldings, firstErrorOffset: -1);
    }
}
