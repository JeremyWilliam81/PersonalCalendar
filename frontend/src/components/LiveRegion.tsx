import { useCallback, useState, type ReactNode } from 'react'
import { AnnounceContext } from './useAnnounce'

export function LiveRegionProvider({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState('')

  // Toggling a trailing no-break space makes screen readers repeat an identical message.
  const announce = useCallback((next: string) => {
    setMessage((previous) => (previous === next ? `${next} ` : next))
  }, [])

  return (
    <AnnounceContext.Provider value={announce}>
      {children}
      <div role="status" aria-live="polite" aria-atomic="true" className="visually-hidden">
        {message}
      </div>
    </AnnounceContext.Provider>
  )
}
