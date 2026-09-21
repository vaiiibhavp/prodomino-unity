#!/usr/bin/env node
const http = require('http');
const readline = require('readline');

const UNITY_HOST = '127.0.0.1';
const UNITY_PORT = 8080;

function httpRequest(path, method = 'GET', body = null) {
  return new Promise((resolve, reject) => {
    const options = {
      hostname: UNITY_HOST,
      port: UNITY_PORT,
      path: path,
      method: method,
      headers: {
        'Content-Type': 'application/json',
      },
      timeout: 6000,
    };

    const req = http.request(options, (res) => {
      let data = '';
      res.on('data', (chunk) => { data += chunk; });
      res.on('end', () => {
        try {
          resolve(JSON.parse(data));
        } catch (e) {
          resolve({ raw: data });
        }
      });
    });

    req.on('error', (err) => {
      resolve({
        error: `Could not connect to Unity Editor on ${UNITY_HOST}:${UNITY_PORT}. Make sure Unity Editor is running and UnityMcpBridge is loaded. Details: ${err.message}`
      });
    });

    req.on('timeout', () => {
      req.destroy();
      resolve({ error: 'Request to Unity Editor timed out.' });
    });

    if (body) {
      req.write(typeof body === 'string' ? body : JSON.stringify(body));
    }
    req.end();
  });
}

const TOOLS = [
  {
    name: 'unity_status',
    description: 'Check connection to the live Unity Editor, active scene, project name, and play mode state.',
    inputSchema: {
      type: 'object',
      properties: {},
    },
  },
  {
    name: 'unity_execute_menu_item',
    description: 'Execute any Unity Editor menu item on the main thread (e.g., "ProDomino/Dashboard/Restyle Achievements + Render").',
    inputSchema: {
      type: 'object',
      properties: {
        menuPath: {
          type: 'string',
          description: 'The exact path of the menu item (e.g. "ProDomino/Dashboard/Restyle Achievements + Render", "File/Save Project")',
        },
      },
      required: ['menuPath'],
    },
  },
  {
    name: 'unity_get_logs',
    description: 'Retrieve recent Unity Console logs, warnings, and errors from the running Editor.',
    inputSchema: {
      type: 'object',
      properties: {
        type: {
          type: 'string',
          enum: ['All', 'Error', 'Warning', 'Log', 'Exception'],
          description: 'Optional filter for log type. Default is All.',
        },
      },
    },
  },
  {
    name: 'unity_clear_logs',
    description: 'Clear the captured log buffer in the Unity Editor.',
    inputSchema: {
      type: 'object',
      properties: {},
    },
  },
  {
    name: 'unity_get_hierarchy',
    description: 'Get root GameObjects and structure of the currently active scene in Unity.',
    inputSchema: {
      type: 'object',
      properties: {},
    },
  },
  {
    name: 'unity_inspect_object',
    description: 'Inspect components, active state, and RectTransform properties of a named GameObject in the active scene.',
    inputSchema: {
      type: 'object',
      properties: {
        target: {
          type: 'string',
          description: 'Name of the GameObject to inspect (e.g. "Achievements_Screen", "Canvas")',
        },
      },
      required: ['target'],
    },
  },
  {
    name: 'unity_play_mode',
    description: 'Control Play Mode in the Unity Editor: start playing, pause, or stop.',
    inputSchema: {
      type: 'object',
      properties: {
        action: {
          type: 'string',
          enum: ['play', 'pause', 'stop'],
          description: 'Action to perform: play, pause, or stop.',
        },
      },
      required: ['action'],
    },
  },
  {
    name: 'unity_capture_screenshot',
    description: 'Capture a screenshot of the active Game/Scene view and return the file path.',
    inputSchema: {
      type: 'object',
      properties: {},
    },
  },
];

const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
  terminal: false,
});

rl.on('line', async (line) => {
  if (!line.trim()) return;
  try {
    const msg = JSON.parse(line);
    const { id, method, params } = msg;

    if (method === 'initialize') {
      sendResult(id, {
        protocolVersion: '2024-11-05',
        capabilities: {
          tools: {},
        },
        serverInfo: {
          name: 'unity-mcp-server',
          version: '1.0.0',
        },
      });
      return;
    }

    if (method === 'notifications/initialized') {
      return;
    }

    if (method === 'tools/list') {
      sendResult(id, { tools: TOOLS });
      return;
    }

    if (method === 'tools/call') {
      const toolName = params.name;
      const args = params.arguments || {};
      const result = await handleToolCall(toolName, args);
      sendResult(id, {
        content: [
          {
            type: 'text',
            text: typeof result === 'string' ? result : JSON.stringify(result, null, 2),
          },
        ],
      });
      return;
    }

    if (id !== undefined) {
      sendError(id, -32601, `Method not found: ${method}`);
    }
  } catch (err) {
    // Malformed JSON
  }
});

async function handleToolCall(name, args) {
  switch (name) {
    case 'unity_status':
      return await httpRequest('/status');

    case 'unity_execute_menu_item':
      return await httpRequest('/menu-item', 'POST', { menuPath: args.menuPath });

    case 'unity_get_logs':
      const res = await httpRequest('/logs');
      if (res.logs && args.type && args.type !== 'All') {
        res.logs = res.logs.filter((l) => l.type.toLowerCase() === args.type.toLowerCase());
      }
      return res;

    case 'unity_clear_logs':
      return await httpRequest('/clear-logs', 'POST');

    case 'unity_get_hierarchy':
      return await httpRequest('/hierarchy');

    case 'unity_inspect_object':
      return await httpRequest('/inspect', 'POST', { target: args.target });

    case 'unity_play_mode':
      return await httpRequest('/play-mode', 'POST', { action: args.action });

    case 'unity_capture_screenshot':
      return await httpRequest('/screenshot', 'POST');

    default:
      return { error: `Unknown tool: ${name}` };
  }
}

function sendResult(id, result) {
  const resp = {
    jsonrpc: '2.0',
    id: id,
    result: result,
  };
  process.stdout.write(JSON.stringify(resp) + '\n');
}

function sendError(id, code, message) {
  const resp = {
    jsonrpc: '2.0',
    id: id,
    error: {
      code: code,
      message: message,
    },
  };
  process.stdout.write(JSON.stringify(resp) + '\n');
}
