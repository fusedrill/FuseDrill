using DiffPlex;
using FuseDrill.Core;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Octokit;
using System.Net.Http;
using System.Text.Json;
using static HelperFunctions;

//Run printenv
//SELENIUM_JAR_PATH=/usr/share/java/selenium-server.jar
//CONDA=/usr/share/miniconda
//GITHUB_WORKSPACE=/home/runner/work/FuseDrill/FuseDrill
//JAVA_HOME_11_X64=/usr/lib/jvm/temurin-11-jdk-amd64
//GITHUB_PATH=/home/runner/work/_temp/_runner_file_commands/add_path_5ca5fb58-f1df-4e8d-b521-25a79ada7402
//GITHUB_ACTION=__run_3
//JAVA_HOME=/usr/lib/jvm/temurin-11-jdk-amd64
//DOTNET_ROOT=/usr/share/dotnet
//GITHUB_RUN_NUMBER=14
//RUNNER_NAME=GitHub Actions 2
//GRADLE_HOME=/usr/share/gradle-8.11.1
//GITHUB_REPOSITORY_OWNER_ID=13511209
//ACTIONS_RUNNER_ACTION_ARCHIVE_CACHE=/opt/actionarchivecache
//XDG_CONFIG_HOME=/home/runner/.config
//DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
//ANT_HOME=/usr/share/ant
//JAVA_HOME_8_X64=/usr/lib/jvm/temurin-8-jdk-amd64
//GITHUB_TRIGGERING_ACTOR=martasp
//GITHUB_REF_TYPE=branch
//HOMEBREW_CLEANUP_PERIODIC_FULL_DAYS=3650
//ANDROID_NDK=/usr/local/lib/android/sdk/ndk/27.2.12479018
//BOOTSTRAP_HASKELL_NONINTERACTIVE=1
//***
//PIPX_BIN_DIR=/opt/pipx_bin
//STATS_TRP=true
//GITHUB_REPOSITORY_ID=899104492
//DEPLOYMENT_BASEPATH=/opt/runner
//GITHUB_ACTIONS=true
//STATS_VMD=true
//ANDROID_NDK_LATEST_HOME=/usr/local/lib/android/sdk/ndk/27.2.12479018
//SYSTEMD_EXEC_PID=477
//GITHUB_SHA=b3e991ecb7457ce6752fb7c56aa4eaa0335222fa
//GITHUB_WORKFLOW_REF=martasp/FuseDrill/.github/workflows/FuseDrillSnapshot.yml@refs/pull/2/merge
//POWERSHELL_DISTRIBUTION_CHANNEL=GitHub-Actions-ubuntu22
//RUNNER_ENVIRONMENT=github-hosted
//STATS_EXTP=https://provjobdprod.z13.web.core.windows.net/settings/provjobdsettings-latest/provjobd.data
//DOTNET_MULTILEVEL_LOOKUP=0
//GITHUB_REF=refs/pull/2/merge
//RUNNER_OS=Linux
//GITHUB_REF_PROTECTED=false
//HOME=/home/runner
//GITHUB_API_URL=https://api.github.com
//LANG=C.UTF-8
//GITHUB_TOKEN=***
//RUNNER_TRACKING_ID=github_1b014f0e-77cd-4f3d-9227-c49b72b2edee
//RUNNER_ARCH=X64
//GOROOT_1_21_X64=/opt/hostedtoolcache/go/1.21.13/x64
//RUNNER_TEMP=/home/runner/work/_temp
//GITHUB_STATE=/home/runner/work/_temp/_runner_file_commands/save_state_5ca5fb58-f1df-4e8d-b521-25a79ada7402
//STATS_PIP=false
//EDGEWEBDRIVER=/usr/local/share/edge_driver
//JAVA_HOME_21_X64=/usr/lib/jvm/temurin-21-jdk-amd64
//GITHUB_ENV=/home/runner/work/_temp/_runner_file_commands/set_env_5ca5fb58-f1df-4e8d-b521-25a79ada7402
//GITHUB_EVENT_PATH=/home/runner/work/_temp/_github_workflow/event.json
//INVOCATION_ID=aaa9176131fc43b8b660bd55da55b3eb
//STATS_D=true
//GITHUB_EVENT_NAME=pull_request
//GITHUB_RUN_ID=12185930383
//JAVA_HOME_17_X64=/usr/lib/jvm/temurin-17-jdk-amd64
//ANDROID_NDK_HOME=/usr/local/lib/android/sdk/ndk/27.2.12479018
//GITHUB_STEP_SUMMARY=/home/runner/work/_temp/_runner_file_commands/step_summary_5ca5fb58-f1df-4e8d-b521-25a79ada7402
//HOMEBREW_NO_AUTO_UPDATE=1
//GITHUB_ACTOR=martasp
//NVM_DIR=/home/runner/.nvm
//SGX_AESM_ADDR=1
//GITHUB_RUN_ATTEMPT=1
//STATS_RDCL=true
//ANDROID_HOME=/usr/local/lib/android/sdk
//GITHUB_GRAPHQL_URL=https://api.github.com/graphql
//ACCEPT_EULA=Y
//RUNNER_USER=runner
//STATS_UE=true
//USER=runner
//GITHUB_SERVER_URL=https://github.com
//STATS_V3PS=true
//PIPX_HOME=/opt/pipx
//GECKOWEBDRIVER=/usr/local/share/gecko_driver
//STATS_EXT=true
//CHROMEWEBDRIVER=/usr/local/share/chromedriver-linux64
//SHLVL=1
//ANDROID_SDK_ROOT=/usr/local/lib/android/sdk
//VCPKG_INSTALLATION_ROOT=/usr/local/share/vcpkg
//GITHUB_ACTOR_ID=13511209
//RUNNER_TOOL_CACHE=/opt/hostedtoolcache
//ImageVersion=20241201.1.0
//DOTNET_NOLOGO=1
//GOROOT_1_23_X64=/opt/hostedtoolcache/go/1.23.3/x64
//GITHUB_WORKFLOW_SHA=b3e991ecb7457ce6752fb7c56aa4eaa0335222fa
//GITHUB_REF_NAME=2/merge
//GITHUB_JOB=build
//XDG_RUNTIME_DIR=/run/user/1001
//AZURE_EXTENSION_DIR=/opt/az/azcliextensions
//PERFLOG_LOCATION_SETTING=RUNNER_PERFLOG
//STATS_VMFE=true
//GITHUB_REPOSITORY=martasp/FuseDrill
//GOROOT_1_22_X64=/opt/hostedtoolcache/go/1.22.9/x64
//ANDROID_NDK_ROOT=/usr/local/lib/android/sdk/ndk/27.2.12479018
//CHROME_BIN=/usr/bin/google-chrome
//GITHUB_RETENTION_DAYS=90
//JOURNAL_STREAM=8:17346
//RUNNER_WORKSPACE=/home/runner/work/FuseDrill
//LEIN_HOME=/usr/local/lib/lein
//LEIN_JAR=/usr/local/lib/lein/self-installs/leiningen-2.11.2-standalone.jar
//GITHUB_ACTION_REPOSITORY=
//PATH=/usr/share/dotnet:/snap/bin:/home/runner/.local/bin:/opt/pipx_bin:/home/runner/.cargo/bin:/home/runner/.config/composer/vendor/bin:/usr/local/.ghcup/bin:/home/runner/.dotnet/tools:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/games:/usr/local/games:/snap/bin
//RUNNER_PERFLOG=/home/runner/perflog
//GITHUB_BASE_REF=main
//GHCUP_INSTALL_BASE_PREFIX=/usr/local
//CI=true
//SWIFT_PATH=/usr/share/swift/usr/bin
//ImageOS=ubuntu22
//STATS_D_D=true
//GITHUB_REPOSITORY_OWNER=martasp
//GITHUB_HEAD_REF=test2
//GITHUB_ACTION_REF=
//STATS_D_TC=true
//GITHUB_WORKFLOW=.NET
//DEBIAN_FRONTEND=noninteractive
//GITHUB_OUTPUT=/home/runner/work/_temp/_runner_file_commands/set_output_5ca5fb58-f1df-4e8d-b521-25a79ada7402
//AGENT_TOOLSDIRECTORY=/opt/hostedtoolcache
//_=/usr/bin/printenv

