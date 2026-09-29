import { useEffect, useRef, type KeyboardEvent, type ReactNode, type RefObject } from 'react'

interface ModalProps {
  labelledBy: string
  describedBy?: string
  /** Escape or the browser's cancel gesture. */
  onCancel: () => void
  initialFocusRef?: RefObject<HTMLElement | null>
  className?: string
  children: ReactNode
}

function fallbackFocusTarget(): HTMLElement | null {
  // If the control that opened the dialog is gone (e.g. its event was deleted), return to the grid.
  return document.querySelector<HTMLElement>('[role="gridcell"][tabindex="0"]')
}

/**
 * Native modal <dialog> (research R9): contains focus, closes on Escape via `onCancel`,
 * and returns focus to the control that opened it when it unmounts.
 */
export function Modal({ labelledBy, describedBy, onCancel, initialFocusRef, className, children }: ModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const onCancelRef = useRef(onCancel)
  onCancelRef.current = onCancel

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null

    if (!dialog.open) dialog.showModal()
    const initial =
      initialFocusRef?.current ??
      dialog.querySelector<HTMLElement>('input, textarea, select, button, [tabindex]:not([tabindex="-1"])')
    initial?.focus()

    const handleCancel = (event: Event) => {
      event.preventDefault()
      onCancelRef.current()
    }
    dialog.addEventListener('cancel', handleCancel)

    return () => {
      dialog.removeEventListener('cancel', handleCancel)
      if (dialog.open) dialog.close()
      const target = opener?.isConnected ? opener : fallbackFocusTarget()
      target?.focus()
    }
    // Runs once per opening; the initial focus target is fixed for the dialog's lifetime.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleKeyDown = (event: KeyboardEvent<HTMLDialogElement>) => {
    if (event.key !== 'Escape') return
    event.preventDefault()
    event.stopPropagation()
    onCancelRef.current()
  }

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={labelledBy}
      aria-describedby={describedBy}
      className={className ? `modal ${className}` : 'modal'}
      onKeyDown={handleKeyDown}
    >
      {children}
    </dialog>
  )
}
