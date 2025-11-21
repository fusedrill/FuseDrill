# //  Could not copy "D:\main\org\FuseDrill\src\FuseDrill.Core\bin\Debug\net8.0\FuseDrill.Core.dll" to
# //   "bin\Debug\net9.0\FuseDrill.Core.dll".
# //   Beginning retry 5 in 1000ms. The process cannot access the file: 
# //   'D:\main\org\FuseDrill\src\FuseDrill.MCP\bin\Debug\net9.0\FuseDrill.Core.dll'
# //    because it is being used by another process. The file is locked by: "FuseDrill.MCP (5960)"

# // When developing mcp server tool the problem is you want to test and rebuild it.
# // So you need to stop the mcp server and rebuild it
# // Then you need to restart the mcp server on vs code antigravity or cli tools.
# // This script will help you to do that 

Stop-Process -Name "dotnet" -Force
jq '.mcpServers.fusedrill.disabled = true' 'C:\Users\marta\.gemini\antigravity\mcp_config.json' > temp.json; Move-Item -Force temp.json 'C:\Users\marta\.gemini\antigravity\mcp_config.json' 
dotnet build FuseDrill.MCP.csproj

# // Then restart the mcp server on vs code antigravity or cli tools.
# //C:\Users\marta\.gemini\antigravity\mcp_config.json
# //edit this value to "disabled": false

# //```json
# //{
# //  "mcpServers": {
# //    "fusedrill": {
# //      "command": "dotnet",
# //      "args": [
# //        "run",
# //        "--project",
# //        "d:\\main\\org\\FuseDrill\\src\\FuseDrill.MCP\\FuseDrill.MCP.csproj"
# //      ],
# //      "disabled": false
# //    }
# //  }
# //}
# //```

jq '.mcpServers.fusedrill.disabled = false' 'C:\Users\marta\.gemini\antigravity\mcp_config.json' > temp.json; Move-Item -Force temp.json 'C:\Users\marta\.gemini\antigravity\mcp_config.json'

Solved with msbuild prebuild step, to iterate faster.
<!-- // add prebuild step and kill the mcp server ModelContextProtocol  that runs in vs code or antigravity  -->
<!-- Image    PID    Type    Handle Name
FuseDrill.MCP.exe    35396    File    D:\main\org\FuseDrill\src\FuseDrill.MCP\bin\Debug\net9.0\Microsoft.Extensions.Logging.Configuration.dll -->
<Target Name="PreBuildKillMCP" BeforeTargets="PreBuildEvent">
  <Exec IgnoreExitCode="true" Command="taskkill /F /IM FuseDrill.MCP.exe 2&gt;NUL" />
</Target>
