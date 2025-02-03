# FuseDrill - Remove bugs by fuzzing your application openapi.

[![Publish cli](https://github.com/fusedrill/FuseDrill/actions/workflows/PublishFusedrillCli.yml/badge.svg?branch=main)](https://github.com/fusedrill/FuseDrill/actions/workflows/PublishFusedrillCli.yml)
## 🚀 Introduction

FuseDrill is a tool for **fuzzing** and **simulation** testing of **OpenAPIs** using snapshots. It helps you identify open API contract changes from the previous version to the current one. 

### C# example
```cs
    [Fact]
    public async Task TestMinimumConfiguration()
    {
        //if you are using top-level statements in web api you need to add :
        //public partial class Program { } in Program.cs class

        var fuzzer = new ApiFuzzerWithVerifier<Program>();
        await fuzzer.TestWholeApi();
    }
```


### Try it out (No Github integration)
```cmd
docker run --network host --rm \
 -e FUSEDRILL_BASE_ADDRESS="https://api.apis.guru/v2" \
 -e FUSEDRILL_OPENAPI_URL="https://api.apis.guru/v2/openapi.yaml" \
 -e FUSEDRILL_OAUTH_HEADER="Bearer your-API-test-account-oauth-token"  \
 -e SMOKE_FLAG="true" \
 ghcr.io/fusedrill/fusedrill-cli:latest
```

## ✨ Features

- **Automated Fuzzing**: Generates permutations of all API input requests of all API methods with randomizer.
- **Detailed Reports**: Get a [json report](https://github.com/fusedrill/FuseDrill?tab=readme-ov-file#-example-fuzzing-report-of-api-spec) on the fuzz test result that will be [committed to your source control](https://github.com/fusedrill/FuseDrill/blob/main/api-snapshot.json) to create a baseline of correctness.
- **CI/CD Integration**: Integrate with your [CI/CD pipeline](https://github.com/fusedrill/FuseDrill?tab=readme-ov-file#-remote-fuzzing).
- **AI helper bot**: Reads the fuzzing diff report analyzes it and adds suggestion list [comment](https://github.com/fusedrill/FuseDrill/pull/20#issuecomment-2557747106) on a pull request.
## 📋 Usage

To use FuseDrill, you need to set up a GitHub Actions workflow. Here is an example configuration:

## 📋 Remote fuzzing

```yml
name: FuseDrill Fuzzing/Simulation Testing

on:
  push:
    branches:
      - main  # Runs on push to the main branch
  pull_request:
    branches:
      - main  # Runs on pull requests to the main branch

jobs:
  fuzz-test:
    runs-on: ubuntu-latest

    steps:
    - name: Pull FuseDrill Docker Image
      run: docker pull ghcr.io/fusedrill/fusedrill-cli:latest # Add docker image versioning later

    - name: Run FuseDrill CLI in Docker
      run: |
         docker run --network host --rm \
          -e FUSEDRILL_BASE_ADDRESS="https://api.apis.guru/v2" \
          -e FUSEDRILL_OPENAPI_URL="https://api.apis.guru/v2/openapi.yaml" \
          -e FUSEDRILL_OAUTH_HEADER="Bearer your-API-test-account-oauth-token"  \
          -e GITHUB_TOKEN="${{ secrets.GITHUB_TOKEN }}" \
          -e SMOKE_FLAG="true" \
          -e GITHUB_REPOSITORY_OWNER="${{ github.repository_owner }}" \
          -e GITHUB_REPOSITORY="${{ github.repository }}" \
          -e GITHUB_HEAD_REF="${{ github.head_ref }}" \
          -e GEMINI_API_KEY="${{ secrets.GEMINI_API_KEY }}" \
          ghcr.io/fusedrill/fusedrill-cli:latest

      - name: Post-run step
        run: |
          echo "Fuzzing test completed"
```

## 💬 Example fuzzing report of [API SPEC](https://api.apis.guru/v2/openapi.yaml).
```json
{
  "Seed": 1234567,
  "TestSuites": [
    {
      "ApiCalls": [
        {
          "MethodName": "GetMetrics_http_get_Async",
          "Order": 2,
          "Response": {
            "NumSpecs": 3992,
            "NumAPIs": 2529,
            "NumEndpoints": 108837,
            "Unreachable": 166,
            "Invalid": 688,
            "Unofficial": 25,
            "Fixes": 84860,
            "FixedPct": 23,
            "Datasets": [
              {
                "data": {
                  "adyen.com": [],
                  "amadeus.com": [],
                  "amazonaws.com": [],
                  "apideck.com": [],
                  "apisetu.gov.in": [],
                  "azure.com": [],
                  "ebay.com": [],
                  "fungenerators.com": [],
                  "github.com": [],
                  "googleapis.com": [],
                  "hubapi.com": [],
                  "interzoid.com": [],
                  "mastercard.com": [],
                  "microsoft.com": [],
                  "nexmo.com": [],
                  "nytimes.com": [],
                  "Others": [],
                  "parliament.uk": [],
                  "sportsdata.io": [],
                  "twilio.com": [],
                  "vtex.local": [],
                  "windows.net": []
                },
                "title": []
              }
            ],
            "Stars": 3151,
            "Issues": 35,
            "ThisWeek": {
              "Added": 9,
              "Updated": 437
            },
            "NumDrivers": 10,
            "NumProviders": 677
          },
          "HttpMethod": "get"
        }
      ]
    },
    {
      "ApiCalls": [
        {
          "MethodName": "GetAPI_http_get_Async",
          "Order": 1,
          "Request": [
            "RandomString275",
            "RandomString157"
          ],
          "Response": {
            "StatusCode": 404,
            "Message": "The HTTP status code of the response was not expected (404).  Status: 404 Response:  <!DOCTYPE html> <html>   <head>     <meta http-equiv=\"Content-type\" content=\"text/html; charset=utf-8\">     <meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:; connect-src 'self'\">     <title>Page not found &middot; GitHub Pages</title>     <style type=\"text/css\" media=\"screen\">       body {         background-color: #f1f1f1;         margin: 0;         font-family: \"Helvetica Neue\", Helvetica, Arial, sans-serif;       }        .container { margin",
            "TypeName": "ApiException"
          },
          "HttpMethod": "get"
        },
        {
          "MethodName": "GetMetrics_http_get_Async",
          "Order": 3,
          "Response": {
            "NumSpecs": 3992,
            "NumAPIs": 2529,
            "NumEndpoints": 108837,
            "Unreachable": 166,
            "Invalid": 688,
            "Unofficial": 25,
            "Fixes": 84860,
            "FixedPct": 23,
            "Datasets": [
              {
                "data": {
                  "adyen.com": [],
                  "amadeus.com": [],
                  "amazonaws.com": [],
                  "apideck.com": [],
                  "apisetu.gov.in": [],
                  "azure.com": [],
                  "ebay.com": [],
                  "fungenerators.com": [],
                  "github.com": [],
                  "googleapis.com": [],
                  "hubapi.com": [],
                  "interzoid.com": [],
                  "mastercard.com": [],
                  "microsoft.com": [],
                  "nexmo.com": [],
                  "nytimes.com": [],
                  "Others": [],
                  "parliament.uk": [],
                  "sportsdata.io": [],
                  "twilio.com": [],
                  "vtex.local": [],
                  "windows.net": []
                },
                "title": []
              }
            ],
            "Stars": 3151,
            "Issues": 35,
            "ThisWeek": {
              "Added": 9,
              "Updated": 437
            },
            "NumDrivers": 10,
            "NumProviders": 677
          },
          "HttpMethod": "get"
        }
      ]
    }
....
```

## 📋 Fuzzing inside CI/CD example 
```yml
name: FuseDrill Fuzzing/Simulation Testing

on:
  push:
    branches:
      - main  # Runs on push to the main branch
  pull_request:
    branches:
      - main  # Runs on pull requests to the main branch

jobs:
  fuzz-test:
    runs-on: ubuntu-latest

    steps:
# ------------ Deploy your app or run inside the GitHub CI/CD 
      - name: Pull FuseDrill test api Docker Image
        run: docker pull ghcr.io/fusedrill/fusedrill/testapi:latest
        
      - name: Run Test API
        run: |
          docker run -d \
            -e ASPNETCORE_ENVIRONMENT="Development" \
            -p 8080:8080 \
            ghcr.io/fusedrill/fusedrill/testapi:latest
          
      - name: Wait for Test API to be Ready
        run: |
          # Wait for the API to start and be reachable on port 8080
          until curl -s http://localhost:8080/swagger/v1/swagger.json; do
            echo "Waiting for Test API to start..."
            sleep 5
          done
# ------------------------------------------------------
    - name: Pull FuseDrill Docker Image
      run: docker pull ghcr.io/fusedrill/fusedrill-cli:latest # Add docker image versioning later

    - name: Run FuseDrill CLI in Docker
      run: |
         docker run --network host --rm \
          -e FUSEDRILL_BASE_ADDRESS="http://localhost:8080/" \
          -e FUSEDRILL_OPENAPI_URL="http://localhost:8080/swagger/v1/swagger.json" \
          -e FUSEDRILL_OAUTH_HEADER="Bearer your-API-test-account-oauth-token"  \
          -e GITHUB_TOKEN="${{ secrets.GITHUB_TOKEN }}" \
          -e SMOKE_FLAG="true" \
          -e GITHUB_REPOSITORY_OWNER="${{ github.repository_owner }}" \
          -e GITHUB_REPOSITORY="${{ github.repository }}" \
          -e GITHUB_HEAD_REF="${{ github.head_ref }}" \
          -e GEMINI_API_KEY="${{ secrets.GEMINI_API_KEY }}" \
          ghcr.io/fusedrill/fusedrill-cli:latest

      - name: Post-run step
        run: |
          echo "Fuzzing test completed"
```

## ✨ Futures

- **Reduce combinations using LLMs**: Permutations create a lot of test cases, exploiting the symmetry of A rest API by first doing POST then DELETE would reduce cases.
- **HPC fuzzing**: Combining the clusters would eliminate permutation search space using thousands of docker containers. (Already have docker/container support)
- **CI/CD Integration**: Integrate with your CI/CD pipeline.
- **AI helper bot**: Reeds the fuzzing diff report analyzes it and adds suggestion list comment on a pull request.
- **Vertical fuzzing**: Every internal interaction captured of your dynamic system to a [ASCII tree structure](https://x.com/martasp32/status/1812166465866318080) . 

---
### FAQ for Your API Fuzzer

#### **General Questions**

**Q1: What is an API fuzzer?**
A: An API fuzzer is a tool that tests APIs by sending random, unexpected, or malicious inputs to identify potential vulnerabilities or issues in the system. With AI-enhanced fuzzing, the tool crafts more sophisticated and context-aware test cases, increasing the likelihood of uncovering complex issues and ensuring better coverage during testing.

**Q2: Why is fuzzing important for my API?**
A: Fuzzing is crucial for identifying and fixing hidden vulnerabilities or bugs in your API before they reach your clients. By detecting unexpected behavior early, the tool ensures your changes are expected and won’t disrupt your users' experience when released. It helps maintain trust and reliability in your services.

**Q3: What are snapshot comparisons?**
A: Snapshot comparison allows you to compare the current state of your API responses against previously saved snapshots, making it easy to spot unintended changes.

#### **Integration and Compatibility**

**Q4: Is the API fuzzer compatible with my local environment?**
A: Absolutely. The Basic and Team plans are designed to run locally, ensuring no data leaves your environment unless you choose to integrate external tools.

#### **Getting Started**

**Q5: How do I install the API fuzzer?**
A: The Basic version is open-source and available on GitHub. Follow the installation guide in the repository to set it up locally.

If you have further questions about your complicated case and setup, feel free to reach out via [![Mail Me](https://img.shields.io/badge/Mail%20Me-Email-blue)](mailto:martasp3289@gmail.com)


## 💬 Contact

For any questions or feedback, please open an issue on the [GitHub repository](https://github.com/fusedrill/FuseDrill/issues).
