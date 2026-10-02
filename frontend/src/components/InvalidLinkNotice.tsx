import { useEffect } from 'react'
import { useAnnounce } from './useAnnounce'

const MESSAGE = "That link couldn't be opened, so you're seeing this month."

/** Shown when the address named no valid view or date (FR-020). */
export function InvalidLinkNotice({ onDismiss }: { onDismiss: () => void }) {
  const announce = useAnnounce()

  useEffect(() => {
    announce(MESSAGE)
  }, [announce])

  return (
    <div className="notice">
      <p>{MESSAGE}</p>
      <button type="button" onClick={onDismiss}>
        Dismiss
      </button>
    </div>
  )
}
