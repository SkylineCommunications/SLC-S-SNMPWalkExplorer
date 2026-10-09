import { getConnectionId, triggerReAuth } from './auth'

const automationEndpoint = '/API/v1/Json.asmx/ExecuteAutomationScriptWithOutput'
const bridgeScriptName = 'SLC-S-SNMPWalkExplorer.Bridge'

interface ScriptOutput {
  Key?: string
  key?: string
  Name?: string
  Value?: string
  name?: string
  value?: string
}

interface AutomationResponse {
  ExceptionType?: string
  Message?: string
  d?: {
    ScriptOutput?: ScriptOutput[]
    Output?: ScriptOutput[]
  }
}

function getConnection(): string {
  const connectionId = getConnectionId()
  if (!connectionId) {
    triggerReAuth()
    throw new Error('Your DataMiner session is unavailable. Open this browser through DataMiner authentication.')
  }

  return connectionId
}

function getResult(outputs: ScriptOutput[] | undefined): string {
  const result = outputs?.find((output) => {
    const key = output.Key ?? output.key ?? output.Name ?? output.name
    return key?.toLowerCase() === 'result'
  })
  const value = result?.Value ?? result?.value
  if (value === undefined) {
    throw new Error('The SNMP Walk Explorer bridge did not return a result.')
  }

  return value
}

export async function executeBridge<T>(action: string, request?: object): Promise<T> {
  const connection = getConnection()

  const response = await fetch(automationEndpoint, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      connection,
      script: {
        __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScript',
        Name: bridgeScriptName,
        Folder: '',
        Parameters: [
          {
            __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScriptParameter',
            ParameterId: 1,
            Name: 'Action',
            Description: 'Action',
            Value: action,
          },
          {
            __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScriptParameter',
            ParameterId: 2,
            Name: 'RequestJson',
            Description: 'RequestJson',
            Value: JSON.stringify(request ?? {}),
          },
          {
            __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScriptParameter',
            ParameterId: 3,
            Name: 'Result',
            Description: 'Result',
            Value: '-',
          },
        ],
        Dummies: [],
        MemoryFiles: [],
      },
      scriptOptions: {
        __type: 'Skyline.DataMiner.Web.Common.v1.DMAAutomationScriptOptions',
        WaitForScript: true,
        hideSuccessPopup: true,
      },
    }),
  })

  const payload = (await response.json().catch(() => undefined)) as AutomationResponse | undefined

  if (
    response.status === 401 ||
    response.status === 403 ||
    payload?.ExceptionType?.includes('NoConnectionWebApiException')
  ) {
    triggerReAuth()
    throw new Error('Your DataMiner session has expired. Re-authenticating...')
  }

  if (!response.ok) {
    throw new Error(payload?.Message ?? `The SNMP Walk Explorer bridge returned ${response.status}.`)
  }

  const outputs = payload?.d?.ScriptOutput ?? payload?.d?.Output
  return JSON.parse(getResult(outputs)) as T
}