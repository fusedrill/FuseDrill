using FuseDrill.Core;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Octokit;
using System.ClientModel.Primitives;
using System.Text.Json;

public static class HelperFunctions
{
    public static async Task<bool> CliFlow(string? owner, string? repoName, string? branch, string? githubToken, string? fuseDrillBaseAddres, string? fuseDrillOpenApiUrl, string? fuseDrillTestAccountOAuthHeaderValue, bool smokeFlag, string? pullRequestNumber, string? geminiToken)
    {
        // Fuzz testing the API
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(fuseDrillBaseAddres),
        };

        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", fuseDrillTestAccountOAuthHeaderValue);

        var tester = new ApiFuzzer(httpClient, fuseDrillOpenApiUrl);
        var snapshot = await tester.TestWholeApi();
        var newSnapshotString = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true, Converters = { new DateTimeScrubbingConverter(), new GuidScrubbingConverter(), new DateTimeOffsetScrubbingConverter() }, });

        if (smokeFlag)
        {
            Console.WriteLine(newSnapshotString);
        }

        if (string.IsNullOrEmpty(newSnapshotString))
        {
            Console.WriteLine("API snapshot is empty.");
            return false;
        }

        // Save snapshot to a local file
        var filePath = $"api-snapshot.json";

        // GitHub client setup
        var githubClient = new GitHubClient(new ProductHeaderValue("FuseDrill"));

        // Authenticate GitHub client using a githubToken (replace with your githubToken)
        var tokenAuth = new Credentials(githubToken);
        githubClient.Credentials = tokenAuth;

        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repoName))
        {
            Console.WriteLine("Repository owner or name not found in environment variables.");
            return false;
        }

        if (string.IsNullOrEmpty(branch))
        {
            Console.WriteLine("Branch name not found in environment variables.");
            return false;
        }

        // Read the branch reference
        var branchRef = $"refs/heads/{branch}";

        var existingSnapshotString = await GetExistingSnapshotAsync(owner, repoName, branch, filePath, githubClient);
        await SaveSnapshotAsync(owner, repoName, branch, newSnapshotString, filePath, githubClient, branchRef);

        if (existingSnapshotString == "")
        {
            Console.WriteLine("No existing snapshot found.");
            Console.WriteLine("Stopping comparison.");
            return false;
        }

        if (!int.TryParse(pullRequestNumber, out var pullRequestNumberParsed))
        {
            Console.WriteLine("Pull request number does not exists.");
            return false;
        }

        if (string.IsNullOrEmpty(geminiToken))
        {
            Console.WriteLine("Gemini token is not provided, continuing without AI summarization.");
            return false;
        }

        string llmResponse = await CompareFuzzingsWithLLM(newSnapshotString, existingSnapshotString);

        if (string.IsNullOrEmpty(llmResponse))
        {
            Console.WriteLine("LLM response is empty there is no differences.");
            return false;
        }

        await PostCommentToPullRequestAsync(owner, repoName, pullRequestNumberParsed, llmResponse, githubClient);

        Console.WriteLine(llmResponse);
        return true;
    }

    private static async Task PostCommentToPullRequestAsync(string owner, string repoName, int pullRequestNumber, string comment, GitHubClient githubClient)
    {
        Console.WriteLine($"Creating comment at PR:{pullRequestNumber}");
        await githubClient.Issue.Comment.Create(owner, repoName, pullRequestNumber, comment);
    }

    public static async Task<string> AnalyzeFuzzingDiffWithLLM(Kernel kernel, string fuzzingOutputDiff)
    {
        var prompt =
    """
### Prompt for API Contract Reviews
**Context:**  
You are an expert in reviewing API contracts and changes for adherence to best practices, compatibility, and potential breaking changes. The API contracts use JSON structures, and I provide you with the differences between the previous version and the current version of the contract. 

**Example API Contract Difference:**  
--- oldText
+++ newText
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
-     "FullName": "John Doe",
-     "Grades": ["B","A"]
+     "Name": "John",
+     "Surname": "Doe",
+     "Grade": "B"
    }
  }

**Your task:**  
1. Provide a summary of the changes in the API Contract Difference. It should be list.  
2. Use simple and concise language and Paul Graham's tone.
3. Think deeply about the API method and request/response data does it make sense? Does values pass through the request and belongs to responses?,
4. Provide only information that i asked for.
5. Produce a concise markdown-formatted list of the analysis.  
6. Always use checklist - [ ] for every action point that is one property difference at a time.
7. Produce valid markdown.

---

### Expected Example of the LLM's Output
**Summary of Changes:**  
- The field `Breed` has been replaced with `Type`.  
- The field `Name` has been replaced with `FullName`.
- New field `FullName` added.
- New field `Surname` added.
- New field `Age` added.
- No changes have been made to the `PetType` field.  

**Actionable Recommendations:**  
- [ ] **Request property value passed should be the same as in response:**  When you send a request property `value`, you should expect a response property with be the same. (Not always applicable, use context and common sence).
- [ ] **Request property value should be processed and different in response:**  When you send a request property `value`, you should expect a response property to be different. (Not always applicable, use context and common sence).

---

Heres is the real API Contract Difference you should work on this:
{{$fuzzingOutputDiff}}
----
""";
        var code = kernel.CreateFunctionFromPrompt(prompt, executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 16000 });

        var markdownResponse = await kernel.InvokeAsync(code, new()
        {
            ["fuzzingOutputDiff"] = fuzzingOutputDiff,
        });

        return markdownResponse.ToString();
    }

    public static async Task<string> GetExistingSnapshotAsync(
        string owner,
        string repoName,
        string branch,
        string filePath,
        GitHubClient githubClient)
    {
        // GitHub client setup

        try
        {
            // Get the file contents from the repository by branch reference
            var branchRef = $"refs/heads/{branch}";
            var fileContents = await githubClient.Repository.Content.GetAllContentsByRef(owner, repoName, filePath, branchRef);

            // Return the content of the file
            return fileContents[0].Content; // Assuming the file exists and is not a directory
        }
        catch (NotFoundException)
        {
            Console.WriteLine($"The file '{filePath}' does not exist in branch '{branch}'.");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while fetching the snapshot: {ex.Message}");
            return string.Empty;
        }
    }

    public static async Task SaveSnapshotAsync(string? owner, string? repoName, string? branch, string newSnapshotString, string filePath, GitHubClient githubClient, string branchRef)
    {

        // Get the reference for the branch
        var reference = await githubClient.Git.Reference.Get(owner, repoName, branchRef);

        // Create a blob for the snapshot file
        var blob = new NewBlob
        {
            Content = newSnapshotString,
            Encoding = EncodingType.Utf8
        };

        var blobResult = await githubClient.Git.Blob.Create(owner, repoName, blob);

        // Create a tree with the new blob
        var newTree = new NewTree
        {
            BaseTree = reference.Object.Sha
        };

        newTree.Tree.Add(new NewTreeItem
        {
            Path = filePath,
            Mode = "100644",
            Type = TreeType.Blob,
            Sha = blobResult.Sha
        });

        var treeResult = await githubClient.Git.Tree.Create(owner, repoName, newTree);

        // Create a new commit
        var newCommit = new NewCommit("Add API snapshot JSON", treeResult.Sha, reference.Object.Sha);

        var commitResult = await githubClient.Git.Commit.Create(owner, repoName, newCommit);

        // Update the reference to point to the new commit
        await githubClient.Git.Reference.Update(owner, repoName, branchRef, new ReferenceUpdate(commitResult.Sha));

        Console.WriteLine($"Snapshot committed to branch {branch} in repository {owner}/{repoName}");
    }

    public static async Task<string> CompareFuzzingsWithLLM(string newText, string oldText)
    {
        if (newText == oldText)
            return string.Empty;

        //use difplex string comparison 
        var actualDiff = SimpleDiffer.GenerateDiff(oldText, newText);

        if (string.IsNullOrEmpty(actualDiff))
            return string.Empty;

        //use semantic kernel 
        var kernel = Kernel.CreateBuilder()
        //.AddGeminiChatCompletion() //Todo make config switch here
        .AddGithubChatCompletion() // Free for all github accounts but with limits;
        .Build();

        var llmResponse = await AnalyzeFuzzingDiffWithLLM(kernel, actualDiff);
        return llmResponse;
    }

}
