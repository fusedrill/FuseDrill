using System.Text.Json;
namespace tests;

public class ScrubbingConverterTests
{
    [Fact]
    public async Task DateAndGuidAndDateTimeOffsetValuesShouldBeScrubbed()
    {
        var testData = new
        {
            Id = new { id = Guid.NewGuid(), date = DateTimeOffset.Now }, 
            Name = "In-Memory Test Data",
            Date = DateTime.Now,  
            Tags = new[] { "example", "snapshot", "memory" },
            Types = new[] { "example".GetType(), 6.GetType(), new int[] { }.GetType() }
        };

        var json = JsonSerializer.Serialize(testData, SerializerOptions.GetOptions());

        await Verify(json);
    }
}

