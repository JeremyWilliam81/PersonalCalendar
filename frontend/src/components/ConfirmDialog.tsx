import { useId, useRef } from 'react'
import { Modal } from './Modal'

interface ConfirmDialogProps {
  title: string
  description?: string
  confirmLabel: string
  cancelLabel: string
  initialFocus: 'confirm' | 'cancel'
  onConfirm: () => void
  onCancel: () => void
}

export function ConfirmDialog({
  title,
  description,
  confirmLabel,
  cancelLabel,
  initialFocus,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const titleId = useId()
  const descriptionId = useId()
  const confirmRef = useRef<HTMLButtonElement>(null)
  const cancelRef = useRef<HTMLButtonElement>(null)

  return (
    <Modal
      labelledBy={titleId}
      describedBy={description ? descriptionId : undefined}
      onCancel={onCancel}
      initialFocusRef={initialFocus === 'confirm' ? confirmRef : cancelRef}
      className="modal-confirm"
    >
      <h2 id={titleId}>{title}</h2>
      {description && <p id={descriptionId}>{description}</p>}
      <div className="dialog-actions">
        <button type="button" ref={confirmRef} className="button-primary" onClick={onConfirm}>
          {confirmLabel}
        </button>
        <button type="button" ref={cancelRef} onClick={onCancel}>
          {cancelLabel}
        </button>
      </div>
    </Modal>
  )
}
