using System;
using System.Text;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

public static class SimpleDiffer
{
    public static string GenerateDiff(string newText, string oldText)
    {
        // Create a Differ object
        var differ = new Differ();

        // Use the InlineDiffBuilder to generate the diff
        var diffBuilder = new InlineDiffBuilder(differ);

        // Generate the diff result
        var diffResult = diffBuilder.BuildDiffModel(oldText: oldText, newText: newText);

        // Create a StringBuilder to accumulate the diff output
        var sb = new StringBuilder();

        sb.AppendLine("--- oldText");
        sb.AppendLine("+++ newText");

        // Append the diff to the StringBuilder
        foreach (var line in diffResult.Lines)
        {
            // Using a switch expression to handle different ChangeTypes
            sb.AppendLine(line.Type switch
            {
                ChangeType.Deleted => $"- {line.Text}",   // Lines that were deleted
                ChangeType.Inserted => $"+ {line.Text}",  // Lines that were inserted
                ChangeType.Modified => $"~ {line.Text}",  // Lines that were modified (if needed)
                ChangeType.Imaginary => $"? {line.Text}", // Imaginary lines, usually used for added or deleted in context
                ChangeType.Unchanged => $"  {line.Text}", // Unchanged lines (no prefix)
                _ => throw new ArgumentOutOfRangeException()  // Default case for unknown types
            });
        }

        return sb.ToString(); // Return the diff as a string

    }
}
