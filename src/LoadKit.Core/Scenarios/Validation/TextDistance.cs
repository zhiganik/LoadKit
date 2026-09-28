namespace LoadKit.Core.Scenarios.Validation;

/// <summary>Edit distance for "did you mean" hints.</summary>
internal static class TextDistance
{
    public static int Levenshtein(string first, string second)
    {
        var previousRow = new int[second.Length + 1];
        var currentRow = new int[second.Length + 1];
        for (var column = 0; column <= second.Length; column++)
        {
            previousRow[column] = column;
        }

        for (var row = 1; row <= first.Length; row++)
        {
            currentRow[0] = row;
            for (var column = 1; column <= second.Length; column++)
            {
                var substitutionCost = first[row - 1] == second[column - 1] ? 0 : 1;
                currentRow[column] = Math.Min(
                    Math.Min(currentRow[column - 1] + 1, previousRow[column] + 1),
                    previousRow[column - 1] + substitutionCost);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[second.Length];
    }
}
