import type { EditScope } from '../api/types'

export const ALL_SCOPES: EditScope[] = ['this', 'following', 'all']

/**
 * The choices to offer (contracts/ui-interaction.md "Scope choice"). Null means "don't ask": a new date and new repeat
 * options can't be saved together (FR-016a).
 */
export function scopeChoices({
  mode,
  dateChanged,
  repeatChanged,
}: {
  mode: 'edit' | 'delete'
  dateChanged: boolean
  repeatChanged: boolean
}): EditScope[] | null {
  if (mode === 'delete') return ALL_SCOPES
  if (dateChanged && repeatChanged) return null
  if (dateChanged) return ['this'] // FR-016a
  if (repeatChanged) return ['following', 'all'] // FR-016
  return ALL_SCOPES
}
