import { useId, useRef, useState } from 'react'
import type { EditScope } from '../api/types'
import { Modal } from './Modal'

const LABELS: Record<EditScope, string> = {
  this: 'This event',
  following: 'This and following events',
  all: 'All events',
}

interface ScopeChoiceDialogProps {
  mode: 'edit' | 'delete'
  choices: EditScope[]
  /** A warning to confirm before the choice is made, or null when none is needed (FR-020, FR-021). */
  warning?: (scope: EditScope) => string | null
  onChoose: (scope: EditScope) => void
  onCancel: () => void
}

/**
 * Asks where a change to an occurrence applies (FR-015, FR-023). Touch first: stacked full-width buttons at least
 * 48 px tall (research S11). Focus starts on the first choice and returns to the opener on close.
 */
export function ScopeChoiceDialog({ mode, choices, warning, onChoose, onCancel }: ScopeChoiceDialogProps) {
  const titleId = useId()
  const warningId = useId()
  const firstChoiceRef = useRef<HTMLButtonElement>(null)
  const [pending, setPending] = useState<{ scope: EditScope; text: string } | null>(null)

  const choose = (scope: EditScope) => {
    const text = warning?.(scope)
    if (text) setPending({ scope, text })
    else onChoose(scope)
  }

  return (
    <Modal
      labelledBy={titleId}
      describedBy={pending ? warningId : undefined}
      onCancel={pending ? () => setPending(null) : onCancel}
      initialFocusRef={firstChoiceRef}
      className="scope-dialog"
    >
      <h2 id={titleId}>{mode === 'edit' ? 'Change recurring event' : 'Delete recurring event'}</h2>

      {pending ? (
        <>
          <p id={warningId}>{pending.text}</p>
          <div className="dialog-actions">
            <button type="button" className="button-primary" autoFocus onClick={() => onChoose(pending.scope)}>
              {mode === 'edit' ? 'Save anyway' : 'Delete anyway'}
            </button>
            <button type="button" onClick={() => setPending(null)}>
              Back
            </button>
          </div>
        </>
      ) : (
        <>
          <div className="scope-choices">
            {choices.map((scope, index) => (
              <button
                key={scope}
                ref={index === 0 ? firstChoiceRef : undefined}
                type="button"
                className="scope-choice"
                onClick={() => choose(scope)}
              >
                {LABELS[scope]}
              </button>
            ))}
          </div>
          <div className="dialog-actions">
            <button type="button" onClick={onCancel}>
              Cancel
            </button>
          </div>
        </>
      )}
    </Modal>
  )
}
