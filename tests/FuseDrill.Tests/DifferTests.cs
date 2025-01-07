using System;
using Xunit;

namespace DifferTests;

public class SimpleDifferTests
{
    [Fact]
    public void TestGenerateDiff_WithStringLiterals_ShouldReturnCorrectDiff()
    {
        string oldText =
                    """
                    {
                      "Name": "John",
                      "Surname": "Doe",
                      "Age": 20,
                      "Grade": "A"
                    }
                    """;

        string newText =
                    """
                    {
                      "Name": "John",
                      "Surname": "Doe",
                      "Grade": "B"
                    }
                    """;

        // Expected diff output for this change
        string expectedDiff =
                   """
                   --- oldText
                   +++ newText
                     {
                       "Name": "John",
                       "Surname": "Doe",
                   -   "Age": 20,
                   +   "Grade": "B"
                   -   "Grade": "A"
                     }
                   """;


        // Generate the actual diff and normalize line endings
        string actualDiff = SimpleDiffer.GenerateDiff(newText: newText.Trim(), oldText: oldText.Trim());

        // Normalize both the expected and actual diffs to use the same line endings
        string normalizedExpectedDiff = expectedDiff.Replace("\r\n", "\n").TrimEnd();
        string normalizedActualDiff = actualDiff.Replace("\r\n", "\n").TrimEnd();

        // Assert that the actual diff matches the expected diff
        Assert.Equal(normalizedExpectedDiff, normalizedActualDiff);
    }
}
