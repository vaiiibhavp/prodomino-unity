#!/usr/bin/env bash
#
# setup-antigravity.sh — Configure Antigravity AI + Unity MCP for this ProDomino project.
#
# Usage:
#   chmod +x Tools/setup-antigravity.sh
#   ./Tools/setup-antigravity.sh
#
# This is OPTIONAL. Antigravity also auto-discovers the project-level config
# at .agents/mcp.json (which uses a relative path and needs no setup).
#
# This global setup is useful if you want the Unity MCP tools available in ALL
# Antigravity sessions, not just when this project workspace is open.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
SERVER_JS="$REPO_ROOT/.agents/mcp/unity-bridge/server.js"

if [ ! -f "$SERVER_JS" ]; then
    echo "ERROR: Could not find $SERVER_JS"
    echo "Make sure you run this script from the repository root."
    exit 1
fi

# Gemini config directory
GEMINI_CONFIG_DIR="$HOME/.gemini/config"
MCP_CONFIG_PATH="$GEMINI_CONFIG_DIR/mcp_config.json"

# Create config directory if needed
mkdir -p "$GEMINI_CONFIG_DIR"

# Build the MCP config JSON
# If jq is available, merge with existing config; otherwise overwrite
if command -v jq &> /dev/null && [ -f "$MCP_CONFIG_PATH" ]; then
    # Merge: add/overwrite the "unity" server, keep others
    UPDATED=$(jq --arg server_path "$SERVER_JS" \
        '.mcpServers.unity = {"command": "node", "args": [$server_path]}' \
        "$MCP_CONFIG_PATH")
    echo "$UPDATED" > "$MCP_CONFIG_PATH"
else
    # Write fresh config
    cat > "$MCP_CONFIG_PATH" << EOF
{
  "mcpServers": {
    "unity": {
      "command": "node",
      "args": ["$SERVER_JS"]
    }
  }
}
EOF
fi

echo ""
echo "=== Antigravity MCP Setup Complete ==="
echo ""
echo "Global config written to:"
echo "  $MCP_CONFIG_PATH"
echo ""
echo "Unity MCP server path:"
echo "  $SERVER_JS"
echo ""
echo "Next steps:"
echo "  1. Open the project in Unity Editor (the MCP bridge auto-starts)"
echo "  2. Open Antigravity — Unity MCP tools will be available"
echo "  3. Verify: In Unity, go to ProDomino > MCP > Check Status"
echo ""