// Get all GitHub Actions environment variables
var envars = Environment.GetEnvironmentVariables();

var owner = envars["GITHUB_REPOSITORY_OWNER"]?.ToString(); // e.g., "owner"
var repoName = envars["GITHUB_REPOSITORY"]?.ToString()?.Split('/')?[1]; // e.g., "repo-name"
var branch = envars["GITHUB_HEAD_REF"]?.ToString(); // e.g., "refs/heads/branch-name"
var githubToken = envars["GITHUB_TOKEN"]?.ToString();
var geminiToken = envars["GEMINI_API_KEY"]?.ToString();
var pullReqestNumber = envars["GITHUB_REF_NAME"]?.ToString()?.Split('/')?[0]; // e.g., 20/merge => 20

var fuseDrillBaseAddres = (envars["FUSEDRILL_BASE_ADDRESS"] ?? throw new Exception("FUSEDRILL_BASE_ADDRESS not found in environment variables.")).ToString();
var fuseDrillOpenApiUrl = (envars["FUSEDRILL_OPENAPI_URL"] ?? throw new Exception("FUSEDRILL_OPENAPI_URL not found in environment variables.")).ToString();
var fuseDrillTestAccountOAuthHeaderValue = envars["FUSEDRILL_TEST_ACCOUNT_OAUTH_HEADER_VALUE"]?.ToString();
var smokeFlag = envars["SMOKE_FLAG"]?.ToString() == "true";

#if DEBUG
// just dotnet run in D:\main\FuseDrill\tests\TestApi\TestApi.csproj
//"http://localhost:5184/swagger/v1/swagger.json"
fuseDrillBaseAddres = "http://localhost:5184/";
fuseDrillOpenApiUrl = "http://localhost:5184/swagger/v1/swagger.json";
githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
geminiToken = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
branch = "Ordering_bug";
repoName = "FuseDrill";
owner = "martasp";
pullReqestNumber = "26";
#endif


await CliFlow(owner, repoName, branch, githubToken, fuseDrillBaseAddres, fuseDrillOpenApiUrl, fuseDrillTestAccountOAuthHeaderValue, smokeFlag, pullReqestNumber, geminiToken);
