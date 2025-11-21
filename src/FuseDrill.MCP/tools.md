# FuseDrill MCP Tools

This document describes the Model Context Protocol (MCP) tools and configuration used in the FuseDrill project.

## Configuration


## Tools

- First pass fuzzing in order to know what is the appi request and response structure.
- Specific endpoint api fuzzing with reqest parameters.
- Incorporate all the knowledge and create a test code and add it codebase.


## Use MCP config in antigravity path : C:\Users\user\.gemini\antigravity\mcp_config.json

```json
{
  "mcpServers": {
    "fusedrill": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "d:\\main\\org\\FuseDrill\\src\\FuseDrill.MCP\\FuseDrill.MCP.csproj"
      ],
      "disabled": false
    }
  }
}
```