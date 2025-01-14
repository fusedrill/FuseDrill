using Microsoft.VisualStudio.TestPlatform.TestHost;

public class LLMPlayGround
{
    [Fact(Skip = "Test Uses LLM, only good for manual debugging and tweaking prompt")]
    //[Fact]
    public async Task CompareFuzzingsWithLLMTest()
    {

        string oldText =
                    """
                    {
                      "MethodName": "UserPOSTAsync",
                      "Order": 4,
                      "Request": {
                        "Id": 4,
                        "Name": "John",
                        "Surname": "Doe",
                        "Grade": "B"
                      },
                      "Response": {
                        "Id": 4,
                        "Name": "John",
                        "Surname": "Doe",
                        "Grade": "B"
                      }
                    }
                    """;

        string newText =
                    """
                    {
                      "MethodName": "UserPOSTAsync",
                      "Order": 4,
                      "Request": {
                        "Id": 4,
                        "Name": "John",
                        "Surname": "Doe",
                        "Grade": "B"
                      },
                      "Response": {
                        "Id": 4,
                        "FullName": "John Doe",
                        "Grades": ["B","A"]
                      }
                    }
                    """;

        var resut = await HelperFunctions.CompareFuzzingsWithLLM(newText, oldText);
    }
}
