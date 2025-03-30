using DotNet.Testcontainers.Builders;
using FuseDrill.Core;
using Octokit;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Capture;
using FuseDrill;

namespace tests;

// Define a test collection
[CollectionDefinition("Sequential Tests", DisableParallelization = true)]
public class VerticalFuzzingTest
{
    [Fact]
    public async Task FuzzingWebApp()
    {
        // Just test web application calls capture snapshot.

        await CallTreeCapture.BeginCapturingCallTree();
        
        var fuzzer = new ApiFuzzer<Program>(callEndpoints:false);
        var results = await fuzzer.TestWholeApi();
        
        var callTree = CallTreeCapture.EndCapturingCallTree();
        await Verify(callTree);
    }
    
    [Fact]
    public async Task PachedMethodsSnapshot()
    {
        var methods = await MyClassPatch.GetMethods();
        
        await Verify(methods);
    }
    
    [Fact]
    public async Task CaptureEveryVericallFuzzingWhileFuzzing()
    {
        await CallTreeCapture.BeginCapturingCallTree();
        
        var fuzzer = new ApiFuzzer<Program>();
        var results = await fuzzer.TestWholeApi();
        
        var callTree = CallTreeCapture.EndCapturingCallTree();
        await Verify(callTree);
    }
}
