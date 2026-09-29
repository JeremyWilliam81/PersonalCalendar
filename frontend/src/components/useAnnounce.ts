import { createContext, useContext } from 'react'

export type Announce = (message: string) => void

export const AnnounceContext = createContext<Announce>(() => {})

/** Announces a message to screen reader users through the page's single live region (FR-019). */
export function useAnnounce(): Announce {
  return useContext(AnnounceContext)
}
