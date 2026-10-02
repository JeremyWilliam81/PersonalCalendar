import { useSyncExternalStore } from 'react'

// Phone-sized screens (FR-004a, research V6).
const QUERY = '(max-width: 599px)'

function subscribe(onChange: () => void): () => void {
  const list = window.matchMedia(QUERY)
  list.addEventListener('change', onChange)
  return () => list.removeEventListener('change', onChange)
}

export function useNarrowScreen(): boolean {
  return useSyncExternalStore(subscribe, () => window.matchMedia(QUERY).matches)
}
