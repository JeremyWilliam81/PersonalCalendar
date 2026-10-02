import { useCallback, useEffect, useState } from 'react'
import { parsePath, toPath, type ViewState } from '../lib/viewState'

export type HistoryMode = 'push' | 'replace'

/**
 * The view state, kept in the page address (research V1). No router: the History API is enough for one screen.
 * Changes of view or period push an entry; moves inside the shown period replace it (FR-021).
 */
export function useViewState(today: string) {
  const [initial] = useState(() => parsePath(window.location.pathname, today))
  const [state, setStateValue] = useState<ViewState>(initial.state)
  const [error, setError] = useState<'invalid-link' | null>(initial.error ?? null)
  const [popCount, setPopCount] = useState(0)

  useEffect(() => {
    // Correct an unreadable or incomplete address to match what is shown (FR-020).
    if (window.location.pathname !== toPath(initial.state)) window.history.replaceState(null, '', toPath(initial.state))
  }, [initial])

  useEffect(() => {
    const onPopState = () => {
      setStateValue(parsePath(window.location.pathname, today).state)
      setPopCount((n) => n + 1)
    }
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [today])

  const setState = useCallback((next: ViewState, mode: HistoryMode) => {
    const path = toPath(next)
    if (path !== window.location.pathname) {
      if (mode === 'push') window.history.pushState(null, '', path)
      else window.history.replaceState(null, '', path)
    }
    setStateValue(next)
  }, [])

  const clearError = useCallback(() => setError(null), [])

  return { state, setState, error, clearError, popCount }
}
