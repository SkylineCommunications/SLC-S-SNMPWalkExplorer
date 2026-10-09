export interface DataMinerUser {
  name: string
}

export function getCookie(name: string): string | undefined {
  const prefix = `${name}=`
  const cookies = document.cookie.split(';')
  for (const item of cookies) {
    const trimmed = item.trim()
    if (trimmed.startsWith(prefix)) {
      return decodeURIComponent(trimmed.slice(prefix.length))
    }
  }
  return undefined
}

export function getConnectionId(): string | undefined {
  return getCookie('DMAConnection')
}

export function getCurrentUser(): DataMinerUser | undefined {
  const userCookie = getCookie('DMAUser')
  if (!userCookie) {
    return undefined
  }

  try {
    const parsed = JSON.parse(userCookie) as {
      FullName?: string
      Login?: string
      Name?: string
      name?: string
    }
    const name = parsed.FullName ?? parsed.Login ?? parsed.Name ?? parsed.name
    return name ? { name } : undefined
  } catch {
    return userCookie ? { name: userCookie } : undefined
  }
}

export function triggerReAuth(): void {
  const attemptedKey = 'app_auth_attempted'
  const attemptedTime = sessionStorage.getItem(attemptedKey)
  const now = Date.now()

  // Loop protection: avoid redirect loop if attempted within the last 15 seconds
  if (attemptedTime && now - Number(attemptedTime) < 15000) {
    console.warn('Authentication was recently attempted. Waiting to avoid redirect loops.')
    return
  }

  sessionStorage.setItem(attemptedKey, String(now))
  const targetUrl = window.location.pathname + window.location.search + window.location.hash
  const authUrl = `/auth/?url=${encodeURIComponent(targetUrl)}`

  try {
    if (window.top && window.top !== window) {
      window.top.location.replace(authUrl)
      return
    }
  } catch {
    // Cross-origin iframe fallback
  }

  window.location.replace(authUrl)
}

export type KeepAliveStatus = 'connected' | 'connecting' | 'disconnected'

export function startKeepAlive(
  connectionId: string,
  onStatusChange?: (status: KeepAliveStatus) => void,
): () => void {
  let isDisposed = false
  let socket: WebSocket | null = null
  let pingInterval: number | null = null
  let reconnectTimeout: number | null = null

  function connect() {
    if (isDisposed) return

    onStatusChange?.('connecting')
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
    const url = `${protocol}//${window.location.host}/API/v1/WebSocket.ashx`

    try {
      socket = new WebSocket(url)
    } catch {
      onStatusChange?.('disconnected')
      scheduleReconnect()
      return
    }

    socket.onopen = () => {
      if (isDisposed) {
        socket?.close()
        return
      }

      onStatusChange?.('connected')
      sessionStorage.removeItem('app_auth_attempted')

      // Step 1: Bind connection ID
      socket?.send(
        JSON.stringify({
          ClientSubscriptionID: 1,
          Method: 'SetConnectionID',
          Parameters: { connectionID: connectionId },
        }),
      )

      // Step 2: Ping every 30 seconds
      if (pingInterval) clearInterval(pingInterval)
      pingInterval = window.setInterval(() => {
        if (socket?.readyState === WebSocket.OPEN) {
          socket.send(
            JSON.stringify({
              ClientSubscriptionID: 0,
              Method: 'Ping',
              Parameters: {},
            }),
          )
        }
      }, 30000)
    }

    socket.onerror = () => {
      onStatusChange?.('disconnected')
    }

    socket.onclose = () => {
      if (pingInterval) {
        clearInterval(pingInterval)
        pingInterval = null
      }
      onStatusChange?.('disconnected')
      if (!isDisposed) {
        scheduleReconnect()
      }
    }
  }

  function scheduleReconnect() {
    if (isDisposed || reconnectTimeout) return
    reconnectTimeout = window.setTimeout(() => {
      reconnectTimeout = null
      connect()
    }, 10000)
  }

  connect()

  return () => {
    isDisposed = true
    if (pingInterval) clearInterval(pingInterval)
    if (reconnectTimeout) clearTimeout(reconnectTimeout)
    if (socket) {
      socket.onclose = null
      socket.onerror = null
      socket.close()
    }
  }
}
